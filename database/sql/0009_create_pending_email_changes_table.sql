-- 0009_create_pending_email_changes_table.sql
-- Holds a requested new address until it is verified (PRF-009/BR-PRF-002). users.email stays
-- authoritative the whole time, so losing access to the new inbox never locks anyone out.

CREATE TABLE IF NOT EXISTS pending_email_changes (
    id         uuid         PRIMARY KEY,
    user_id    uuid         NOT NULL REFERENCES users (id) ON DELETE CASCADE,
    new_email  varchar(320) NOT NULL,
    token_hash text         NOT NULL,
    expires_at timestamptz  NOT NULL,
    used_at    timestamptz  NULL,
    created_at timestamptz  NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_pending_email_changes_hash ON pending_email_changes (token_hash);
CREATE INDEX IF NOT EXISTS ix_pending_email_changes_user ON pending_email_changes (user_id, used_at, expires_at);
