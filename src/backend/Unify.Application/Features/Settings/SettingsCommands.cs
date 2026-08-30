using System.Text.RegularExpressions;
using FluentValidation;
using Unify.Application.Abstractions.Messaging;
using Unify.Application.Common;

namespace Unify.Application.Features.Settings;

public sealed record UpgradeRequestDto(
    Guid Id,
    Guid UserId,
    string? UserName,
    string? UserEmail,
    string NicNumber,
    string Address,
    string PhoneNumber2,
    string PropertyInfo,
    string Status,
    string? RejectionReason,
    DateTimeOffset SubmittedAt,
    DateTimeOffset? DecidedAt);

/// <summary>
/// SET-005 / BR-SET-001. Structured fields only, no document upload - an admin reviews these
/// manually. PhoneNumber2 is validated against the account's existing contact number (phone 1)
/// in the handler rather than here, since that check needs the loaded User.
/// </summary>
public sealed record RequestPropertyOwnerUpgradeCommand(
    string NicNumber,
    string Address,
    string PhoneNumber2,
    string PropertyInfo) : ICommand<Result<UpgradeRequestDto>>;

/// <summary>SET-008: only open once a previous request has been rejected.</summary>
public sealed record ResubmitUpgradeRequestCommand(
    string NicNumber,
    string Address,
    string PhoneNumber2,
    string PropertyInfo) : ICommand<Result<UpgradeRequestDto>>;

public sealed record GetMyUpgradeRequestQuery : IQuery<Result<UpgradeRequestDto?>>;

public sealed record ListUpgradeRequestsQuery(string Status) : IQuery<Result<IReadOnlyList<UpgradeRequestDto>>>;

public sealed record ApproveUpgradeRequestCommand(Guid RequestId) : ICommand<Result>;

/// <summary>SET-007: the reason is required, and is surfaced to the applicant.</summary>
public sealed record RejectUpgradeRequestCommand(Guid RequestId, string Reason) : ICommand<Result>;

/// <summary>SET-012: password confirmation, because both are destructive and irreversible-ish.</summary>
public sealed record DeactivateAccountCommand(string Password) : ICommand<Result>;

public sealed record DeleteAccountCommand(string Password) : ICommand<Result>;

/// <summary>
/// Shared shape rules for the upgrade-request fields. A partial class purely so the two
/// [GeneratedRegex] source-generated members can live beside the rules that use them.
/// </summary>
public sealed partial class RequestPropertyOwnerUpgradeCommandValidator
    : AbstractValidator<RequestPropertyOwnerUpgradeCommand>
{
    /// <summary>Sri Lankan NIC: old 9-digit + V/X, or new 12-digit. Case-insensitive on the suffix.</summary>
    [GeneratedRegex(@"^([0-9]{9}[VXvx]|[0-9]{12})$")]
    internal static partial Regex NicPattern();

    /// <summary>Matches the phone pattern used everywhere else in the app (registration, profile).</summary>
    [GeneratedRegex(@"^\+?[0-9()\-\s]{7,32}$")]
    internal static partial Regex PhonePattern();

    public RequestPropertyOwnerUpgradeCommandValidator()
    {
        RuleFor(command => command.NicNumber)
            .NotEmpty().WithMessage("An NIC number is required.")
            .Matches(NicPattern()).WithMessage("Enter a valid NIC number (old 9-digit or new 12-digit format).");

        RuleFor(command => command.Address)
            .NotEmpty().WithMessage("An address is required.")
            .MaximumLength(255);

        RuleFor(command => command.PhoneNumber2)
            .NotEmpty().WithMessage("A second phone number is required.")
            .MaximumLength(20)
            .Matches(PhonePattern()).WithMessage("Enter a valid phone number.");

        RuleFor(command => command.PropertyInfo)
            .NotEmpty().WithMessage("Property information is required.")
            .MaximumLength(4000);
    }
}

public sealed class ResubmitUpgradeRequestCommandValidator : AbstractValidator<ResubmitUpgradeRequestCommand>
{
    public ResubmitUpgradeRequestCommandValidator()
    {
        RuleFor(command => command.NicNumber)
            .NotEmpty().WithMessage("An NIC number is required.")
            .Matches(RequestPropertyOwnerUpgradeCommandValidator.NicPattern())
                .WithMessage("Enter a valid NIC number (old 9-digit or new 12-digit format).");

        RuleFor(command => command.Address)
            .NotEmpty().WithMessage("An address is required.")
            .MaximumLength(255);

        RuleFor(command => command.PhoneNumber2)
            .NotEmpty().WithMessage("A second phone number is required.")
            .MaximumLength(20)
            .Matches(RequestPropertyOwnerUpgradeCommandValidator.PhonePattern())
                .WithMessage("Enter a valid phone number.");

        RuleFor(command => command.PropertyInfo)
            .NotEmpty().WithMessage("Property information is required.")
            .MaximumLength(4000);
    }
}

public sealed class RejectUpgradeRequestCommandValidator : AbstractValidator<RejectUpgradeRequestCommand>
{
    public RejectUpgradeRequestCommandValidator()
    {
        RuleFor(command => command.RequestId).NotEmpty();

        RuleFor(command => command.Reason)
            .NotEmpty().WithMessage("A rejection reason is required.")
            .MaximumLength(1000);
    }
}

public sealed class DeactivateAccountCommandValidator : AbstractValidator<DeactivateAccountCommand>
{
    public DeactivateAccountCommandValidator() => RuleFor(command => command.Password).NotEmpty();
}

public sealed class DeleteAccountCommandValidator : AbstractValidator<DeleteAccountCommand>
{
    public DeleteAccountCommandValidator() => RuleFor(command => command.Password).NotEmpty();
}
