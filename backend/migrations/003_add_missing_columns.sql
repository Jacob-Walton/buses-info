-- Add last_login column to users table
ALTER TABLE users ADD COLUMN IF NOT EXISTS last_login TIMESTAMP WITH TIME ZONE;

-- Create index for last_login
CREATE INDEX IF NOT EXISTS idx_users_last_login ON users(last_login);

-- Add comment
COMMENT ON COLUMN users.last_login IS 'Timestamp of user last login for analytics and user data export';
