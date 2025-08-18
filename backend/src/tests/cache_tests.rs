use crate::cache::BusCache;
use crate::models::BusStatus;
use std::time::Duration;
use tokio::time::sleep;

#[tokio::test]
async fn test_cache_set_and_get() {
    let cache = BusCache::new(1); // 1 minute TTL

    let bus_data = vec![
        BusStatus {
            service: "Service 1".to_string(),
            bay: Some("Bay A".to_string()),
        },
        BusStatus {
            service: "Service 2".to_string(),
            bay: Some("Bay B".to_string()),
        },
    ];

    // Initially cache should be empty
    assert!(cache.get().await.is_none());

    // Set data
    cache.set(bus_data.clone()).await;

    // Get data
    let cached_data = cache.get().await.expect("Cache should have data");
    assert_eq!(cached_data.len(), 2);
    assert_eq!(cached_data[0].service, "Service 1");
    assert_eq!(cached_data[1].service, "Service 2");
}

#[tokio::test]
async fn test_cache_expiration() {
    let cache = BusCache::new(0); // 0 minute TTL for immediate expiration

    let bus_data = vec![BusStatus {
        service: "Test Service".to_string(),
        bay: Some("Test Bay".to_string()),
    }];

    cache.set(bus_data).await;

    // Wait a bit to ensure expiration
    sleep(Duration::from_millis(10)).await;

    // Should be expired
    assert!(cache.is_expired().await);
    assert!(cache.get().await.is_none());
}

#[tokio::test]
async fn test_cache_empty_data() {
    let cache = BusCache::new(1);

    let empty_data = vec![];
    cache.set(empty_data).await;

    let cached_data = cache.get().await.expect("Cache should have empty data");
    assert!(cached_data.is_empty());
}

#[tokio::test]
async fn test_cache_overwrite() {
    let cache = BusCache::new(1);

    let initial_data = vec![BusStatus {
        service: "Initial".to_string(),
        bay: Some("Bay 1".to_string()),
    }];

    let new_data = vec![BusStatus {
        service: "Updated".to_string(),
        bay: Some("Bay 2".to_string()),
    }];

    cache.set(initial_data).await;
    cache.set(new_data.clone()).await;

    let cached_data = cache.get().await.expect("Cache should have data");
    assert_eq!(cached_data.len(), 1);
    assert_eq!(cached_data[0].service, "Updated");
}
