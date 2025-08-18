use crate::models::BusStatus;
use scraper::{Html, Selector};

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

fn parse_bus_data(html: &str) -> anyhow::Result<Vec<BusStatus>> {
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
