use chrono::{DateTime, Utc};
use serde::{Deserialize, Serialize};
use uuid::Uuid;

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
pub struct Bus {
    pub service: String,
    pub bay: Option<Bay>,
}

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq)]
pub struct Bay {
    pub name: String,
    pub points: u32,
}

impl Bay {
    pub fn new(name: String) -> Self {
        let points = Self::calculate_points(&name);
        Self { name, points }
    }

    fn calculate_points(bay_name: &str) -> u32 {
        match bay_name {
            "T1" | "T2" => 10,
            bay if bay.len() >= 2 => {
                let letter = bay.chars().next().unwrap();
                let number_str = &bay[1..];

                if let Ok(number) = number_str.parse::<u32>() {
                    match letter {
                        'A' | 'B' | 'C' => {
                            if number == 0 {
                                0
                            } else {
                                (10u32).saturating_sub(number)
                            }
                        }
                        _ => 0,
                    }
                } else {
                    0
                }
            }
            _ => 0,
        }
    }
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct BusArrival {
    pub id: Uuid,
    pub service: String,
    pub bay: Bay,
    pub timestamp: DateTime<Utc>,
}

impl BusArrival {
    pub fn new(service: String, bay_name: String) -> Self {
        Self {
            id: Uuid::new_v4(),
            service,
            bay: Bay::new(bay_name),
            timestamp: Utc::now(),
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_bay_points_calculation() {
        assert_eq!(Bay::new("T1".to_string()).points, 10);
        assert_eq!(Bay::new("T2".to_string()).points, 10);
        assert_eq!(Bay::new("A1".to_string()).points, 9);
        assert_eq!(Bay::new("A2".to_string()).points, 8);
        assert_eq!(Bay::new("B1".to_string()).points, 9);
        assert_eq!(Bay::new("C3".to_string()).points, 7);
        assert_eq!(Bay::new("A10".to_string()).points, 0);
        assert_eq!(Bay::new("D1".to_string()).points, 0);
        assert_eq!(Bay::new("".to_string()).points, 0);
    }
}
