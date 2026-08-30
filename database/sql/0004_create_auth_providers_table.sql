-- 0004_create_auth_providers_table.sql
-- One row per (user, provider). A single account can hold a local password and a linked
-- Google identity at the same time - that is how REG-004 account linking works.

CREATE TABLE IF NOT EXISTS auth_providers (
    id                         uuid         PRIMARY KEY,
    user_id                    uuid         NOT NULL REFERENCES users (id) ON DELETE CASCADE,
    provider                   varchar(16)  NOT NULL,
    provider_user_id           varchar(256) NOT NULL,
    email_verified_by_provider boolean      NOT NULL DEFAULT false,
    linked_at                  timestamptz  NOT NULL DEFAULT now(),
    CONSTRAINT ck_auth_providers_provider CHECK (provider IN ('local', 'google'))
);

COMMENT ON COLUMN auth_providers.provider_user_id IS
    'The provider stable subject claim. For local this is the user id.';
COMMENT ON COLUMN auth_providers.email_verified_by_provider IS
    'Google email_verified from the verified ID token. Drives REG-004 (link) vs REG-005 (reject).';

-- One external identity maps to at most one account.
CREATE UNIQUE INDEX IF NOT EXISTS ux_auth_providers_provider_subject
    ON auth_providers (provider, provider_user_id);

-- A user links a given provider at most once.
CREATE UNIQUE INDEX IF NOT EXISTS ux_auth_providers_user_provider
    ON auth_providers (user_id, provider);

CREATE INDEX IF NOT EXISTS ix_auth_providers_user_id ON auth_providers (user_id);
