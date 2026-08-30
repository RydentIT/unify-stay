-- 0012_create_audit_logs_table.sql
-- Append-only security/action trail.
--
-- Deliberately NOT foreign-keyed to users: BR-SET-004 requires audit rows to survive account
-- deletion, so the trail keeps the email/provider it captured at the time instead.

CREATE TABLE IF NOT EXISTS audit_logs (
    id             uuid         PRIMARY KEY,
    user_id        uuid         NULL,
    email          varchar(320) NULL,
    provider       varchar(16)  NULL,
    ip_address     varchar(64)  NULL,
    action_type    varchar(64)  NOT NULL,
    fields_changed text         NULL,
    occurred_at    timestamptz  NOT NULL DEFAULT now()
);

COMMENT ON COLUMN audit_logs.action_type IS
    'Dotted event name, e.g. user.register.succeeded, user.login.locked, profile.email.change_requested.';
COMMENT ON COLUMN audit_logs.fields_changed IS
    'JSON payload naming what changed. Text rather than jsonb: this is written on every action and never queried structurally.';

CREATE INDEX IF NOT EXISTS ix_audit_logs_occurred ON audit_logs (occurred_at DESC);
CREATE INDEX IF NOT EXISTS ix_audit_logs_user ON audit_logs (user_id, occurred_at DESC);
CREATE INDEX IF NOT EXISTS ix_audit_logs_action ON audit_logs (action_type, occurred_at DESC);
CREATE INDEX IF NOT EXISTS ix_audit_logs_email ON audit_logs (email, occurred_at DESC);
