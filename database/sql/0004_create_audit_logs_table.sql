-- 0004_create_audit_logs_table.sql
-- Append-only security event log. actor_user_id is nullable because anonymous actions
-- (a failed login against an unknown address) still have to be recorded.
-- Deliberately NOT foreign-keyed to users: the trail must survive account deletion.
-- See the idempotency note in 0001.

CREATE TABLE IF NOT EXISTS audit_logs (
    id              uuid         PRIMARY KEY,
    action          varchar(128) NOT NULL,
    actor_user_id   uuid         NULL,
    subject_type    varchar(64)  NULL,
    subject_id      varchar(128) NULL,
    ip_address      inet         NULL,
    user_agent      text         NULL,
    metadata        jsonb        NULL,
    occurred_at_utc timestamptz  NOT NULL DEFAULT now()
);

COMMENT ON COLUMN audit_logs.action IS
    'Dotted event name, e.g. "user.register.succeeded".';

CREATE INDEX IF NOT EXISTS ix_audit_logs_occurred_at ON audit_logs (occurred_at_utc DESC);
CREATE INDEX IF NOT EXISTS ix_audit_logs_actor ON audit_logs (actor_user_id, occurred_at_utc DESC);
CREATE INDEX IF NOT EXISTS ix_audit_logs_action ON audit_logs (action, occurred_at_utc DESC);
