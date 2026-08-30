-- 0015_replace_upgrade_documents_with_structured_fields.sql
-- The Property Owner upgrade flow no longer collects a document upload; the applicant instead
-- fills in NIC, address, a second phone number and a description of the property. NIC and
-- Address are proof enough for a human admin to review manually - see the module notes.
--
-- verification_documents existed solely to back the old upload flow and has no other consumer,
-- so it is dropped outright rather than left as dead weight.

DROP TABLE IF EXISTS verification_documents;

ALTER TABLE role_upgrade_requests
    ADD COLUMN IF NOT EXISTS nic_number    varchar(20)  NOT NULL DEFAULT '',
    ADD COLUMN IF NOT EXISTS address       varchar(255) NOT NULL DEFAULT '',
    ADD COLUMN IF NOT EXISTS phone_number_2 varchar(20) NOT NULL DEFAULT '',
    ADD COLUMN IF NOT EXISTS property_info text        NOT NULL DEFAULT '';

ALTER TABLE role_upgrade_requests
    ALTER COLUMN nic_number DROP DEFAULT,
    ALTER COLUMN address DROP DEFAULT,
    ALTER COLUMN phone_number_2 DROP DEFAULT,
    ALTER COLUMN property_info DROP DEFAULT;

COMMENT ON COLUMN role_upgrade_requests.phone_number_2 IS
    'The applicant''s alternate/property contact number. Phone 1 is the account''s existing contact number from registration - it is not re-collected or stored here, only compared against at submission time.';
