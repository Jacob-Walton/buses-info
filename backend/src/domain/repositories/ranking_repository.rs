use crate::domain::entities::{RankingFilter, ServiceRanking};
use async_trait::async_trait;
use std::error::Error;

pub type Result<T> = std::result::Result<T, Box<dyn Error + Send + Sync>>;

#[async_trait]
pub trait RankingRepository: Send + Sync {
    async fn get_service_rankings(&self, filter: &RankingFilter) -> Result<Vec<ServiceRanking>>;
    async fn get_all_time_rankings(&self) -> Result<Vec<ServiceRanking>>;
}
