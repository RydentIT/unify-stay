-- 0008_create_email_verification_tokens_table.sql
-- Single-use verification tokens. Issuing a new one invalidates any earlier unexpired token
-- (EVR-005/BR-EVR-002), which the repository does by stamping used_at on the outstanding rows.

CREATE TABLE IF NOT EXISTS email_verification_tokens (
    id         uuid        PRIMARY KEY,
    user_id    uuid        NOT NULL REFERENCES users (id) ON DELETE CASCADE,
    token_hash text        NOT NULL,
    expires_at timestamptz NOT NULL,
    used_at    timestamptz NULL,
    created_at timestamptz NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_email_verification_tokens_hash ON email_verification_tokens (token_hash);
CREATE INDEX IF NOT EXISTS ix_email_verification_tokens_user ON email_verification_tokens (user_id, used_at, expires_at);
