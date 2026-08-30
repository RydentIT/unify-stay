using FluentValidation;
using Microsoft.Extensions.Options;
using Unify.Application.Abstractions.Messaging;
using Unify.Application.Common;
using Unify.Application.Options;
using Unify.Domain.Users;

namespace Unify.Application.Features.Profile;

public sealed record ProfileDto(
    Guid UserId,
    string FirstName,
    string LastName,
    string Email,
    string? AvatarUrl,
    string? ContactNumber,
    bool EmailVerified,
    string Status,
    string? PendingEmail,
    IReadOnlyCollection<string> Roles,
    bool HasPassword,
    IReadOnlyCollection<string> LinkedProviders,
    DateTimeOffset CreatedAt);

public sealed record GetProfileQuery : IQuery<Result<ProfileDto>>;

/// <summary>
/// BR-PRF-005: name, avatar and contact number only. Role and verification status are
/// deliberately absent from this command so they cannot be edited through the profile screen.
/// </summary>
public sealed record UpdateProfileCommand(
    string FirstName,
    string LastName,
    string? AvatarUrl,
    string? ContactNumber) : ICommand<Result<ProfileDto>>;

/// <summary>Self-service change while signed in (PRF-004, PRF-005, PRF-010, PRF-012).</summary>
public sealed record ChangePasswordCommand(
    string CurrentPassword,
    string NewPassword) : ICommand<Result>;

/// <summary>PRF-009: stores the new address as pending until its own token is confirmed.</summary>
public sealed record RequestEmailChangeCommand(string NewEmail, string CurrentPassword) : ICommand<Result>;

public sealed record ConfirmEmailChangeCommand(string Token) : ICommand<Result>;

public sealed class UpdateProfileCommandValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileCommandValidator()
    {
        RuleFor(command => command.FirstName).NotEmpty().MaximumLength(60);
        RuleFor(command => command.LastName).NotEmpty().MaximumLength(60);
        RuleFor(command => command.ContactNumber).MaximumLength(32);
        RuleFor(command => command.AvatarUrl).MaximumLength(2048);
    }
}

public sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator(IOptions<AuthOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        RuleFor(command => command.CurrentPassword).NotEmpty();

        RuleFor(command => command.NewPassword)
            .NotEmpty()
            .MinimumLength(options.Value.MinimumPasswordLength)
                .WithMessage($"Password must be at least {options.Value.MinimumPasswordLength} characters long.")
            .MaximumLength(256);
    }
}

public sealed class RequestEmailChangeCommandValidator : AbstractValidator<RequestEmailChangeCommand>
{
    public RequestEmailChangeCommandValidator()
    {
        RuleFor(command => command.NewEmail).NotEmpty().MaximumLength(Email.MaxLength).EmailAddress();
        RuleFor(command => command.CurrentPassword).NotEmpty();
    }
}

public sealed class ConfirmEmailChangeCommandValidator : AbstractValidator<ConfirmEmailChangeCommand>
{
    public ConfirmEmailChangeCommandValidator() =>
        RuleFor(command => command.Token).NotEmpty().MaximumLength(512);
}
