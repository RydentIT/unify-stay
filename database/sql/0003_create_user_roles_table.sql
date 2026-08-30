-- 0003_create_user_roles_table.sql
-- Role grants. A user may hold more than one role (e.g. Student who became PropertyOwner).

CREATE TABLE IF NOT EXISTS user_roles (
    user_id    uuid        NOT NULL REFERENCES users (id) ON DELETE CASCADE,
    role_id    integer     NOT NULL REFERENCES roles (id) ON DELETE RESTRICT,
    granted_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (user_id, role_id)
);

CREATE INDEX IF NOT EXISTS ix_user_roles_user_id ON user_roles (user_id);
CREATE INDEX IF NOT EXISTS ix_user_roles_role_id ON user_roles (role_id);
