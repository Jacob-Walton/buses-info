use serde::{Deserialize, Serialize};

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
pub struct ServiceRanking {
    pub service: String,
    pub total_points: u64,
    pub arrival_count: u64,
    pub rank: Option<usize>,
}

impl ServiceRanking {
    pub fn new(service: String, total_points: u64, arrival_count: u64) -> Self {
        Self {
            service,
            total_points,
            arrival_count,
            rank: None,
        }
    }

    pub fn with_rank(mut self, rank: usize) -> Self {
        self.rank = Some(rank);
        self
    }
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct RankingFilter {
    pub date_from: Option<chrono::DateTime<chrono::Utc>>,
    pub date_to: Option<chrono::DateTime<chrono::Utc>>,
    pub limit: Option<usize>,
}

impl Default for RankingFilter {
    fn default() -> Self {
        Self {
            date_from: None,
            date_to: None,
            limit: Some(50), // Default limit
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_service_ranking_creation() {
        let ranking = ServiceRanking::new("101".to_string(), 50, 5);
        assert_eq!(ranking.service, "101");
        assert_eq!(ranking.total_points, 50);
        assert_eq!(ranking.arrival_count, 5);
        assert_eq!(ranking.rank, None);
    }

    #[test]
    fn test_service_ranking_with_rank() {
        let ranking = ServiceRanking::new("101".to_string(), 50, 5).with_rank(1);
        assert_eq!(ranking.rank, Some(1));
    }
}
