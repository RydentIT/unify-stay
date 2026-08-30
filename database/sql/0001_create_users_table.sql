-- 0001_create_users_table.sql
-- Core account record.
--
-- CONVENTIONS FOR EVERY SCRIPT IN THIS FOLDER:
--   * Idempotent (IF NOT EXISTS / guarded DO blocks) so a re-run is harmless.
--   * Enum-like columns are varchar + CHECK rather than native Postgres ENUM types.
--     Native enums cannot have values removed and need ALTER TYPE ceremony to extend,
--     and they map awkwardly through Dapper. The C# enum is the source of truth; the
--     CHECK constraint is the database's guarantee that nothing else gets in.
--   * Timestamps are timestamptz, always stored UTC.

CREATE TABLE IF NOT EXISTS users (
    id                   uuid         PRIMARY KEY,
    name                 varchar(120) NOT NULL,
    email                varchar(320) NOT NULL,
    password_hash        text         NULL,
    email_verified       boolean      NOT NULL DEFAULT false,
    must_change_password boolean      NOT NULL DEFAULT false,
    status               varchar(16)  NOT NULL DEFAULT 'active',
    avatar_url           text         NULL,
    contact_number       varchar(32)  NULL,
    pending_email        varchar(320) NULL,
    created_at           timestamptz  NOT NULL DEFAULT now(),
    updated_at           timestamptz  NOT NULL DEFAULT now(),
    CONSTRAINT ck_users_status CHECK (status IN ('active', 'deactivated', 'deleted'))
);

COMMENT ON COLUMN users.password_hash IS
    'NULL for Google-only accounts. A missing hash must never be treated as "any password matches"; password flows must reject these accounts explicitly.';
COMMENT ON COLUMN users.must_change_password IS
    'While true the user may only hold a limited-scope token that unlocks nothing except the change-password endpoint.';
COMMENT ON COLUMN users.pending_email IS
    'Mirror of the address in pending_email_changes. users.email stays authoritative until the new address is verified.';
COMMENT ON COLUMN users.status IS
    'active | deactivated | deleted. Deactivated accounts self-reactivate by logging in; deleted accounts cannot.';

-- Email is lower-cased by the domain Email value object, so a plain unique index is enough
-- to make "the same address" mean exactly one row.
CREATE UNIQUE INDEX IF NOT EXISTS ux_users_email ON users (email);
CREATE INDEX IF NOT EXISTS ix_users_status ON users (status);
