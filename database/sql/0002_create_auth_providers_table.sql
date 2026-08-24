-- 0002_create_auth_providers_table.sql
-- One row per (user, provider) pair so a single account can hold a local password and any
-- number of federated logins. See the idempotency note in 0001.

CREATE TABLE IF NOT EXISTS auth_providers (
    id               uuid         PRIMARY KEY,
    user_id          uuid         NOT NULL REFERENCES users (id) ON DELETE CASCADE,
    kind             smallint     NOT NULL,
    provider_subject varchar(256) NOT NULL,
    linked_at_utc    timestamptz  NOT NULL DEFAULT now()
);

COMMENT ON COLUMN auth_providers.kind IS
    '0 = Local, 1 = Google, 2 = Facebook, 3 = Apple.';
COMMENT ON COLUMN auth_providers.provider_subject IS
    'The provider''s stable identifier for the user (the OIDC "sub"). For Local this is the user id.';

-- A given external identity may map to at most one account.
CREATE UNIQUE INDEX IF NOT EXISTS ux_auth_providers_kind_subject
    ON auth_providers (kind, provider_subject);

-- A user may link a given provider only once.
CREATE UNIQUE INDEX IF NOT EXISTS ux_auth_providers_user_kind
    ON auth_providers (user_id, kind);

CREATE INDEX IF NOT EXISTS ix_auth_providers_user_id ON auth_providers (user_id);
