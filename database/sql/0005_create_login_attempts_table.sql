-- 0005_create_login_attempts_table.sql
-- Feeds the lockout logic (LOG-005/LOG-006). user_id is nullable because an attempt against
-- an unknown address still has to be counted per IP - that is the case brute force relies on.

CREATE TABLE IF NOT EXISTS login_attempts (
    id           uuid        PRIMARY KEY,
    user_id      uuid        NULL REFERENCES users (id) ON DELETE SET NULL,
    email        varchar(320) NULL,
    ip_address   varchar(64) NULL,
    success      boolean     NOT NULL,
    attempted_at timestamptz NOT NULL DEFAULT now()
);

COMMENT ON COLUMN login_attempts.email IS
    'Address as typed. Retained even when user_id is null so per-address throttling works for unknown accounts.';

-- The two lockout queries are "recent failures for this account" and "recent failures from
-- this IP", both windowed by time - hence the composite ordering.
CREATE INDEX IF NOT EXISTS ix_login_attempts_user_attempted
    ON login_attempts (user_id, attempted_at DESC);
CREATE INDEX IF NOT EXISTS ix_login_attempts_ip_attempted
    ON login_attempts (ip_address, attempted_at DESC);
CREATE INDEX IF NOT EXISTS ix_login_attempts_email_attempted
    ON login_attempts (email, attempted_at DESC);
