use crate::application::dtos::{BusDto, BusesResponse};
use crate::domain::services::BusService;
use std::error::Error;
use std::sync::Arc;

pub type Result<T> = std::result::Result<T, Box<dyn Error + Send + Sync>>;

pub struct GetCurrentBuses {
    bus_service: Arc<BusService>,
}

impl GetCurrentBuses {
    pub fn new(bus_service: Arc<BusService>) -> Self {
        Self { bus_service }
    }

    pub async fn execute(&self, buses: Vec<BusDto>) -> Result<BusesResponse> {
        // Convert DTOs to entities
        let domain_buses: Vec<crate::domain::entities::Bus> =
            buses.into_iter().map(|dto| dto.into()).collect();

        // Validate bus data
        let validation_errors = self.bus_service.validate_bus_data(&domain_buses);
        if !validation_errors.is_empty() {
            tracing::warn!("Bus data validation errors: {:?}", validation_errors);
        }

        // Log bus arrivals
        if let Err(e) = self.bus_service.log_current_buses(&domain_buses).await {
            tracing::error!("Failed to log bus arrivals: {}", e);
        }

        // Convert back to DTOs for response
        let response_buses: Vec<BusDto> = domain_buses.into_iter().map(|bus| bus.into()).collect();

        Ok(BusesResponse {
            buses: response_buses,
            cached: false,
            last_updated: Some(chrono::Utc::now()),
        })
    }
}
