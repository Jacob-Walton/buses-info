-- Create function to validate bay entries
-- Valid bays: {A,B,C}{1..17} and T{1,2}
CREATE OR REPLACE FUNCTION validate_bay(bay_name TEXT)
RETURNS BOOLEAN AS $$
BEGIN
    -- Check if bay_name is NULL or empty
    IF bay_name IS NULL OR trim(bay_name) = '' THEN
        RETURN FALSE;
    END IF;
    
    -- Normalize input (trim whitespace and convert to uppercase)
    bay_name := upper(trim(bay_name));
    
    -- Check for valid patterns:
    -- 1. {A,B,C}{1..17} pattern
    IF bay_name ~ '^[ABC]([1-9]|1[0-7])$' THEN
        RETURN TRUE;
    END IF;
    
    -- 2. T{1,2} pattern
    IF bay_name ~ '^T[12]$' THEN
        RETURN TRUE;
    END IF;
    
    -- If no pattern matches, return false
    RETURN FALSE;
END;
$$ LANGUAGE plpgsql IMMUTABLE;

-- Create a check constraint helper function for use in table constraints
CREATE OR REPLACE FUNCTION check_bay_constraint(bay_name TEXT)
RETURNS BOOLEAN AS $$
BEGIN
    IF NOT validate_bay(bay_name) THEN
        RAISE EXCEPTION 'Invalid bay name: %. Valid bays are A1-A17, B1-B17, C1-C17, T1, T2', bay_name;
    END IF;
    RETURN TRUE;
END;
$$ LANGUAGE plpgsql IMMUTABLE;

-- Add bay validation to buses table if it has a bay column
-- First check if bay column exists, if not add it
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'buses' AND column_name = 'bay'
    ) THEN
        ALTER TABLE buses ADD COLUMN bay VARCHAR(10);
    END IF;
END $$;

-- Add check constraint to bay column in buses table
ALTER TABLE buses DROP CONSTRAINT IF EXISTS chk_valid_bay;
ALTER TABLE buses ADD CONSTRAINT chk_valid_bay 
    CHECK (bay IS NULL OR validate_bay(bay));

-- Create an index on the bay column
CREATE INDEX IF NOT EXISTS idx_buses_bay ON buses(bay);

-- Add some helpful comments
COMMENT ON FUNCTION validate_bay(TEXT) IS 'Validates bus bay names. Returns true for valid bays: A1-A17, B1-B17, C1-C17, T1, T2';
COMMENT ON FUNCTION check_bay_constraint(TEXT) IS 'Check constraint helper that raises an exception for invalid bay names';