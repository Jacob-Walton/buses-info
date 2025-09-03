use crate::domain::entities::{Bay, BusArrival};
use crate::domain::repositories::BusRepository;
use crate::infrastructure::database::PostgresConnection;
use async_trait::async_trait;
use chrono::{DateTime, Utc};
use sqlx::Row;
use std::error::Error;
use std::sync::Arc;
use uuid::Uuid;

pub type Result<T> = std::result::Result<T, Box<dyn Error + Send + Sync>>;

pub struct PostgresBusRepository {
    db: Arc<PostgresConnection>,
}

impl PostgresBusRepository {
    pub fn new(db: Arc<PostgresConnection>) -> Self {
        Self { db }
    }
}

#[async_trait]
impl BusRepository for PostgresBusRepository {
    async fn log_arrival(&self, arrival: &BusArrival) -> Result<()> {
        self.log_arrivals(std::slice::from_ref(arrival)).await
    }

    async fn log_arrivals(&self, arrivals: &[BusArrival]) -> Result<()> {
        let mut transaction = self.db.pool.begin().await?;

        for arrival in arrivals {
            // Check if we already have an entry for this service today
            let today_start = arrival
                .timestamp
                .date_naive()
                .and_hms_opt(0, 0, 0)
                .unwrap()
                .and_utc();
            let today_end = arrival
                .timestamp
                .date_naive()
                .and_hms_opt(23, 59, 59)
                .unwrap()
                .and_utc();

            let existing = sqlx::query_scalar::<_, i64>(
                "SELECT COUNT(*) FROM bus_arrivals WHERE service = $1 AND timestamp >= $2 AND timestamp <= $3"
            )
            .bind(&arrival.service)
            .bind(today_start)
            .bind(today_end)
            .fetch_one(&mut *transaction)
            .await?;

            if existing > 0 {
                // Update existing record
                sqlx::query(
                    "UPDATE bus_arrivals SET bay = $1, timestamp = $2 WHERE service = $3 AND timestamp >= $4 AND timestamp <= $5"
                )
                .bind(&arrival.bay.name)
                .bind(arrival.timestamp)
                .bind(&arrival.service)
                .bind(today_start)
                .bind(today_end)
                .execute(&mut *transaction)
                .await?;
            } else {
                // Insert new record
                sqlx::query(
                    "INSERT INTO bus_arrivals (service, bay, timestamp) VALUES ($1, $2, $3)",
                )
                .bind(&arrival.service)
                .bind(&arrival.bay.name)
                .bind(arrival.timestamp)
                .execute(&mut *transaction)
                .await?;
            }
        }

        transaction.commit().await?;
        Ok(())
    }

    async fn get_arrivals_by_date_range(
        &self,
        from: DateTime<Utc>,
        to: DateTime<Utc>,
    ) -> Result<Vec<BusArrival>> {
        let rows = sqlx::query(
            r#"
            SELECT id, service, bay, timestamp
            FROM bus_arrivals 
            WHERE timestamp >= $1 AND timestamp < $2
            ORDER BY timestamp DESC
            "#,
        )
        .bind(from)
        .bind(to)
        .fetch_all(&self.db.pool)
        .await?;

        let arrivals = rows
            .into_iter()
            .map(|row| {
                let id: Uuid = row.get("id");
                let service: String = row.get("service");
                let bay_name: String = row.get("bay");
                let timestamp: DateTime<Utc> = row.get("timestamp");

                BusArrival {
                    id,
                    service,
                    bay: Bay::new(bay_name),
                    timestamp,
                }
            })
            .collect();

        Ok(arrivals)
    }

    async fn get_today_arrivals(&self) -> Result<Vec<BusArrival>> {
        let today = chrono::Utc::now().date_naive();
        let start_of_day = today.and_hms_opt(0, 0, 0).unwrap().and_utc();
        let end_of_day = today.and_hms_opt(23, 59, 59).unwrap().and_utc();

        self.get_arrivals_by_date_range(start_of_day, end_of_day)
            .await
    }
}
