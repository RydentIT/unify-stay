-- 0002_create_roles_table.sql
-- Role catalogue. Roles are a table rather than a C# enum column because Admin/Staff are
-- assigned operationally and the set is expected to grow; the seed below is the baseline.

CREATE TABLE IF NOT EXISTS roles (
    id   integer     PRIMARY KEY,
    name varchar(32) NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_roles_name ON roles (name);

-- Fixed ids so UserRoles rows stay meaningful across environments and so a re-seed cannot
-- silently renumber existing grants. ON CONFLICT DO NOTHING keeps this re-runnable.
INSERT INTO roles (id, name) VALUES
    (1, 'Student'),
    (2, 'PropertyOwner'),
    (3, 'Admin'),
    (4, 'Staff')
ON CONFLICT (id) DO NOTHING;
