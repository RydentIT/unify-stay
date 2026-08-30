-- 0013_add_must_complete_profile_to_users.sql
-- Google-registered accounts cannot supply a phone number through the ID token, so they land
-- with this flag set until they complete it (mirrors must_change_password's gate/redirect
-- pattern). Email-registered accounts collect the phone number at registration and never set it.

ALTER TABLE users
    ADD COLUMN IF NOT EXISTS must_complete_profile boolean NOT NULL DEFAULT false;

COMMENT ON COLUMN users.must_complete_profile IS
    'While true the user may only hold a limited-scope token (token_type=profile_completion_required) that unlocks nothing except the complete-profile endpoint.';
