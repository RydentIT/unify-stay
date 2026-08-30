using NSubstitute;
using Unify.Application.Abstractions.Security;
using Unify.Application.Common;
using Unify.Application.Features.Profile;
using Unify.Application.Features.Settings;
using Unify.Domain.Settings;
using Unify.Domain.Users;

namespace Unify.Application.Tests.Features;

/// <summary>
/// PRF-004, PRF-005, PRF-010, PRF-011, PRF-012, BR-PRF-003/004/005 and the Settings rules
/// BR-SET-001, SET-007, SET-008, SET-009, SET-012, SET-013, BR-SET-002.
/// </summary>
public sealed class ProfileAndSettingsTests
{
    private readonly TestKit _kit = new();

    private CurrentUserAccessor Accessor => new(_kit.Users, _kit.CurrentUser);

    private ChangePasswordCommandHandler CreateChangePassword() => new(
        Accessor, _kit.Users, _kit.PasswordHasher, _kit.Sessions,
        _kit.EmailSender, _kit.Templates, _kit.AuditLogger, _kit.CurrentUser, _kit.Clock);

    private UpdateProfileCommandHandler CreateUpdateProfile() => new(
        Accessor, _kit.Users, _kit.AuditLogger, _kit.CurrentUser, _kit.Clock);

    private DeactivateAccountCommandHandler CreateDeactivate() => new(
        Accessor, _kit.Users, _kit.PasswordHasher, _kit.Sessions,
        _kit.EmailSender, _kit.Templates, _kit.AuditLogger, _kit.CurrentUser, _kit.Clock);

    private UpgradeRequestService CreateUpgradeService() => new(
        Accessor, _kit.UpgradeRequests, _kit.AuditLogger, _kit.CurrentUser, _kit.Clock);

    private ApproveUpgradeRequestCommandHandler CreateApprove() => new(
        _kit.UpgradeRequests, _kit.Users, _kit.EmailSender, _kit.Templates,
        _kit.AuditLogger, _kit.CurrentUser, _kit.Clock);

    // ---------------------------------------------------------------- Profile

