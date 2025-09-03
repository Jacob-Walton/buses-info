use crate::application::dtos::{RankingsQuery, RankingsResponse, ServiceRankingDto};
use crate::domain::services::RankingService;
use std::error::Error;
use std::sync::Arc;

pub type Result<T> = std::result::Result<T, Box<dyn Error + Send + Sync>>;

pub struct GetBusRankings {
    ranking_service: Arc<RankingService>,
}

impl GetBusRankings {
    pub fn new(ranking_service: Arc<RankingService>) -> Self {
        Self { ranking_service }
    }

    pub async fn execute(&self, query: Option<RankingsQuery>) -> Result<RankingsResponse> {
        let query = query.unwrap_or_default();

        // Get rankings from domain service
        let rankings = if let Some(limit) = query.limit {
            self.ranking_service.get_top_services(limit).await?
        } else {
            self.ranking_service.get_all_time_rankings().await?
        };

        // Filter by minimum arrivals if specified
        let filtered_rankings = if let Some(min_arrivals) = query.min_arrivals {
            self.ranking_service
                .filter_by_minimum_arrivals(rankings, min_arrivals)
        } else {
            rankings
        };

        // Convert to DTOs
        let ranking_dtos: Vec<ServiceRankingDto> = filtered_rankings
            .into_iter()
            .map(|ranking| ranking.into())
            .collect();

        Ok(RankingsResponse {
            total_services: ranking_dtos.len(),
            rankings: ranking_dtos,
            generated_at: chrono::Utc::now(),
        })
    }
}
