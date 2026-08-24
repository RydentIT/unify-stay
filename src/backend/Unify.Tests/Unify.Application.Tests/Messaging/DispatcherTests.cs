using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Unify.Application.Abstractions.Messaging;
using Unify.Application.Abstractions.Persistence;
using Unify.Application.Abstractions.Time;
using Unify.Application.Features.Authentication.RegisterUser;
using Unify.Application.Features.System;
using Unify.Domain.Common;
using ValidationException = Unify.Application.Common.Exceptions.ValidationException;

namespace Unify.Application.Tests.Messaging;

/// <summary>
/// Exercises AddApplication end to end: handlers and validators are discovered by assembly
/// scan, and the validation behaviour runs before the handler. A regression here means new
/// use cases would silently fail to resolve.
/// </summary>
public sealed class DispatcherTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();


        services.AddApplication();

        // Only the ports the exercised handlers need; the rest stay unregistered on purpose.
        var probe = Substitute.For<IDatabaseHealthProbe>();
        probe.CheckAsync(Arg.Any<CancellationToken>()).Returns(HealthState.Healthy);

        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTimeOffset.UnixEpoch);

        services.AddSingleton(probe);
        services.AddSingleton(clock);

        return services.BuildServiceProvider(validateScopes: true);
    }

    [Fact]
    public async Task Resolves_and_invokes_the_handler_registered_by_assembly_scan()
    {
        using ServiceProvider provider = BuildProvider();
        using IServiceScope scope = provider.CreateScope();

        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

        HealthReport report = await dispatcher.SendAsync(new GetHealthQuery());

        Assert.Equal(HealthState.Healthy, report.State);
    }

    [Fact]
    public async Task Validation_runs_before_the_handler_and_reports_every_failing_field()
    {
        using ServiceProvider provider = BuildProvider();
        using IServiceScope scope = provider.CreateScope();

        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

        var invalid = new RegisterUserCommand(
            Email: "not-an-email",
            Password: "short",
            DisplayName: "",
            AcceptedTerms: false);

        ValidationException exception = await Assert.ThrowsAsync<ValidationException>(
            () => dispatcher.SendAsync(invalid));

        Assert.Contains(nameof(RegisterUserCommand.Email), exception.Errors.Keys);
        Assert.Contains(nameof(RegisterUserCommand.Password), exception.Errors.Keys);
        Assert.Contains(nameof(RegisterUserCommand.DisplayName), exception.Errors.Keys);
        Assert.Contains(nameof(RegisterUserCommand.AcceptedTerms), exception.Errors.Keys);
    }

    [Fact]
    public async Task Dispatching_a_request_with_no_registered_handler_fails_loudly()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IDispatcher, Unify.Application.Messaging.Dispatcher>();

        using ServiceProvider provider = services.BuildServiceProvider();
        var dispatcher = provider.GetRequiredService<IDispatcher>();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => dispatcher.SendAsync(new GetHealthQuery()));
    }
}
