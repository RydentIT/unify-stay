-- 0006_create_sessions_table.sql
-- Server-side refresh sessions. Logout and "invalidate all sessions" are real operations
-- against this table (BR-LOG-006): deleting a token client-side is never sufficient.

CREATE TABLE IF NOT EXISTS sessions (
    id                 uuid        PRIMARY KEY,
    user_id            uuid        NOT NULL REFERENCES users (id) ON DELETE CASCADE,
    refresh_token_hash text        NOT NULL,
    role_claim         varchar(32) NULL,
    expires_at         timestamptz NOT NULL,
    revoked_at         timestamptz NULL,
    created_at         timestamptz NOT NULL DEFAULT now()
);

COMMENT ON COLUMN sessions.refresh_token_hash IS
    'SHA-256 of the refresh token. The raw token is returned to the client once and never stored.';
COMMENT ON COLUMN sessions.role_claim IS
    'Role captured at issue time, so a revoked/expired session cannot silently carry stale privileges.';

CREATE UNIQUE INDEX IF NOT EXISTS ux_sessions_refresh_token_hash ON sessions (refresh_token_hash);
CREATE INDEX IF NOT EXISTS ix_sessions_user_active ON sessions (user_id, revoked_at, expires_at);
