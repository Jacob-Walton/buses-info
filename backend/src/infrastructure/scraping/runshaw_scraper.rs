use crate::domain::entities::{Bay, Bus};
use scraper::{Html, Selector};
use std::error::Error;

pub type Result<T> = std::result::Result<T, Box<dyn Error + Send + Sync>>;

pub struct RunshawScraper {
    client: reqwest::Client,
}

impl RunshawScraper {
    pub fn new() -> Result<Self> {
        let client = reqwest::ClientBuilder::new()
            .user_agent("buses_api/0.2.0")
            .timeout(std::time::Duration::from_secs(30))
            .build()?;

        Ok(Self { client })
    }

    pub async fn scrape_buses(&self) -> Result<Vec<Bus>> {
        const MAX_RETRIES: u32 = 3;
        const RETRY_DELAY_MS: u64 = 1000;

        let mut last_error = None;

        for attempt in 1..=MAX_RETRIES {
            match self.scrape_attempt().await {
                Ok(result) => return Ok(result),
                Err(e) => {
                    last_error = Some(e);
                    if attempt < MAX_RETRIES {
                        tokio::time::sleep(std::time::Duration::from_millis(RETRY_DELAY_MS)).await;
                    }
                }
            }
        }

        Err(last_error.unwrap_or_else(|| "All scraping attempts failed".into()))
    }

    async fn scrape_attempt(&self) -> Result<Vec<Bus>> {
        let response = self
            .client
            .get("https://webservices.runshaw.ac.uk/bus/busdepartures.aspx")
            .send()
            .await?;

        if !response.status().is_success() {
            return Err(format!("HTTP error: {}", response.status()).into());
        }

        let text = response.text().await?;

        if text.trim().is_empty() {
            return Err("Received empty response".into());
        }

        self.parse_html(&text)
    }

    fn parse_html(&self, html: &str) -> Result<Vec<Bus>> {
        let document = Html::parse_document(html);
        let table_selector = Selector::parse("table").unwrap();
        let row_selector = Selector::parse("tr").unwrap();
        let cell_selector = Selector::parse("td").unwrap();

        let mut buses = Vec::new();

        for table in document.select(&table_selector) {
            for row in table.select(&row_selector) {
                let cells: Vec<String> = row
                    .select(&cell_selector)
                    .map(|cell| cell.text().collect::<String>().trim().to_string())
                    .filter(|text| !text.is_empty())
                    .collect();

                if cells.len() >= 2 {
                    let service = cells[0].clone();
                    let bay_name = cells[1].clone();

                    if !service.is_empty() && !bay_name.is_empty() {
                        buses.push(Bus {
                            service,
                            bay: Some(Bay::new(bay_name)),
                        });
                    }
                }
            }

            if !buses.is_empty() {
                break;
            }
        }

        Ok(buses)
    }

    pub fn generate_dummy_data() -> Vec<Bus> {
        vec![
            Bus {
                service: "101".to_string(),
                bay: Some(Bay::new("T1".to_string())),
            },
            Bus {
                service: "102".to_string(),
                bay: Some(Bay::new("T2".to_string())),
            },
            Bus {
                service: "103".to_string(),
                bay: Some(Bay::new("A1".to_string())),
            },
            Bus {
                service: "104".to_string(),
                bay: Some(Bay::new("B1".to_string())),
            },
            Bus {
                service: "105".to_string(),
                bay: Some(Bay::new("C1".to_string())),
            },
            Bus {
                service: "106".to_string(),
                bay: Some(Bay::new("A2".to_string())),
            },
            Bus {
                service: "107".to_string(),
                bay: Some(Bay::new("B2".to_string())),
            },
            Bus {
                service: "108".to_string(),
                bay: None,
            },
            Bus {
                service: "109".to_string(),
                bay: Some(Bay::new("A3".to_string())),
            },
            Bus {
                service: "110".to_string(),
                bay: None,
            },
        ]
    }
}
