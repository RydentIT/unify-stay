using Unify.Api.Extensions;
using Unify.Application.Abstractions.Messaging;
using Unify.Application.Abstractions.Storage;
using Unify.Application.Common;
using Unify.Application.Features.Profile;

namespace Unify.Api.Endpoints;

/// <summary>
/// Routes under /api/profile.
///
/// The whole group requires the default full-access policy, so a limited-scope token cannot
/// reach any of it (PRF-011/AC-PRF-009). The handlers repeat that check independently, because
/// a route added later that forgets the policy would otherwise be silently open.
/// </summary>
internal static class ProfileEndpoints
{
    public static IEndpointRouteBuilder MapProfileEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/profile")
            .WithTags("Profile")
            .RequireAuthorization(AuthorizationPolicies.FullAccess);

        group.MapGet("/", async (IDispatcher dispatcher, CancellationToken cancellationToken) =>
            {
                Result<ProfileDto> result = await dispatcher
                    .SendAsync(new GetProfileQuery(), cancellationToken)
                    .ConfigureAwait(false);

                return result.ToHttpResult();
            })
            .WithName("GetProfile")
            .WithSummary("Read the signed-in user's profile.");

        group.MapPut("/", async (
                UpdateProfileCommand command,
                IDispatcher dispatcher,
                CancellationToken cancellationToken) =>
            {
                Result<ProfileDto> result = await dispatcher
                    .SendAsync(command, cancellationToken)
                    .ConfigureAwait(false);

                return result.ToHttpResult();
            })
            .WithName("UpdateProfile")
            .WithSummary("Update name, avatar and contact number.");

        // Avatar upload is separate from the JSON profile update because it is multipart; the
        // stored URL is then saved through the ordinary update path.
        group.MapPost("/avatar", async (
                IFormFile file,
                IFileStorageService fileStorage,
                IDispatcher dispatcher,
                CancellationToken cancellationToken) =>
            {
                if (file is null || file.Length == 0)
                {
                    return Results.BadRequest(new { title = "A file is required." });
                }

                await using Stream content = file.OpenReadStream();

                StoredFile stored = await fileStorage.SaveAsync(
                    "avatars",
                    new FileToStore(content, file.FileName, file.ContentType, file.Length),
                    cancellationToken).ConfigureAwait(false);

                string url = fileStorage.GetPublicUrl(stored.Path);

                Result<ProfileDto> current = await dispatcher
                    .SendAsync(new GetProfileQuery(), cancellationToken)
                    .ConfigureAwait(false);

                if (current.IsFailure)
                {
                    return current.ToHttpResult();
                }

                Result<ProfileDto> updated = await dispatcher
                    .SendAsync(
                        new UpdateProfileCommand(
                            current.Value.FirstName,
                            current.Value.LastName,
                            url,
                            current.Value.ContactNumber),
                        cancellationToken)
                    .ConfigureAwait(false);

                return updated.ToHttpResult();
            })
            .WithName("UploadAvatar")
            .WithSummary("Upload a new avatar image.")
            .DisableAntiforgery();

        group.MapPost("/change-password", async (
                ChangePasswordCommand command,
                IDispatcher dispatcher,
                CancellationToken cancellationToken) =>
            {
                Result result = await dispatcher.SendAsync(command, cancellationToken).ConfigureAwait(false);
                return result.ToHttpResult();
            })
            .WithName("ChangePassword")
            .WithSummary("Change password, confirming the current one first.");

        group.MapPost("/change-email", async (
                RequestEmailChangeCommand command,
                IDispatcher dispatcher,
                CancellationToken cancellationToken) =>
            {
                Result result = await dispatcher.SendAsync(command, cancellationToken).ConfigureAwait(false);
                return result.IsSuccess ? Results.Accepted() : result.ToHttpResult();
            })
            .WithName("RequestEmailChange")
            .WithSummary("Request a change of email address.");

        // Anonymous: the user follows this link from the NEW inbox, possibly with no session.
        app.MapPost("/api/profile/confirm-email-change", async (
                ConfirmEmailChangeCommand command,
                IDispatcher dispatcher,
                CancellationToken cancellationToken) =>
            {
                Result result = await dispatcher.SendAsync(command, cancellationToken).ConfigureAwait(false);
                return result.ToHttpResult();
            })
            .WithTags("Profile")
            .WithName("ConfirmEmailChange")
            .WithSummary("Confirm a pending email change using its token.")
            .AllowAnonymous();

        return app;
    }
}
