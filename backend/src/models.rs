use chrono::{DateTime, Utc};
use serde::{Deserialize, Serialize};

#[derive(Debug, Clone, Serialize, Deserialize, Default)]
pub struct BusStatus {
    pub service: String,
    pub bay: Option<String>,
}

#[allow(dead_code)]
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct BusArrival {
    pub service: String,
    pub bay: String,
    pub timestamp: DateTime<Utc>,
    // Possible additions:
    // - Weather conditions
    // - Traffic conditions
}
