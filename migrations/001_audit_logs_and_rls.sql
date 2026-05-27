-- ==========================================================
-- MIGRATION 001: Audit Logs + Row-Level Security + Constraints
-- ==========================================================

-- 1. AUDIT LOGS TABLE (append-only)
CREATE TABLE IF NOT EXISTS audit_logs (
    id BIGSERIAL PRIMARY KEY,
    user_id INTEGER NOT NULL DEFAULT 0,
    action VARCHAR(100) NOT NULL,
    entity_type VARCHAR(100) NOT NULL,
    entity_id INTEGER NOT NULL,
    old_value TEXT NOT NULL DEFAULT '',
    new_value TEXT NOT NULL DEFAULT '',
    details TEXT NOT NULL DEFAULT '',
    created_at TIMESTAMP NOT NULL DEFAULT NOW()
);

-- Index for querying by entity
CREATE INDEX IF NOT EXISTS ix_audit_logs_entity
ON audit_logs (entity_type, entity_id, created_at DESC);

-- Index for querying by user
CREATE INDEX IF NOT EXISTS ix_audit_logs_user
ON audit_logs (user_id, created_at DESC);

-- Index for querying by action
CREATE INDEX IF NOT EXISTS ix_audit_logs_action
ON audit_logs (action, created_at DESC);

-- RPC function to insert audit log (used by application)
CREATE OR REPLACE FUNCTION insert_audit_log(
    p_user_id INTEGER,
    p_action VARCHAR(100),
    p_entity_type VARCHAR(100),
    p_entity_id INTEGER,
    p_old_value TEXT DEFAULT '',
    p_new_value TEXT DEFAULT '',
    p_details TEXT DEFAULT ''
)
RETURNS void
LANGUAGE plpgsql
SECURITY DEFINER
AS $$
BEGIN
    INSERT INTO audit_logs (user_id, action, entity_type, entity_id, old_value, new_value, details)
    VALUES (p_user_id, p_action, p_entity_type, p_entity_id, p_old_value, p_new_value, p_details);
END;
$$;

-- 2. PAYMENT AMOUNT CONSTRAINTS
-- Ensure paid_amount never exceeds total_amount and amounts are non-negative
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'chk_payments_total_positive') THEN
        ALTER TABLE payments ADD CONSTRAINT chk_payments_total_positive CHECK (total_amount >= 0);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'chk_payments_paid_non_negative') THEN
        ALTER TABLE payments ADD CONSTRAINT chk_payments_paid_non_negative CHECK (paid_amount >= 0);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'chk_payments_paid_lte_total') THEN
        ALTER TABLE payments ADD CONSTRAINT chk_payments_paid_lte_total CHECK (paid_amount <= total_amount);
    END IF;
END $$;

-- 3. BOOKING GUEST COUNT CONSTRAINTS
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'chk_bookings_guest_count_positive') THEN
        ALTER TABLE bookings ADD CONSTRAINT chk_bookings_guest_count_positive CHECK (guest_count > 0);
    END IF;
END $$;

-- 4. DEPARTURE SLOT CONSTRAINTS
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'chk_departures_available_slots_non_negative') THEN
        ALTER TABLE departures ADD CONSTRAINT chk_departures_available_slots_non_negative CHECK (available_slots >= 0);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'chk_departures_available_lte_max') THEN
        ALTER TABLE departures ADD CONSTRAINT chk_departures_available_lte_max CHECK (available_slots <= max_slots);
    END IF;
END $$;

-- 5. ROW-LEVEL SECURITY POLICIES
-- Enable RLS on sensitive tables
ALTER TABLE bookings ENABLE ROW LEVEL SECURITY;
ALTER TABLE payments ENABLE ROW LEVEL SECURITY;
ALTER TABLE customers ENABLE ROW LEVEL SECURITY;
ALTER TABLE audit_logs ENABLE ROW LEVEL SECURITY;

-- Policy: audit_logs is append-only (no UPDATE/DELETE via API)
DROP POLICY IF EXISTS audit_logs_insert_only ON audit_logs;
CREATE POLICY audit_logs_insert_only ON audit_logs
    FOR INSERT
    WITH CHECK (true);

DROP POLICY IF EXISTS audit_logs_select_admin ON audit_logs;
CREATE POLICY audit_logs_select_admin ON audit_logs
    FOR SELECT
    USING (true);  -- Admin app can read all logs

-- Note: For Supabase with anon key (desktop app), RLS policies need to be
-- configured based on your auth strategy. The policies above are a starting point.
-- In production, use Supabase Auth with JWT claims for proper user-level RLS.

-- 6. PASSWORD HASH COLUMN SIZE (BCrypt hashes are ~60 chars, but allow room)
ALTER TABLE users
ALTER COLUMN password_hash TYPE VARCHAR(255);

-- 7. REVOKE direct DELETE on audit_logs for the anon role (defense in depth)
-- Uncomment if using Supabase with proper role separation:
-- REVOKE DELETE ON audit_logs FROM anon;
-- REVOKE UPDATE ON audit_logs FROM anon;
