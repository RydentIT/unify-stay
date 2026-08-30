-- 0010_create_role_upgrade_requests_table.sql
-- Student -> PropertyOwner upgrade applications.

CREATE TABLE IF NOT EXISTS role_upgrade_requests (
    id               uuid        PRIMARY KEY,
    user_id          uuid        NOT NULL REFERENCES users (id) ON DELETE CASCADE,
    status           varchar(16) NOT NULL DEFAULT 'pending',
    rejection_reason text        NULL,
    submitted_at     timestamptz NOT NULL DEFAULT now(),
    decided_at       timestamptz NULL,
    decided_by       uuid        NULL REFERENCES users (id) ON DELETE SET NULL,
    CONSTRAINT ck_role_upgrade_requests_status
        CHECK (status IN ('pending', 'approved', 'rejected')),
    -- SET-007: a rejection is not a rejection without a reason.
    CONSTRAINT ck_role_upgrade_requests_rejection_reason
        CHECK (status <> 'rejected' OR rejection_reason IS NOT NULL)
);

-- BR-SET-001: at most one active (pending or approved) request per user. Enforced in the
-- database as well as the handler, because a double-submit race would otherwise slip through.
CREATE UNIQUE INDEX IF NOT EXISTS ux_role_upgrade_requests_one_active
    ON role_upgrade_requests (user_id)
    WHERE status IN ('pending', 'approved');

CREATE INDEX IF NOT EXISTS ix_role_upgrade_requests_status ON role_upgrade_requests (status, submitted_at DESC);
