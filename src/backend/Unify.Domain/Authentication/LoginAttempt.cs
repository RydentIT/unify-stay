using Unify.Domain.Common;

namespace Unify.Domain.Authentication;

/// <summary>
/// One recorded sign-in attempt. Feeds the lockout counters (LOG-005/LOG-006).
///
/// UserId is nullable because an attempt against an address that does not exist still has to
/// be counted against the originating IP - otherwise enumeration is free.
/// </summary>
public sealed class LoginAttempt : Entity
{
    public LoginAttempt(
        Guid id,
        Guid? userId,
        string? email,
        string? ipAddress,
        bool success,
        DateTimeOffset attemptedAt)
        : base(id)
    {
        UserId = userId;
        Email = email;
        IpAddress = ipAddress;
        Success = success;
        AttemptedAt = attemptedAt;
    }

    public Guid? UserId { get; }

    public string? Email { get; }

    public string? IpAddress { get; }

    public bool Success { get; }

    public DateTimeOffset AttemptedAt { get; }

    public static LoginAttempt Failure(Guid? userId, string? email, string? ipAddress, DateTimeOffset now) =>
        new(Guid.CreateVersion7(), userId, email, ipAddress, success: false, now);

    public static LoginAttempt Successful(Guid userId, string? email, string? ipAddress, DateTimeOffset now) =>
        new(Guid.CreateVersion7(), userId, email, ipAddress, success: true, now);
}
