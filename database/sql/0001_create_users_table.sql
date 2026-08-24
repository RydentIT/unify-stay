-- 0001_create_users_table.sql
-- Core account record. Email is stored already lower-cased by the domain's Email value
-- object, so a plain unique index is enough to make "the same address" mean one row.
--
-- NOTE: every script in this folder must be idempotent. They are applied by DbUp (which
-- journals them in schemaversions) and are ALSO mounted into the postgres container's
-- docker-entrypoint-initdb.d for local compose runs, so a script can legitimately run
-- against a database where its objects already exist.

CREATE TABLE IF NOT EXISTS users (
    id                    uuid         PRIMARY KEY,
    email                 varchar(320) NOT NULL,
    password_hash         text         NULL,
    display_name          varchar(120) NOT NULL,
    status                smallint     NOT NULL DEFAULT 0,
    must_change_password  boolean      NOT NULL DEFAULT false,
    email_verified_at_utc timestamptz  NULL,
    created_at_utc        timestamptz  NOT NULL DEFAULT now(),
    updated_at_utc        timestamptz  NOT NULL DEFAULT now()
);

COMMENT ON COLUMN users.password_hash IS
    'NULL for accounts that only authenticate through an external provider.';
COMMENT ON COLUMN users.status IS
    '0 = PendingVerification, 1 = Active, 2 = Suspended, 3 = Deactivated.';
COMMENT ON COLUMN users.must_change_password IS
    'When true the user may only be issued a limited-scope (password-change-only) token.';

CREATE UNIQUE INDEX IF NOT EXISTS ux_users_email ON users (email);
CREATE INDEX IF NOT EXISTS ix_users_status ON users (status);
