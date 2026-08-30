-- 0014_split_user_name_into_first_and_last.sql
-- Replaces the single free-text name with first_name/last_name so both can be collected and
-- displayed independently. Safe to do as a hard split rather than a phased migration: nothing
-- is deployed yet, so there is no existing "name" data that needs to be preserved by parsing.

ALTER TABLE users
    ADD COLUMN IF NOT EXISTS first_name varchar(60) NOT NULL DEFAULT '',
    ADD COLUMN IF NOT EXISTS last_name  varchar(60) NOT NULL DEFAULT '';

ALTER TABLE users
    ALTER COLUMN first_name DROP DEFAULT,
    ALTER COLUMN last_name DROP DEFAULT;

ALTER TABLE users DROP COLUMN IF EXISTS name;
