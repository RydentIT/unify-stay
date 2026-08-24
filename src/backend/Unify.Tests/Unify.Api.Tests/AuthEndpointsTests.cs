using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Unify.Api.Tests.Infrastructure;
using Unify.Application.Features.Authentication.Login;
using Unify.Application.Features.Authentication.RegisterUser;
using Unify.Application.Features.Authentication.RequestPasswordReset;

namespace Unify.Api.Tests;

/// <summary>
/// SCAFFOLD expectations. The routes, model binding, validation pipeline and problem-details
/// mapping are real and asserted here; the handlers behind them return 501 until the auth
/// module lands. Update the 501 assertions - do not delete the tests - as each one is built.
/// </summary>
public sealed class AuthEndpointsTests : IClassFixture<UnifyApiFactory>
{
    private readonly UnifyApiFactory _factory;

    public AuthEndpointsTests(UnifyApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Register_reaches_its_handler_and_reports_not_implemented()
    {
        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            new Uri("/api/v1/auth/register", UriKind.Relative),
            new RegisterUserCommand("someone@example.com", "correct-horse-battery", "Someone", true));

        Assert.Equal(HttpStatusCode.NotImplemented, response.StatusCode);
    }

    [Fact]
    public async Task Register_rejects_an_invalid_body_with_a_field_level_problem_document()
    {
        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            new Uri("/api/v1/auth/register", UriKind.Relative),
            new RegisterUserCommand("not-an-email", "short", "", false));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        JsonElement errors = body.GetProperty("errors");

        Assert.True(errors.TryGetProperty(nameof(RegisterUserCommand.Email), out _));
        Assert.True(errors.TryGetProperty(nameof(RegisterUserCommand.Password), out _));
        Assert.True(errors.TryGetProperty(nameof(RegisterUserCommand.AcceptedTerms), out _));
    }

    [Fact]
    public async Task Login_reaches_its_handler_and_reports_not_implemented()
    {
        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            new Uri("/api/v1/auth/login", UriKind.Relative),
            new LoginCommand("someone@example.com", "correct-horse-battery"));

        Assert.Equal(HttpStatusCode.NotImplemented, response.StatusCode);
    }

    [Fact]
    public async Task Forgot_password_reaches_its_handler_and_reports_not_implemented()
    {
        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            new Uri("/api/v1/auth/forgot-password", UriKind.Relative),
            new RequestPasswordResetCommand("someone@example.com"));

        Assert.Equal(HttpStatusCode.NotImplemented, response.StatusCode);
    }
}
