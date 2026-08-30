-- 0007_create_password_reset_tokens_table.sql
-- Single-use, time-boxed reset tokens (FPW-004/FPW-005). Only the hash is stored, so a
-- database leak does not hand out working reset links.

CREATE TABLE IF NOT EXISTS password_reset_tokens (
    id         uuid        PRIMARY KEY,
    user_id    uuid        NOT NULL REFERENCES users (id) ON DELETE CASCADE,
    token_hash text        NOT NULL,
    expires_at timestamptz NOT NULL,
    used_at    timestamptz NULL,
    created_at timestamptz NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_password_reset_tokens_hash ON password_reset_tokens (token_hash);
CREATE INDEX IF NOT EXISTS ix_password_reset_tokens_user ON password_reset_tokens (user_id, used_at, expires_at);
