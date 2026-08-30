-- 0017_create_user_suspensions_table.sql
-- Records of admin-imposed suspensions (bans). One row per suspend action; a lifted suspension
-- is not deleted, it is marked LIFTED, so the history survives (mirrors audit_logs' own
-- retain-everything approach). Whether a user is CURRENTLY suspended is users.status = 'suspended';
-- this table is the "why, by whom, and when" detail behind that status, not a second source of
-- truth for it.

CREATE TABLE IF NOT EXISTS user_suspensions (
    id            uuid         PRIMARY KEY,
    user_id       uuid         NOT NULL REFERENCES users (id) ON DELETE CASCADE,
    reason        varchar(255) NOT NULL,
    suspended_by  uuid         NOT NULL REFERENCES users (id) ON DELETE RESTRICT,
    suspended_at  timestamptz  NOT NULL DEFAULT now(),
    status        varchar(16)  NOT NULL DEFAULT 'active',
    lifted_by     uuid         NULL REFERENCES users (id) ON DELETE SET NULL,
    lifted_at     timestamptz  NULL,
    CONSTRAINT ck_user_suspensions_status CHECK (status IN ('active', 'lifted')),
    CONSTRAINT ck_user_suspensions_lift_fields
        CHECK (status = 'active' OR (lifted_by IS NOT NULL AND lifted_at IS NOT NULL))
);

-- At most one ACTIVE suspension per user at a time - mirrors the one-active-request pattern in
-- migration 0010. Also the index the "does this user have an active suspension" lookup needs.
CREATE UNIQUE INDEX IF NOT EXISTS ux_user_suspensions_one_active
    ON user_suspensions (user_id)
    WHERE status = 'active';

CREATE INDEX IF NOT EXISTS ix_user_suspensions_user_status ON user_suspensions (user_id, status);
