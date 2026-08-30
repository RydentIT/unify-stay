using Unify.Application.Abstractions.Messaging;
using Unify.Application.Common;
using Unify.Domain.Users;

namespace Unify.Application.Features.Profile;

internal sealed class GetProfileQueryHandler : IQueryHandler<GetProfileQuery, Result<ProfileDto>>
{
    private readonly CurrentUserAccessor _currentUserAccessor;

    public GetProfileQueryHandler(CurrentUserAccessor currentUserAccessor) =>
        _currentUserAccessor = currentUserAccessor;

    public async Task<Result<ProfileDto>> HandleAsync(GetProfileQuery request, CancellationToken cancellationToken)
    {
        Result<User> resolved = await _currentUserAccessor
            .RequireActiveUserAsync(cancellationToken)
            .ConfigureAwait(false);

        if (resolved.IsFailure)
        {
            return Result.Failure<ProfileDto>(resolved.Error);
        }

        return Result.Success(ProfileMapper.ToDto(resolved.Value));
    }
}

/// <summary>Shared projection so every profile response has the same shape.</summary>
internal static class ProfileMapper
{
    public static ProfileDto ToDto(User user) => new(
        user.Id,
        user.FirstName,
        user.LastName,
        user.Email.Value,
        user.AvatarUrl,
        user.ContactNumber,
        user.EmailVerified,
        user.Status.ToString(),
        user.PendingEmail,
        [.. user.Roles.Select(role => role.ToString())],
        user.HasPassword,
        [.. user.AuthProviders.Select(provider => provider.Provider.ToString())],
        user.CreatedAt);
}
