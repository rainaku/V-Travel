-- Migration: Add ban tracking columns to users table
-- Tracks which admin banned the user and when

ALTER TABLE users ADD COLUMN IF NOT EXISTS banned_by TEXT DEFAULT '';
ALTER TABLE users ADD COLUMN IF NOT EXISTS banned_at TEXT DEFAULT '';

-- Add comment for documentation
COMMENT ON COLUMN users.banned_by IS 'Username of admin who banned this account';
COMMENT ON COLUMN users.banned_at IS 'ISO timestamp when the account was banned';
