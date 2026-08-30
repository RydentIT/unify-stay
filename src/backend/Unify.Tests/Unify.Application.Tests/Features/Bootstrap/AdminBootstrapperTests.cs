using Microsoft.Extensions.Logging;
using NSubstitute;
using Unify.Application.Abstractions.Messaging;
using Unify.Application.Features.Authentication.EmailVerification;
using Unify.Application.Features.Bootstrap;
using Unify.Application.Options;
using Unify.Domain.Users;

namespace Unify.Application.Tests.Features.Bootstrap;

/// <summary>
/// Covers the four requirements from the bootstrap spec directly: (a) exactly one Admin is
/// created when none exists, (b) a second run is a clean no-op, (c) the created account cannot
/// sign in until it is verified, (d) the raw password is never logged or persisted anywhere.
/// </summary>
public sealed class AdminBootstrapperTests
{
    private readonly TestKit _kit = new();
    private readonly RecordingLogger<AdminBootstrapper> _logger = new();

    private AdminBootstrapper CreateBootstrapper(string? email = "admin@example.com", string? password = "correct-horse-battery-staple") =>
        new(
            _kit.Users,
            _kit.PasswordHasher,
            _kit.Dispatcher,
            _kit.Clock,
            Microsoft.Extensions.Options.Options.Create(new AdminBootstrapOptions { Email = email, Password = password }),
            _logger);

    [Fact]
    public async Task Creates_exactly_one_admin_when_none_exists()
    {
        _kit.Users.ExistsAnyWithRoleAsync(RoleName.Admin, Arg.Any<CancellationToken>()).Returns(false);
        _kit.PasswordHasher.Hash("correct-horse-battery-staple").Returns("hashed-admin-password");

        User? created = null;
        await _kit.Users.AddAsync(Arg.Do<User>(user => created = user), Arg.Any<CancellationToken>());

        AdminBootstrapOutcome outcome = await CreateBootstrapper().RunAsync(CancellationToken.None);

        Assert.Equal(AdminBootstrapOutcome.Created, outcome);

        await _kit.Users.Received(1).AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());

        Assert.NotNull(created);
        Assert.Equal(RoleName.Admin, Assert.Single(created.Roles));
        Assert.Equal("admin@example.com", created.Email.Value);
        Assert.Equal("hashed-admin-password", created.PasswordHash);

        // (c) EVR-001-style: verification is dispatched exactly like a normal registration, and
        // the account stays unverified until VerifyEmailCommand runs it through - login already
        // refuses any unverified account (LOG-004), covered by LoginWithEmailTests.
        Assert.False(created.EmailVerified);

        await _kit.Dispatcher.Received(1).SendAsync(
            Arg.Is<SendVerificationEmailCommand>(command => command.UserId == created.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Is_a_no_op_when_an_admin_already_exists()
    {
        _kit.Users.ExistsAnyWithRoleAsync(RoleName.Admin, Arg.Any<CancellationToken>()).Returns(true);

        AdminBootstrapOutcome outcome = await CreateBootstrapper().RunAsync(CancellationToken.None);

        Assert.Equal(AdminBootstrapOutcome.AlreadyExists, outcome);

        await _kit.Users.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        await _kit.Dispatcher.DidNotReceive()
            .SendAsync(Arg.Any<SendVerificationEmailCommand>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(null, "correct-horse-battery-staple")]
    [InlineData("admin@example.com", null)]
    [InlineData("", "")]
    public async Task Refuses_to_run_without_both_environment_variables_set(string? email, string? password)
    {
        _kit.Users.ExistsAnyWithRoleAsync(RoleName.Admin, Arg.Any<CancellationToken>()).Returns(false);

        AdminBootstrapOutcome outcome = await CreateBootstrapper(email, password).RunAsync(CancellationToken.None);

        Assert.Equal(AdminBootstrapOutcome.MissingConfiguration, outcome);
        await _kit.Users.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    /// <summary>(d): the raw password must never appear in a log call, only the hash goes anywhere.</summary>
    [Fact]
    public async Task Never_logs_the_raw_password()
    {
        const string rawPassword = "correct-horse-battery-staple";

        _kit.Users.ExistsAnyWithRoleAsync(RoleName.Admin, Arg.Any<CancellationToken>()).Returns(false);
        _kit.PasswordHasher.Hash(rawPassword).Returns("hashed-admin-password");

        await CreateBootstrapper(password: rawPassword).RunAsync(CancellationToken.None);

        Assert.DoesNotContain(_logger.Messages, message => message.Contains(rawPassword, StringComparison.Ordinal));
    }

    /// <summary>Minimal ILogger that records the formatted message of every call, for asserting on content.</summary>
    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }
}
