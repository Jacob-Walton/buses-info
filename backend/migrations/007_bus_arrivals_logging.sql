-- Migration to add bus arrivals logging
CREATE TABLE IF NOT EXISTS bus_arrivals (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    service VARCHAR NOT NULL,
    bay VARCHAR NOT NULL,
    timestamp TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Create indexes
CREATE INDEX IF NOT EXISTS idx_bus_arrivals_bay ON bus_arrivals(bay);
CREATE INDEX IF NOT EXISTS idx_bus_arrivals_service ON bus_arrivals(service);
CREATE INDEX IF NOT EXISTS idx_bus_arrivals_timestamp ON bus_arrivals(timestamp DESC);

-- Function to calculate bay ranking points
CREATE OR REPLACE FUNCTION calculate_bay_points(bay_name VARCHAR)
RETURNS INTEGER AS $$
BEGIN
    -- T1, T2 = 10 points
    IF bay_name IN ('T1', 'T2') THEN
        RETURN 10;
    END IF;
    
    -- Parse bay format (letter + number)
    DECLARE
        bay_letter VARCHAR := SUBSTRING(bay_name FROM '^([A-Z])');
        bay_number INTEGER := SUBSTRING(bay_name FROM '(\d+)$')::INTEGER;
        base_points INTEGER;
    BEGIN
        -- A, B, C series starting at 9 points for x1, decreasing by 1 for each number
        CASE bay_letter
            WHEN 'A', 'B', 'C' THEN
                base_points := 10 - bay_number; -- 9 for x1, 8 for x2, etc.
            ELSE
                base_points := 0;
        END CASE;
        
        -- Minimum 0 points
        RETURN GREATEST(base_points, 0);
    END;
END;
$$ LANGUAGE plpgsql IMMUTABLE;