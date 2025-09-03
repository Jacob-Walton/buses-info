-- Add composite index for service and timestamp
DROP INDEX IF EXISTS idx_bus_arrivals_service_date_unique;
CREATE INDEX IF NOT EXISTS idx_bus_arrivals_service_timestamp 
ON bus_arrivals (service, timestamp);