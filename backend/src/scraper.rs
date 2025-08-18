use crate::models::BusStatus;
use scraper::{Html, Selector};

/// Generate dummy bus data for development/debug mode
pub fn generate_dummy_bus_data() -> Vec<BusStatus> {
    vec![
        BusStatus {
            service: "101".to_string(),
            bay: Some("T1".to_string()),
        },
        BusStatus {
            service: "102".to_string(),
            bay: Some("T2".to_string()),
        },
        BusStatus {
            service: "103".to_string(),
            bay: Some("A1".to_string()),
        },
        BusStatus {
            service: "104".to_string(),
            bay: Some("B1".to_string()),
        },
        BusStatus {
            service: "105".to_string(),
            bay: Some("C1".to_string()),
        },
        BusStatus {
            service: "106".to_string(),
            bay: Some("A2".to_string()),
        },
        BusStatus {
            service: "107".to_string(),
            bay: Some("B2".to_string()),
        },
        BusStatus {
            service: "108".to_string(),
            bay: None,
        }, // Not arrived
        BusStatus {
            service: "109".to_string(),
            bay: Some("A3".to_string()),
        },
        BusStatus {
            service: "110".to_string(),
            bay: None,
        }, // Not arrived
        BusStatus {
            service: "111".to_string(),
            bay: Some("C3".to_string()),
        },
        BusStatus {
            service: "112".to_string(),
            bay: Some("A4".to_string()),
        },
        BusStatus {
            service: "113".to_string(),
            bay: Some("B4".to_string()),
        },
        BusStatus {
            service: "114".to_string(),
            bay: Some("C4".to_string()),
        },
        BusStatus {
            service: "115".to_string(),
            bay: None,
        }, // Not arrived
        BusStatus {
            service: "116".to_string(),
            bay: Some("B5".to_string()),
        },
        BusStatus {
            service: "117".to_string(),
            bay: Some("C5".to_string()),
        },
        BusStatus {
            service: "118".to_string(),
            bay: Some("A6".to_string()),
        },
        BusStatus {
            service: "119".to_string(),
            bay: None,
        }, // Not arrived
        BusStatus {
            service: "120".to_string(),
            bay: Some("C6".to_string()),
        },
        BusStatus {
            service: "121".to_string(),
            bay: Some("A7".to_string()),
        },
        BusStatus {
            service: "122".to_string(),
            bay: Some("B7".to_string()),
        },
        BusStatus {
            service: "123".to_string(),
            bay: None,
        }, // Not arrived
        BusStatus {
            service: "124".to_string(),
            bay: Some("A8".to_string()),
        },
        BusStatus {
            service: "125".to_string(),
            bay: Some("B8".to_string()),
        },
    ]
}

pub async fn scrape_bus_information() -> anyhow::Result<Vec<BusStatus>> {
    const MAX_RETRIES: u32 = 3;
    const RETRY_DELAY_MS: u64 = 1000;
    const REQUEST_TIMEOUT_SECS: u64 = 30;

    let client = reqwest::ClientBuilder::new()
        .user_agent("buses_api/1.0")
        .timeout(std::time::Duration::from_secs(REQUEST_TIMEOUT_SECS))
        .build()
        .map_err(|e| anyhow::anyhow!("Failed to build HTTP client: {}", e))?;

    let mut last_error = None;

    for attempt in 1..=MAX_RETRIES {
        match scrape_attempt(&client).await {
            Ok(result) => return Ok(result),
            Err(e) => {
                last_error = Some(e);
                if attempt < MAX_RETRIES {
                    tokio::time::sleep(std::time::Duration::from_millis(RETRY_DELAY_MS)).await;
                }
            }
        }
    }

    Err(last_error.unwrap_or_else(|| anyhow::anyhow!("All scraping attempts failed")))
}

async fn scrape_attempt(client: &reqwest::Client) -> anyhow::Result<Vec<BusStatus>> {
    let response = client
        .get("https://webservices.runshaw.ac.uk/bus/busdepartures.aspx")
        .send()
        .await
        .map_err(|e| anyhow::anyhow!("Network request failed: {}", e))?;

    if !response.status().is_success() {
        return Err(anyhow::anyhow!("HTTP error: {}", response.status()));
    }

    let text = response
        .text()
        .await
        .map_err(|e| anyhow::anyhow!("Failed to read response body: {}", e))?;

    if text.trim().is_empty() {
        return Err(anyhow::anyhow!("Received empty response"));
    }

    parse_bus_data(&text)
}

pub fn parse_bus_data(html: &str) -> anyhow::Result<Vec<BusStatus>> {
    let document = Html::parse_document(html);
    let table_selector = Selector::parse("table").unwrap();
    let row_selector = Selector::parse("tr").unwrap();
    let cell_selector = Selector::parse("td").unwrap();

    let mut bus_statuses = Vec::new();

    for table in document.select(&table_selector) {
        for row in table.select(&row_selector) {
            let cells: Vec<String> = row
                .select(&cell_selector)
                .map(|cell| cell.text().collect::<String>().trim().to_string())
                .filter(|text| !text.is_empty())
                .collect();

            if cells.len() >= 2 {
                let service = cells[0].clone();
                let bay = cells[1].clone();

                if !service.is_empty() && !bay.is_empty() {
                    bus_statuses.push(BusStatus {
                        service,
                        bay: Some(bay),
                    });
                }
            }
        }

        if !bus_statuses.is_empty() {
            break;
        }
    }

    Ok(bus_statuses)
}
