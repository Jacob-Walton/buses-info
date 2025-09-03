use crate::domain::entities::{RankingFilter, ServiceRanking};
use crate::domain::repositories::RankingRepository;
use std::error::Error;
use std::sync::Arc;

pub type Result<T> = std::result::Result<T, Box<dyn Error + Send + Sync>>;

pub struct RankingService {
    repository: Arc<dyn RankingRepository>,
}

impl RankingService {
    pub fn new(repository: Arc<dyn RankingRepository>) -> Self {
        Self { repository }
    }

    pub async fn get_service_rankings(
        &self,
        filter: Option<RankingFilter>,
    ) -> Result<Vec<ServiceRanking>> {
        let filter = filter.unwrap_or_default();
        let mut rankings = self.repository.get_service_rankings(&filter).await?;

        self.assign_ranks(&mut rankings);

        Ok(rankings)
    }

    pub async fn get_all_time_rankings(&self) -> Result<Vec<ServiceRanking>> {
        let mut rankings = self.repository.get_all_time_rankings().await?;
        self.assign_ranks(&mut rankings);
        Ok(rankings)
    }

    pub async fn get_top_services(&self, limit: usize) -> Result<Vec<ServiceRanking>> {
        let filter = RankingFilter {
            limit: Some(limit),
            ..Default::default()
        };

        let mut rankings = self.repository.get_service_rankings(&filter).await?;
        self.assign_ranks(&mut rankings);

        rankings.truncate(limit);
        Ok(rankings)
    }

    fn assign_ranks(&self, rankings: &mut [ServiceRanking]) {
        // Sort by total points descending, then by service name ascending
        rankings.sort_by(|a, b| match b.total_points.cmp(&a.total_points) {
            std::cmp::Ordering::Equal => b.service.cmp(&a.service),
            other => other,
        });

        // Assign ranks (1-based)
        for (index, ranking) in rankings.iter_mut().enumerate() {
            ranking.rank = Some(index + 1);
        }
    }

    pub fn filter_by_minimum_arrivals(
        &self,
        rankings: Vec<ServiceRanking>,
        min_arrivals: u64,
    ) -> Vec<ServiceRanking> {
        rankings
            .into_iter()
            .filter(|ranking| ranking.arrival_count >= min_arrivals)
            .collect()
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_assign_ranks() {
        let service = RankingService::new(Arc::new(MockRankingRepository));

        let mut rankings = vec![
            ServiceRanking::new("101".to_string(), 100, 10),
            ServiceRanking::new("102".to_string(), 150, 15),
            ServiceRanking::new("103".to_string(), 100, 5),
        ];

        service.assign_ranks(&mut rankings);

        assert_eq!(rankings[0].rank, Some(1)); // 102 with 150 points
        assert_eq!(rankings[1].rank, Some(2)); // 101 with 100 points
        assert_eq!(rankings[2].rank, Some(3)); // 103 with 100 points but later in order
    }

    #[test]
    fn test_filter_by_minimum_arrivals() {
        let service = RankingService::new(Arc::new(MockRankingRepository));

        let rankings = vec![
            ServiceRanking::new("101".to_string(), 100, 10),
            ServiceRanking::new("102".to_string(), 50, 3),
            ServiceRanking::new("103".to_string(), 75, 5),
        ];

        let filtered = service.filter_by_minimum_arrivals(rankings, 5);
        assert_eq!(filtered.len(), 2);
        assert!(filtered.iter().all(|r| r.arrival_count >= 5));
    }

    // Mock repository for testing
    struct MockRankingRepository;

    #[async_trait::async_trait]
    impl RankingRepository for MockRankingRepository {
        async fn get_service_rankings(
            &self,
            _filter: &RankingFilter,
        ) -> Result<Vec<ServiceRanking>> {
            Ok(vec![])
        }

        async fn get_all_time_rankings(&self) -> Result<Vec<ServiceRanking>> {
            Ok(vec![])
        }
    }
}
