use crate::domain::entities::{Bay, Bus, BusArrival};
use crate::domain::repositories::BusRepository;
use std::error::Error;
use std::sync::Arc;

pub type Result<T> = std::result::Result<T, Box<dyn Error + Send + Sync>>;

pub struct BusService {
    repository: Arc<dyn BusRepository>,
}

impl BusService {
    pub fn new(repository: Arc<dyn BusRepository>) -> Self {
        Self { repository }
    }

    pub async fn log_current_buses(&self, buses: &[Bus]) -> Result<()> {
        let arrivals: Vec<BusArrival> = buses
            .iter()
            .filter_map(|bus| {
                bus.bay
                    .as_ref()
                    .map(|bay| BusArrival::new(bus.service.clone(), bay.name.clone()))
            })
            .collect();

        if arrivals.is_empty() {
            return Ok(());
        }

        self.repository.log_arrivals(&arrivals).await
    }

    pub fn validate_bus_data(&self, buses: &[Bus]) -> Vec<String> {
        let mut errors = Vec::new();

        for (index, bus) in buses.iter().enumerate() {
            if bus.service.trim().is_empty() {
                errors.push(format!("Bus at index {index} has empty service name"));
            }

            if let Some(bay) = &bus.bay {
                if bay.name.trim().is_empty() {
                    errors.push(format!("Bus {} has empty bay name", bus.service));
                } else if !self.is_valid_bay_name(&bay.name) {
                    // Only check format if not empty
                    errors.push(format!(
                        "Bus {} has invalid bay name '{}'",
                        bus.service, bay.name
                    ));
                }
            }
        }

        errors
    }

    fn is_valid_bay_name(&self, bay_name: &str) -> bool {
        if bay_name == "T1" || bay_name == "T2" {
            return true;
        }

        if bay_name.len() >= 2 {
            let first_char = bay_name.chars().next().unwrap();
            let number_part = &bay_name[1..];

            if matches!(first_char, 'A' | 'B' | 'C') && number_part.parse::<u32>().is_ok() {
                return true;
            }
        }

        false
    }

    pub fn convert_to_buses(&self, raw_buses: &[(String, Option<String>)]) -> Vec<Bus> {
        raw_buses
            .iter()
            .map(|(service, bay_name)| Bus {
                service: service.clone(),
                bay: bay_name.as_ref().map(|name| Bay::new(name.clone())),
            })
            .collect()
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_validate_bus_data() {
        let service = BusService::new(Arc::new(MockBusRepository));

        let valid_buses = vec![
            Bus {
                service: "101".to_string(),
                bay: Some(Bay::new("T1".to_string())),
            },
            Bus {
                service: "102".to_string(),
                bay: Some(Bay::new("A1".to_string())),
            },
            Bus {
                service: "103".to_string(),
                bay: None,
            },
        ];

        let errors = service.validate_bus_data(&valid_buses);
        assert!(errors.is_empty());

        let invalid_buses = vec![
            Bus {
                service: "".to_string(),
                bay: Some(Bay::new("T1".to_string())),
            },
            Bus {
                service: "102".to_string(),
                bay: Some(Bay::new("".to_string())),
            },
            Bus {
                service: "103".to_string(),
                bay: Some(Bay::new("X1".to_string())),
            },
        ];

        let errors = service.validate_bus_data(&invalid_buses);
        assert_eq!(errors.len(), 3);
    }

    #[test]
    fn test_is_valid_bay_name() {
        let service = BusService::new(Arc::new(MockBusRepository));

        assert!(service.is_valid_bay_name("T1"));
        assert!(service.is_valid_bay_name("T2"));
        assert!(service.is_valid_bay_name("A1"));
        assert!(service.is_valid_bay_name("B5"));
        assert!(service.is_valid_bay_name("C10"));

        assert!(!service.is_valid_bay_name("D1"));
        assert!(!service.is_valid_bay_name("T3"));
        assert!(!service.is_valid_bay_name("Ax"));
        assert!(!service.is_valid_bay_name(""));
    }

    // Mock repository for testing
    struct MockBusRepository;

    #[async_trait::async_trait]
    impl BusRepository for MockBusRepository {
        async fn log_arrival(&self, _arrival: &BusArrival) -> Result<()> {
            Ok(())
        }
        async fn log_arrivals(&self, _arrivals: &[BusArrival]) -> Result<()> {
            Ok(())
        }
        async fn get_arrivals_by_date_range(
            &self,
            _from: chrono::DateTime<chrono::Utc>,
            _to: chrono::DateTime<chrono::Utc>,
        ) -> Result<Vec<BusArrival>> {
            Ok(vec![])
        }
        async fn get_today_arrivals(&self) -> Result<Vec<BusArrival>> {
            Ok(vec![])
        }
    }
}
