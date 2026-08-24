-- 0003_create_user_roles_table.sql
-- Role grants, persisted by NAME rather than by enum ordinal: reordering RoleName in C#
-- must never silently re-grant permissions. See the idempotency note in 0001.

CREATE TABLE IF NOT EXISTS user_roles (
    id             uuid        PRIMARY KEY,
    user_id        uuid        NOT NULL REFERENCES users (id) ON DELETE CASCADE,
    role           varchar(32) NOT NULL,
    granted_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_user_roles_role
        CHECK (role IN ('Guest', 'Host', 'Support', 'Admin'))
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_user_roles_user_role ON user_roles (user_id, role);
CREATE INDEX IF NOT EXISTS ix_user_roles_user_id ON user_roles (user_id);
