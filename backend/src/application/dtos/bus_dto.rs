use serde::{Deserialize, Serialize};

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct BusDto {
    pub service: String,
    pub bay: Option<String>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct BusesResponse {
    pub buses: Vec<BusDto>,
    pub cached: bool,
    pub last_updated: Option<chrono::DateTime<chrono::Utc>>,
}

impl From<crate::domain::entities::Bus> for BusDto {
    fn from(bus: crate::domain::entities::Bus) -> Self {
        Self {
            service: bus.service,
            bay: bus.bay.map(|b| b.name),
        }
    }
}

impl From<BusDto> for crate::domain::entities::Bus {
    fn from(dto: BusDto) -> Self {
        Self {
            service: dto.service,
            bay: dto.bay.map(crate::domain::entities::Bay::new),
        }
    }
}
