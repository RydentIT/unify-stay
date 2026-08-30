using Unify.Application.Abstractions.Identity;
using Unify.Application.Abstractions.Persistence;
using Unify.Application.Abstractions.Security;
using Unify.Application.Common;
using Unify.Domain.Users;

namespace Unify.Application.Features.Profile;

/// <summary>
/// Loads the signed-in user and applies the gate that every profile and settings operation
/// shares.
///
/// PRF-011 / AC-PRF-009: while must_change_password is set, the account is closed to everything
/// except the forced-reset endpoint. must_complete_profile closes it the same way for a
/// Google account missing a phone number. The API enforces both at the authorization-policy
/// level too, but repeating it here means a handler cannot be reached through some future route
/// that forgot the policy - the two checks are independent on purpose.
/// </summary>
internal sealed class CurrentUserAccessor
{
    private readonly IUserRepository _users;
    private readonly ICurrentUser _currentUser;

    public CurrentUserAccessor(IUserRepository users, ICurrentUser currentUser)
    {
        _users = users;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Resolves the caller, rejecting anything that must not proceed: unauthenticated, deleted,
    /// or still owing a mandatory password change.
    /// </summary>
    public async Task<Result<User>> RequireActiveUserAsync(CancellationToken cancellationToken)
    {
        if (_currentUser.UserId is not Guid userId)
        {
            return Result.Failure<User>(AuthErrors.InvalidCredentials);
        }

        // A limited-scope token never unlocks these handlers.
        if (_currentUser.Scope == TokenScope.PasswordChangeRequired)
        {
            return Result.Failure<User>(AuthErrors.PasswordChangeRequired);
        }

        if (_currentUser.Scope == TokenScope.ProfileCompletionRequired)
        {
            return Result.Failure<User>(AuthErrors.ProfileCompletionRequired);
        }

        User? user = await _users.GetByIdAsync(userId, cancellationToken).ConfigureAwait(false);

        if (user is null || user.IsDeleted)
        {
            return Result.Failure<User>(AuthErrors.UserNotFound);
        }

        // Suspension domain guard: closes every profile/settings action, not just login, until
        // an admin lifts it. A token issued before the suspension remains structurally valid, so
        // this is the check that actually stops it being used.
        if (user.IsSuspended)
        {
            return Result.Failure<User>(AuthErrors.AccountSuspended);
        }

        // Belt and braces: the flag on the record is authoritative even if a token predates it
        // being set (for example an admin forcing a reset mid-session).
        if (user.MustChangePassword)
        {
            return Result.Failure<User>(AuthErrors.PasswordChangeRequired);
        }

        if (user.MustCompleteProfile)
        {
            return Result.Failure<User>(AuthErrors.ProfileCompletionRequired);
        }

        return Result.Success(user);
    }
}