    /// <summary>PRF-011 / AC-PRF-009: profile routes are shut while a reset is owed.</summary>
    [Fact]
    public async Task Profile_is_blocked_while_a_password_change_is_required()
    {
        User user = TestKit.ActiveStudent(mustChangePassword: true);
        _kit.SignIn(user);

        Result<ProfileDto> result = await CreateUpdateProfile().HandleAsync(
            new UpdateProfileCommand("New", "Name", null, null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.PasswordChangeRequired, result.Error);
    }

    /// <summary>
    /// The suspension domain guard: closes profile/settings actions, not just login, even if the
    /// caller is still holding a token issued before the suspension.
    /// </summary>
    [Fact]
    public async Task Profile_is_blocked_for_a_suspended_account()
    {
        User user = TestKit.ActiveStudent(status: UserStatus.Suspended);
        _kit.SignIn(user);

        Result<ProfileDto> result = await CreateUpdateProfile().HandleAsync(
            new UpdateProfileCommand("New", "Name", null, null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.AccountSuspended, result.Error);
    }

    /// <summary>The limited-scope token alone is enough to refuse, even if the flag were stale.</summary>
    [Fact]
    public async Task Profile_is_blocked_for_a_limited_scope_token()
    {
        User user = TestKit.ActiveStudent();
        _kit.SignIn(user, TokenScope.PasswordChangeRequired);

        Result<ProfileDto> result = await CreateUpdateProfile().HandleAsync(
            new UpdateProfileCommand("New", "Name", null, null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.PasswordChangeRequired, result.Error);
    }

    /// <summary>BR-PRF-005: role and verification status are untouched by a profile edit.</summary>
    [Fact]
    public async Task Updating_the_profile_leaves_roles_and_verification_alone()
    {
        User user = TestKit.ActiveStudent();
        _kit.SignIn(user);

        Result<ProfileDto> result = await CreateUpdateProfile().HandleAsync(
            new UpdateProfileCommand("Renamed", "Person", "https://cdn/a.png", "+94 71 000 0000"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Renamed", user.FirstName);
        Assert.Equal("Person", user.LastName);
        Assert.True(user.EmailVerified);
        Assert.Equal(RoleName.Student, Assert.Single(user.Roles));
    }

    /// <summary>PRF-004 / BR-PRF-001.</summary>
    [Fact]
    public async Task Change_password_requires_the_current_password()
    {
        User user = TestKit.ActiveStudent();
        _kit.SignIn(user);
        _kit.PasswordHasher.Verify("wrong-current", user.PasswordHash).Returns(false);

        Result result = await CreateChangePassword().HandleAsync(
            new ChangePasswordCommand("wrong-current", "a-brand-new-password"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.CurrentPasswordIncorrect, result.Error);
    }

    /// <summary>PRF-012 / BR-PRF-004.</summary>
    [Fact]
    public async Task Change_password_is_refused_for_a_google_only_account()
    {
        User user = TestKit.GoogleOnlyUser();
        _kit.SignIn(user);

        Result result = await CreateChangePassword().HandleAsync(
            new ChangePasswordCommand("anything", "a-brand-new-password"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.NoPasswordForGoogleAccount, result.Error);
    }

    /// <summary>
    /// PRF-010 / BR-PRF-003. The distinction from a reset matters: this flow proved possession
    /// of the current password, so the caller's own session survives.
    /// </summary>
    [Fact]
    public async Task Change_password_signs_out_other_sessions_but_not_the_current_one()
    {
        User user = TestKit.ActiveStudent();
        Guid sessionId = Guid.CreateVersion7();
        _kit.SignIn(user, sessionId: sessionId);

        _kit.PasswordHasher.Verify("current-password", user.PasswordHash).Returns(true);
        _kit.PasswordHasher.Verify("a-brand-new-password", user.PasswordHash).Returns(false);
        _kit.PasswordHasher.Hash("a-brand-new-password").Returns("new-hash");

        Result result = await CreateChangePassword().HandleAsync(
            new ChangePasswordCommand("current-password", "a-brand-new-password"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);

        await _kit.Sessions.Received(1).RevokeAllForUserExceptAsync(
            user.Id, sessionId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());

        await _kit.Sessions.DidNotReceive().RevokeAllForUserAsync(
            user.Id, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    /// <summary>PRF-005.</summary>
    [Fact]
    public async Task Change_password_rejects_a_new_password_matching_the_current_one()
    {
        User user = TestKit.ActiveStudent();
        _kit.SignIn(user);
        _kit.PasswordHasher.Verify(Arg.Any<string>(), user.PasswordHash).Returns(true);

        Result result = await CreateChangePassword().HandleAsync(
            new ChangePasswordCommand("same-password", "same-password"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.PasswordMatchesCurrent, result.Error);
    }

    // ---------------------------------------------------------------- Settings

    private static readonly (string NicNumber, string Address, string PhoneNumber2, string PropertyInfo)
        ValidUpgradeFields = ("199912345678", "123 Galle Road, Colombo", "+94711234567", "Two-bedroom apartment.");

    /// <summary>BR-SET-001: one active request at a time.</summary>
    [Fact]
    public async Task Upgrade_request_is_refused_while_another_is_active()
    {
        User user = TestKit.ActiveStudent();
        _kit.SignIn(user);

        _kit.UpgradeRequests
            .GetActiveForUserAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(SubmittedRequest(user.Id));

        Result<UpgradeRequestDto> result = await SubmitValidAsync(isResubmission: false);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.UpgradeRequestAlreadyActive, result.Error);
    }

    /// <summary>
    /// Phone 1 is the account's existing contact number, collected at registration. Phone 2 has
    /// to be a genuinely different number, or asking for a second one is pointless.
    /// </summary>
    [Fact]
    public async Task Upgrade_request_is_refused_when_the_second_phone_matches_the_first()
    {
        User user = TestKit.ActiveStudent(contactNumber: "+94711234567");
        _kit.SignIn(user);

        Result<UpgradeRequestDto> result = await CreateUpgradeService().SubmitAsync(
            ValidUpgradeFields.NicNumber,
            ValidUpgradeFields.Address,
            "+94711234567",
            ValidUpgradeFields.PropertyInfo,
            isResubmission: false,
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.SecondPhoneMatchesPrimary, result.Error);
    }

    /// <summary>
    /// The comparison ignores punctuation/spacing, not just an exact string match - so "the same
    /// number, formatted differently" is still caught rather than slipping through on a technicality.
    /// </summary>
    [Fact]
    public async Task Upgrade_request_treats_differently_formatted_versions_of_the_same_number_as_equal()
    {
        User user = TestKit.ActiveStudent(contactNumber: "+94 71 123 4567");
        _kit.SignIn(user);

        Result<UpgradeRequestDto> result = await CreateUpgradeService().SubmitAsync(
            ValidUpgradeFields.NicNumber,
            ValidUpgradeFields.Address,
            "+94-71-123-4567",
            ValidUpgradeFields.PropertyInfo,
            isResubmission: false,
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.SecondPhoneMatchesPrimary, result.Error);
    }

    /// <summary>SET-005: the applicant keeps their current role while the request is pending.</summary>
    [Fact]
    public async Task Submitting_an_upgrade_request_does_not_grant_any_role()
    {
        User user = TestKit.ActiveStudent(contactNumber: "+94770000000");
        _kit.SignIn(user);

        Result<UpgradeRequestDto> result = await SubmitValidAsync(isResubmission: false);

        Assert.True(result.IsSuccess);
        Assert.Equal(nameof(UpgradeRequestStatus.Pending), result.Value.Status);
        Assert.Equal(ValidUpgradeFields.NicNumber, result.Value.NicNumber);
        Assert.Equal(RoleName.Student, Assert.Single(user.Roles));

        await _kit.Users.DidNotReceive().AddRoleAsync(
            Arg.Any<Guid>(), RoleName.PropertyOwner, Arg.Any<CancellationToken>());
    }

    /// <summary>SET-008: resubmission is only open after a rejection.</summary>
    [Fact]
    public async Task Resubmission_is_refused_when_the_previous_request_was_not_rejected()
    {
        User user = TestKit.ActiveStudent(contactNumber: "+94770000000");
        _kit.SignIn(user);

        _kit.UpgradeRequests.GetActiveForUserAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns((RoleUpgradeRequest?)null);
        _kit.UpgradeRequests.GetLatestForUserAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns((RoleUpgradeRequest?)null);

        Result<UpgradeRequestDto> result = await SubmitValidAsync(isResubmission: true);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.UpgradeRequestNotRejected, result.Error);
    }

    /// <summary>SET-009 / BR-SET-002: approval grants the role immediately.</summary>
    [Fact]
    public async Task Approving_an_upgrade_grants_the_property_owner_role()
    {
        User admin = TestKit.ActiveStudent("admin@example.com");
        User applicant = TestKit.ActiveStudent("applicant@example.com");

        _kit.CurrentUser.UserId.Returns(admin.Id);
        _kit.CurrentUser.IpAddress.Returns("203.0.113.5");

        RoleUpgradeRequest request = SubmittedRequest(applicant.Id);

        _kit.UpgradeRequests.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);
        _kit.Users.GetByIdAsync(applicant.Id, Arg.Any<CancellationToken>()).Returns(applicant);

        Result result = await CreateApprove().HandleAsync(
            new ApproveUpgradeRequestCommand(request.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(UpgradeRequestStatus.Approved, request.Status);

        await _kit.Users.Received(1).AddRoleAsync(
            applicant.Id, RoleName.PropertyOwner, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_already_decided_upgrade_cannot_be_decided_again()
    {
        User admin = TestKit.ActiveStudent("admin@example.com");
        _kit.CurrentUser.UserId.Returns(admin.Id);

        RoleUpgradeRequest request = SubmittedRequest(Guid.CreateVersion7(), TestKit.Now.AddDays(-2));
        request.Reject(admin.Id, "NIC does not match the name on file.", TestKit.Now.AddDays(-1));

        _kit.UpgradeRequests.GetByIdAsync(request.Id, Arg.Any<CancellationToken>()).Returns(request);

        Result result = await CreateApprove().HandleAsync(
            new ApproveUpgradeRequestCommand(request.Id),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.UpgradeRequestAlreadyDecided, result.Error);
    }

    /// <summary>SET-007: the domain refuses a rejection with no reason.</summary>
    [Fact]
    public void Rejecting_without_a_reason_is_a_domain_error()
    {
        RoleUpgradeRequest request = SubmittedRequest(Guid.CreateVersion7());

        Assert.Throws<Domain.Common.DomainException>(() =>
            request.Reject(Guid.CreateVersion7(), "   ", TestKit.Now));
    }

    private Task<Result<UpgradeRequestDto>> SubmitValidAsync(bool isResubmission) =>
        CreateUpgradeService().SubmitAsync(
            ValidUpgradeFields.NicNumber,
            ValidUpgradeFields.Address,
            ValidUpgradeFields.PhoneNumber2,
            ValidUpgradeFields.PropertyInfo,
            isResubmission,
            CancellationToken.None);

    private static RoleUpgradeRequest SubmittedRequest(Guid userId, DateTimeOffset? submittedAt = null) =>
        RoleUpgradeRequest.Submit(
            Guid.CreateVersion7(),
            userId,
            ValidUpgradeFields.NicNumber,
            ValidUpgradeFields.Address,
            ValidUpgradeFields.PhoneNumber2,
            ValidUpgradeFields.PropertyInfo,
            submittedAt ?? TestKit.Now.AddDays(-1));

    /// <summary>SET-012 / SET-013.</summary>
    [Fact]
    public async Task Deactivation_requires_the_password_and_revokes_every_session()
    {
        User user = TestKit.ActiveStudent();
        _kit.SignIn(user);
        _kit.PasswordHasher.Verify("my-password", user.PasswordHash).Returns(true);

        Result result = await CreateDeactivate().HandleAsync(
            new DeactivateAccountCommand("my-password"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(UserStatus.Deactivated, user.Status);

        await _kit.Sessions.Received(1).RevokeAllForUserAsync(
            user.Id, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Deactivation_is_refused_with_the_wrong_password()
    {
        User user = TestKit.ActiveStudent();
        _kit.SignIn(user);
        _kit.PasswordHasher.Verify(Arg.Any<string>(), Arg.Any<string?>()).Returns(false);

        Result result = await CreateDeactivate().HandleAsync(
            new DeactivateAccountCommand("wrong-password"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(UserStatus.Active, user.Status);

        await _kit.Sessions.DidNotReceive().RevokeAllForUserAsync(
            Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }
}
