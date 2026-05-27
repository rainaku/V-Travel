-- Migration: Add ban tracking columns to users table
-- Tracks which admin banned the user and when

ALTER TABLE users ADD COLUMN IF NOT EXISTS banned_by VARCHAR(100);
ALTER TABLE users ADD COLUMN IF NOT EXISTS banned_at TIMESTAMPTZ;

-- Add comment for documentation
COMMENT ON COLUMN users.banned_by IS 'Username of admin who banned this account';
COMMENT ON COLUMN users.banned_at IS 'Timestamp when the account was banned';
