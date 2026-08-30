-- 0011_create_verification_documents_table.sql
-- Documents attached to an upgrade request. Only the storage path is kept here; the bytes
-- live behind IFileStorageService so swapping local disk for S3/Blob is an implementation change.

CREATE TABLE IF NOT EXISTS verification_documents (
    id                uuid         PRIMARY KEY,
    upgrade_request_id uuid        NOT NULL REFERENCES role_upgrade_requests (id) ON DELETE CASCADE,
    file_path         text         NOT NULL,
    file_type         varchar(128) NOT NULL,
    original_file_name varchar(260) NULL,
    file_size_bytes   bigint       NULL,
    uploaded_at       timestamptz  NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_verification_documents_request
    ON verification_documents (upgrade_request_id);
