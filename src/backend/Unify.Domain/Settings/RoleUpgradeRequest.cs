using Unify.Domain.Common;

namespace Unify.Domain.Settings;

/// <summary>
/// A Student's application to become a Property Owner.
///
/// Structured fields only - no document upload. NIC and Phone2 shape (Sri Lankan formats) are
/// FluentValidation's job in Application; PhoneNumber2 differing from the account's existing
/// contact number is checked in the handler, since it needs the loaded User to compare against.
///
/// The transitions here are the business rules: a decision can only be made once, a rejection
/// is not valid without a reason (SET-007), and a resubmission is only open to someone whose
/// previous attempt was rejected (SET-008).
/// </summary>
public sealed class RoleUpgradeRequest : Entity
{
    public RoleUpgradeRequest(
        Guid id,
        Guid userId,
        string nicNumber,
        string address,
        string phoneNumber2,
        string propertyInfo,
        UpgradeRequestStatus status,
        DateTimeOffset submittedAt,
        string? rejectionReason = null,
        DateTimeOffset? decidedAt = null,
        Guid? decidedBy = null)
        : base(id)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("An upgrade request must belong to a user.");
        }

        if (string.IsNullOrWhiteSpace(nicNumber))
        {
            throw new DomainException("An upgrade request requires an NIC number.");
        }

        if (string.IsNullOrWhiteSpace(address))
        {
            throw new DomainException("An upgrade request requires an address.");
        }

        if (string.IsNullOrWhiteSpace(phoneNumber2))
        {
            throw new DomainException("An upgrade request requires a second phone number.");
        }

        if (string.IsNullOrWhiteSpace(propertyInfo))
        {
            throw new DomainException("An upgrade request requires property information.");
        }

        if (status == UpgradeRequestStatus.Rejected && string.IsNullOrWhiteSpace(rejectionReason))
        {
            throw new DomainException("A rejected upgrade request must carry a rejection reason.");
        }

        UserId = userId;
        NicNumber = nicNumber.Trim();
        Address = address.Trim();
        PhoneNumber2 = phoneNumber2.Trim();
        PropertyInfo = propertyInfo.Trim();
        Status = status;
        SubmittedAt = submittedAt;
        RejectionReason = rejectionReason;
        DecidedAt = decidedAt;
        DecidedBy = decidedBy;
    }

    public Guid UserId { get; }

    public string NicNumber { get; }

    public string Address { get; }

    /// <summary>The applicant's alternate/property contact number. Their account's existing
    /// contact number (collected at registration) serves as phone 1 - it is never re-collected
    /// or stored here.</summary>
    public string PhoneNumber2 { get; }

    public string PropertyInfo { get; }

    public UpgradeRequestStatus Status { get; private set; }

    public string? RejectionReason { get; private set; }

    public DateTimeOffset SubmittedAt { get; }

    public DateTimeOffset? DecidedAt { get; private set; }

    /// <summary>The admin who decided. Null while pending.</summary>
    public Guid? DecidedBy { get; private set; }

    public bool IsPending => Status == UpgradeRequestStatus.Pending;

    /// <summary>
    /// Pending or approved requests both block a new submission (BR-SET-001): one is still
    /// being decided, the other already granted the role.
    /// </summary>
    public bool IsActive => Status is UpgradeRequestStatus.Pending or UpgradeRequestStatus.Approved;

    public static RoleUpgradeRequest Submit(
        Guid id,
        Guid userId,
        string nicNumber,
        string address,
        string phoneNumber2,
        string propertyInfo,
        DateTimeOffset now) =>
        new(id, userId, nicNumber, address, phoneNumber2, propertyInfo, UpgradeRequestStatus.Pending, now);

    /// <summary>Approving grants the Property Owner role immediately (SET-009, BR-SET-002).</summary>
    public void Approve(Guid decidedBy, DateTimeOffset now)
    {
        EnsureUndecided();

        Status = UpgradeRequestStatus.Approved;
        RejectionReason = null;
        DecidedBy = decidedBy;
        DecidedAt = now;
    }

    public void Reject(Guid decidedBy, string reason, DateTimeOffset now)
    {
        EnsureUndecided();

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("A rejection requires a reason.");
        }

        Status = UpgradeRequestStatus.Rejected;
        RejectionReason = reason.Trim();
        DecidedBy = decidedBy;
        DecidedAt = now;
    }

    private void EnsureUndecided()
    {
        if (Status != UpgradeRequestStatus.Pending)
        {
            throw new DomainException(
                $"This upgrade request has already been {Status.ToString().ToLowerInvariant()}.");
        }
    }
}
