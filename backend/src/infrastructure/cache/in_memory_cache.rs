use crate::domain::entities::Bus;
use std::sync::Arc;
use std::time::{Duration, Instant};
use tokio::sync::RwLock;

#[derive(Clone)]
pub struct InMemoryCache {
    data: Arc<RwLock<Option<CacheEntry>>>,
    ttl: Duration,
}

struct CacheEntry {
    buses: Vec<Bus>,
    created_at: Instant,
}

impl InMemoryCache {
    pub fn new(ttl_minutes: u64) -> Self {
        Self {
            data: Arc::new(RwLock::new(None)),
            ttl: Duration::from_secs(ttl_minutes * 60),
        }
    }

    pub async fn get(&self) -> Option<Vec<Bus>> {
        let guard = self.data.read().await;

        if let Some(entry) = guard.as_ref()
            && entry.created_at.elapsed() < self.ttl
        {
            return Some(entry.buses.clone());
        }

        None
    }

    pub async fn set(&self, buses: Vec<Bus>) {
        let mut guard = self.data.write().await;
        *guard = Some(CacheEntry {
            buses,
            created_at: Instant::now(),
        });
    }

    pub async fn clear(&self) {
        let mut guard = self.data.write().await;
        *guard = None;
    }

    pub async fn is_expired(&self) -> bool {
        let guard = self.data.read().await;

        if let Some(entry) = guard.as_ref() {
            entry.created_at.elapsed() >= self.ttl
        } else {
            true
        }
    }
}
