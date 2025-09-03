use serde::{Deserialize, Serialize};

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct ServiceRankingDto {
    pub service: String,
    pub total_points: u64,
    pub arrival_count: u64,
    pub rank: Option<usize>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct RankingsResponse {
    pub rankings: Vec<ServiceRankingDto>,
    pub total_services: usize,
    pub generated_at: chrono::DateTime<chrono::Utc>,
}

#[derive(Debug, Clone, Deserialize)]
pub struct RankingsQuery {
    pub limit: Option<usize>,
    pub min_arrivals: Option<u64>,
}

impl Default for RankingsQuery {
    fn default() -> Self {
        Self {
            limit: Some(50),
            min_arrivals: Some(1),
        }
    }
}

impl From<crate::domain::entities::ServiceRanking> for ServiceRankingDto {
    fn from(ranking: crate::domain::entities::ServiceRanking) -> Self {
        Self {
            service: ranking.service,
            total_points: ranking.total_points,
            arrival_count: ranking.arrival_count,
            rank: ranking.rank,
        }
    }
}

impl From<ServiceRankingDto> for crate::domain::entities::ServiceRanking {
    fn from(dto: ServiceRankingDto) -> Self {
        let mut ranking = Self::new(dto.service.clone(), dto.total_points, dto.arrival_count);
        ranking.rank = dto.rank;
        ranking
    }
}
