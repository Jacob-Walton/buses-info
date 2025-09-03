use crate::domain::entities::{RankingFilter, ServiceRanking};
use crate::domain::repositories::RankingRepository;
use crate::infrastructure::database::PostgresConnection;
use async_trait::async_trait;
use sqlx::Row;
use std::error::Error;
use std::sync::Arc;

pub type Result<T> = std::result::Result<T, Box<dyn Error + Send + Sync>>;

pub struct PostgresRankingRepository {
    db: Arc<PostgresConnection>,
}

impl PostgresRankingRepository {
    pub fn new(db: Arc<PostgresConnection>) -> Self {
        Self { db }
    }
}

#[async_trait]
impl RankingRepository for PostgresRankingRepository {
    async fn get_service_rankings(&self, filter: &RankingFilter) -> Result<Vec<ServiceRanking>> {
        let mut query = r#"
            SELECT 
                service,
                SUM(calculate_bay_points(bay)) as total_points,
                COUNT(*) as arrival_count
            FROM bus_arrivals 
        "#
        .to_string();

        let mut conditions = Vec::new();
        let mut params = Vec::new();
        let mut param_count = 0;

        // Add date filters
        if let Some(from) = filter.date_from {
            param_count += 1;
            conditions.push(format!("timestamp >= ${param_count}"));
            params.push(from);
        }

        if let Some(to) = filter.date_to {
            param_count += 1;
            conditions.push(format!("timestamp < ${param_count}"));
            params.push(to);
        }

        if !conditions.is_empty() {
            query.push_str(&format!(" WHERE {}", conditions.join(" AND ")));
        }

        query.push_str(" GROUP BY service ORDER BY total_points DESC, arrival_count DESC");

        if filter.limit.is_some() {
            param_count += 1;
            query.push_str(&format!(" LIMIT ${param_count}"));
        }

        // Build the query based on parameters
        let mut sqlx_query = sqlx::query(&query);
        for param in params {
            sqlx_query = sqlx_query.bind(param);
        }
        if let Some(limit) = filter.limit {
            sqlx_query = sqlx_query.bind(limit as i64);
        }

        let rows = sqlx_query.fetch_all(&self.db.pool).await?;

        let rankings = rows
            .into_iter()
            .map(|row| {
                let service: String = row.get("service");
                let total_points: Option<i64> = row.get("total_points");
                let arrival_count: Option<i64> = row.get("arrival_count");

                ServiceRanking::new(
                    service,
                    total_points.unwrap_or(0) as u64,
                    arrival_count.unwrap_or(0) as u64,
                )
            })
            .collect();

        Ok(rankings)
    }

    async fn get_all_time_rankings(&self) -> Result<Vec<ServiceRanking>> {
        let filter = RankingFilter::default();
        self.get_service_rankings(&filter).await
    }
}
