use crate::models::BusStatus;
use std::sync::Arc;
use std::time::{Duration, Instant};
use tokio::sync::RwLock;

#[derive(Clone)]
pub struct BusCache {
    data: Arc<RwLock<Option<CacheEntry>>>,
    ttl: Duration,
}

struct CacheEntry {
    buses: Vec<BusStatus>,
    timestamp: Instant,
}

impl BusCache {
    pub fn new(ttl_minutes: u64) -> Self {
        Self {
            data: Arc::new(RwLock::new(None)),
            ttl: Duration::from_secs(ttl_minutes * 60),
        }
    }

    pub async fn get(&self) -> Option<Vec<BusStatus>> {
        let cache = self.data.read().await;

        if let Some(entry) = cache.as_ref() {
            if entry.timestamp.elapsed() < self.ttl {
                return Some(entry.buses.clone());
            }
        }

        None
    }

    pub async fn set(&self, buses: Vec<BusStatus>) {
        let mut cache = self.data.write().await;
        *cache = Some(CacheEntry {
            buses,
            timestamp: Instant::now(),
        });
    }

    #[allow(dead_code)]
    pub async fn is_expired(&self) -> bool {
        let cache = self.data.read().await;

        match cache.as_ref() {
            Some(entry) => entry.timestamp.elapsed() >= self.ttl,
            None => true,
        }
    }
}
