use crate::scraper::parse_bus_data;

#[tokio::test]
async fn test_parse_valid_html() {
    let html = r#"
        <html>
            <body>
                <table>
                    <tr>
                        <td>Service 1</td>
                        <td>Bay A</td>
                    </tr>
                    <tr>
                        <td>Service 2</td>
                        <td>Bay B</td>
                    </tr>
                </table>
            </body>
        </html>
    "#;

    let result = parse_bus_data(html).expect("Failed to parse HTML");

    assert_eq!(result.len(), 2);
    assert_eq!(result[0].service, "Service 1");
    assert_eq!(result[0].bay, Some("Bay A".to_string()));
    assert_eq!(result[1].service, "Service 2");
    assert_eq!(result[1].bay, Some("Bay B".to_string()));
}

#[tokio::test]
async fn test_parse_empty_html() {
    let html = "<html><body></body></html>";
    let result = parse_bus_data(html).expect("Failed to parse HTML");
    assert!(result.is_empty());
}

#[tokio::test]
async fn test_parse_table_with_empty_cells() {
    let html = r#"
        <html>
            <body>
                <table>
                    <tr>
                        <td></td>
                        <td></td>
                    </tr>
                    <tr>
                        <td>Valid Service</td>
                        <td>Valid Bay</td>
                    </tr>
                </table>
            </body>
        </html>
    "#;

    let result = parse_bus_data(html).expect("Failed to parse HTML");

    // Should only include the valid row
    assert_eq!(result.len(), 1);
    assert_eq!(result[0].service, "Valid Service");
    assert_eq!(result[0].bay, Some("Valid Bay".to_string()));
}

#[tokio::test]
async fn test_parse_table_with_insufficient_cells() {
    let html = r#"
        <html>
            <body>
                <table>
                    <tr>
                        <td>Only One Cell</td>
                    </tr>
                    <tr>
                        <td>Service</td>
                        <td>Bay</td>
                    </tr>
                </table>
            </body>
        </html>
    "#;

    let result = parse_bus_data(html).expect("Failed to parse HTML");

    // Should only include the row with enough cells
    assert_eq!(result.len(), 1);
    assert_eq!(result[0].service, "Service");
    assert_eq!(result[0].bay, Some("Bay".to_string()));
}

#[tokio::test]
async fn test_parse_multiple_tables() {
    let html = r#"
        <html>
            <body>
                <table>
                    <tr>
                        <td>Service 1</td>
                        <td>Bay 1</td>
                    </tr>
                </table>
                <table>
                    <tr>
                        <td>Service 2</td>
                        <td>Bay 2</td>
                    </tr>
                </table>
            </body>
        </html>
    "#;

    let result = parse_bus_data(html).expect("Failed to parse HTML");

    // Should only process the first table with data
    assert_eq!(result.len(), 1);
    assert_eq!(result[0].service, "Service 1");
}

#[tokio::test]
async fn test_parse_malformed_html() {
    let html = "<html><table><tr><td>Service</td><td>Bay</td></tr></table>";
    let result = parse_bus_data(html);
    assert!(result.is_ok());
}
