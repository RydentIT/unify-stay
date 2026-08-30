using Unify.Application.Abstractions.Auditing;
using Unify.Application.Abstractions.Identity;
using Unify.Application.Abstractions.Messaging;
using Unify.Application.Abstractions.Persistence;
using Unify.Application.Abstractions.Security;
using Unify.Application.Abstractions.Time;
using Unify.Application.Common;
using Unify.Application.Features.Authentication.EmailVerification;
using Unify.Domain.Users;

namespace Unify.Application.Features.Authentication.Register;

/// <summary>
/// REG-003 (uniqueness across providers), REG-006 (consent), REG-007 (default Student role),
/// and the automatic verification email that EVR-001 requires.
/// </summary>
internal sealed class RegisterWithEmailCommandHandler
    : ICommandHandler<RegisterWithEmailCommand, Result<RegisterResult>>
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IDispatcher _dispatcher;
    private readonly IAuditLogger _auditLogger;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _clock;

    public RegisterWithEmailCommandHandler(
        IUserRepository users,
        IPasswordHasher passwordHasher,
        IDispatcher dispatcher,
        IAuditLogger auditLogger,
        ICurrentUser currentUser,
        IDateTimeProvider clock)
    {
        _users = users;
        _passwordHasher = passwordHasher;
        _dispatcher = dispatcher;
        _auditLogger = auditLogger;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result<RegisterResult>> HandleAsync(
        RegisterWithEmailCommand request,
        CancellationToken cancellationToken)
    {
        var email = Email.Create(request.Email);

        // REG-003: uniqueness is checked across ALL providers, not just local accounts, so a
        // Google-registered address cannot be claimed a second time with a password.
        User? existing = await _users.GetByEmailAsync(email, cancellationToken).ConfigureAwait(false);

        if (existing is not null)
        {
            await _auditLogger.LogAsync(
                new AuditEvent
                {
                    ActionType = AuditActions.RegisterEmailInUse,
                    Email = email.Value,
                    Provider = nameof(AuthProviderKind.Local),
                    IpAddress = _currentUser.IpAddress,
                },
                cancellationToken).ConfigureAwait(false);

            return Result.Failure<RegisterResult>(AuthErrors.EmailAlreadyRegistered);
        }

        DateTimeOffset now = _clock.UtcNow;

        var user = User.RegisterWithEmail(
            Guid.CreateVersion7(),
            request.FirstName,
            request.LastName,
            email,
            _passwordHasher.Hash(request.Password),
            request.ContactNumber,
            now);

        user.AddAuthProvider(AuthProvider.Local(Guid.CreateVersion7(), user.Id, now));

        await _users.AddAsync(user, cancellationToken).ConfigureAwait(false);

        await _auditLogger.LogAsync(
            new AuditEvent
            {
                ActionType = AuditActions.RegisterSucceeded,
                UserId = user.Id,
                Email = email.Value,
                Provider = nameof(AuthProviderKind.Local),
                IpAddress = _currentUser.IpAddress,
                FieldsChanged = new { role = nameof(RoleName.Student) },
            },
            cancellationToken).ConfigureAwait(false);

        // EVR-001: verification is triggered as part of registration rather than left to the
        // caller, so no path can create an account and silently skip it.
        await _dispatcher
            .SendAsync(new SendVerificationEmailCommand(user.Id), cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(new RegisterResult(user.Id, email.Value, EmailVerificationRequired: true));
    }
}
