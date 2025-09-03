use crate::domain::entities::BusArrival;
use async_trait::async_trait;
use chrono::{DateTime, Utc};
use std::error::Error;

pub type Result<T> = std::result::Result<T, Box<dyn Error + Send + Sync>>;

#[async_trait]
pub trait BusRepository: Send + Sync {
    async fn log_arrival(&self, arrival: &BusArrival) -> Result<()>;
    async fn log_arrivals(&self, arrivals: &[BusArrival]) -> Result<()>;
    async fn get_arrivals_by_date_range(
        &self,
        from: DateTime<Utc>,
        to: DateTime<Utc>,
    ) -> Result<Vec<BusArrival>>;
    async fn get_today_arrivals(&self) -> Result<Vec<BusArrival>>;
}
