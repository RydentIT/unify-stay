-- 0016_add_suspended_status_to_users.sql
-- Adds 'suspended' as a status value distinct from 'deactivated' and 'deleted', even though all
-- three currently block ordinary account use the same way - kept distinct for audit/reporting
-- clarity, and because only 'suspended' is admin-imposed rather than self-service or terminal.
--
-- 'Suspended' = 'banned' for this project (product decision): a suspended user is rejected at
-- login outright, same as LOG-009's existing rejection for a terminal account. See migration
-- 0017 for the user_suspensions table that records who suspended whom, when, and why.

ALTER TABLE users DROP CONSTRAINT IF EXISTS ck_users_status;

ALTER TABLE users ADD CONSTRAINT ck_users_status
    CHECK (status IN ('active', 'deactivated', 'deleted', 'suspended'));

COMMENT ON COLUMN users.status IS
    'active | deactivated | deleted | suspended. Deactivated accounts self-reactivate by logging in; deleted and suspended accounts cannot log in at all.';

-- Supports the admin user directory's search-by-name/email (Part 3): partial, case-insensitive
-- matching. Plain btree with a leading wildcard-friendly ILIKE is adequate at MVP scale; a
-- trigram/GIN index is not worth the extra extension dependency until search volume says otherwise.
CREATE INDEX IF NOT EXISTS ix_users_first_name ON users (first_name);
CREATE INDEX IF NOT EXISTS ix_users_last_name ON users (last_name);

-- users.email already has ux_users_email (migration 0001) and users.status already has
-- ix_users_status - both already support the directory's email and status filters.
-- user_roles is already joinable on (user_id, role_id) via its primary key, so filtering the
-- directory by role needs no new index.
