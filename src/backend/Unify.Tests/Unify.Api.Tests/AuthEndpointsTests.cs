using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using NSubstitute;
using NSubstitute.ClearExtensions;
using Unify.Api.Tests.Infrastructure;
using Unify.Domain.Users;

namespace Unify.Api.Tests;

/// <summary>
/// The HTTP surface: routing, validation shape, problem-details bodies, and the authorization
/// policy that keeps a limited-scope token out of everything except change-password.
/// </summary>
public sealed class AuthEndpointsTests : IClassFixture<UnifyApiFactory>
{
    private readonly UnifyApiFactory _factory;

    public AuthEndpointsTests(UnifyApiFactory factory)
    {
        _factory = factory;

        // Reset shared substitutes: the fixture is per-class, so state would otherwise leak
        // between tests in confusing ways.
        _factory.Users.ClearSubstitute();
        _factory.PasswordHasher.ClearSubstitute();
        _factory.LoginAttempts.ClearSubstitute();
    }

    private static User VerifiedStudent(string email = "student@example.com", bool mustChangePassword = false)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow.AddDays(-5);

        var user = new User(
            Guid.CreateVersion7(),
            "Test",
            "Student",
            Email.Create(email),
            "hashed-password",
            emailVerified: true,
            mustChangePassword: mustChangePassword,
            UserStatus.Active,
            now,
            now);

        user.AddRole(RoleName.Student);
        return user;
    }

    [Fact]
    public async Task Register_rejects_an_invalid_body_with_field_level_errors()
    {
        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            new Uri("/api/auth/register", UriKind.Relative),
            new
            {
                firstName = "",
                lastName = "",
                email = "not-an-email",
                password = "short",
                contactNumber = "",
                acceptedTerms = false,
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        JsonElement errors = body.GetProperty("errors");

        Assert.True(errors.TryGetProperty("FirstName", out _));
        Assert.True(errors.TryGetProperty("LastName", out _));
        Assert.True(errors.TryGetProperty("Email", out _));
        Assert.True(errors.TryGetProperty("Password", out _));
        Assert.True(errors.TryGetProperty("ContactNumber", out _));
        Assert.True(errors.TryGetProperty("AcceptedTerms", out _));
    }

    /// <summary>REG-003 surfaces as a 409, not a validation error.</summary>
    [Fact]
    public async Task Register_reports_conflict_for_an_address_already_in_use()
    {
        _factory.Users
            .GetByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>())
            .Returns(VerifiedStudent("taken@example.com"));

        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            new Uri("/api/auth/register", UriKind.Relative),
            new
            {
                firstName = "Someone",
                lastName = "Person",
                email = "taken@example.com",
                password = "correct-horse-battery",
                contactNumber = "+94711234567",
                acceptedTerms = true,
            });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    /// <summary>BR-LOG-001 asserted at the HTTP boundary, where it actually matters.</summary>
    [Fact]
    public async Task Login_returns_an_identical_body_for_unknown_address_and_wrong_password()
    {
        using HttpClient client = _factory.CreateClient();

        _factory.Users.GetByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns((User?)null);

        using HttpResponseMessage unknown = await client.PostAsJsonAsync(
            new Uri("/api/auth/login", UriKind.Relative),
            new { email = "nobody@example.com", password = "some-password-value" });

        string unknownBody = await unknown.Content.ReadAsStringAsync();

        User user = VerifiedStudent();
        _factory.Users.GetByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns(user);
        _factory.PasswordHasher.Verify(Arg.Any<string>(), Arg.Any<string?>()).Returns(false);

        using HttpResponseMessage wrongPassword = await client.PostAsJsonAsync(
            new Uri("/api/auth/login", UriKind.Relative),
            new { email = user.Email.Value, password = "wrong-password-value" });

        string wrongBody = await wrongPassword.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);

        // Compare the parts that describe the failure; traceId legitimately differs per request.
        Assert.Equal(TitleAndCode(unknownBody), TitleAndCode(wrongBody));
    }

    [Fact]
    public async Task Login_succeeds_and_returns_a_full_scope_token()
    {
        User user = VerifiedStudent();
        _factory.Users.GetByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns(user);
        _factory.PasswordHasher.Verify("correct-password", user.PasswordHash).Returns(true);

        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            new Uri("/api/auth/login", UriKind.Relative),
            new { email = user.Email.Value, password = "correct-password" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.False(body.GetProperty("mustChangePassword").GetBoolean());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("accessToken").GetString()));
        Assert.Equal(JsonValueKind.String, body.GetProperty("refreshToken").ValueKind);
    }

    /// <summary>LOG-012/LOG-013: the forced-reset response carries no refresh token.</summary>
    [Fact]
    public async Task Login_with_a_forced_reset_returns_a_limited_token_and_no_refresh_token()
    {
        User user = VerifiedStudent(mustChangePassword: true);
        _factory.Users.GetByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns(user);
        _factory.PasswordHasher.Verify("correct-password", user.PasswordHash).Returns(true);

        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            new Uri("/api/auth/login", UriKind.Relative),
            new { email = user.Email.Value, password = "correct-password" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(body.GetProperty("mustChangePassword").GetBoolean());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("refreshToken").ValueKind);
        Assert.Empty(body.GetProperty("roles").EnumerateArray());
    }

    /// <summary>LOG-004.</summary>
    [Fact]
    public async Task Login_is_forbidden_for_an_unverified_account()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow.AddDays(-1);

        var user = new User(
            Guid.CreateVersion7(), "Unverified", "Person", Email.Create("unverified@example.com"),
            "hashed-password", emailVerified: false, mustChangePassword: false,
            UserStatus.Active, now, now);

        user.AddRole(RoleName.Student);

        _factory.Users.GetByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns(user);
        _factory.PasswordHasher.Verify(Arg.Any<string>(), Arg.Any<string?>()).Returns(true);

        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            new Uri("/api/auth/login", UriKind.Relative),
            new { email = user.Email.Value, password = "correct-password" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("auth.login.email_not_verified", body.GetProperty("code").GetString());
    }

    /// <summary>FPW-003 / BR-FPW-002 at the HTTP boundary: both answers are 202.</summary>
    [Fact]
    public async Task Forgot_password_accepts_both_known_and_unknown_addresses_identically()
    {
        using HttpClient client = _factory.CreateClient();

        _factory.Users.GetByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns((User?)null);

        using HttpResponseMessage unknown = await client.PostAsJsonAsync(
            new Uri("/api/auth/forgot-password", UriKind.Relative),
            new { email = "nobody@example.com" });

        _factory.Users
            .GetByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>())
            .Returns(VerifiedStudent("known@example.com"));

        using HttpResponseMessage known = await client.PostAsJsonAsync(
            new Uri("/api/auth/forgot-password", UriKind.Relative),
            new { email = "known@example.com" });

        Assert.Equal(HttpStatusCode.Accepted, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, known.StatusCode);
        Assert.Equal(
            await unknown.Content.ReadAsStringAsync(),
            await known.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Protected_routes_reject_an_anonymous_caller()
    {
        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(new Uri("/api/profile", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// LOG-013 / BR-LOG-008 at the transport layer: a limited-scope token is refused everywhere
    /// except the change-password route, which accepts it.
    /// </summary>
    [Fact]
    public async Task A_limited_scope_token_is_refused_by_profile_but_accepted_by_change_password()
    {
        User user = VerifiedStudent(mustChangePassword: true);
        _factory.Users.GetByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns(user);
        _factory.Users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        _factory.PasswordHasher.Verify("correct-password", user.PasswordHash).Returns(true);

        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage login = await client.PostAsJsonAsync(
            new Uri("/api/auth/login", UriKind.Relative),
            new { email = user.Email.Value, password = "correct-password" });

        JsonElement loginBody = await login.Content.ReadFromJsonAsync<JsonElement>();
        string limitedToken = loginBody.GetProperty("accessToken").GetString()!;

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", limitedToken);

        using HttpResponseMessage profile = await client.GetAsync(new Uri("/api/profile", UriKind.Relative));

        // Blocked from the rest of the application...
        Assert.Equal(HttpStatusCode.Forbidden, profile.StatusCode);

        // ...but allowed through the one route that lets the user recover.
        using HttpResponseMessage changePassword = await client.PostAsJsonAsync(
            new Uri("/api/auth/change-password-required", UriKind.Relative),
            new { newPassword = "" });

        Assert.NotEqual(HttpStatusCode.Forbidden, changePassword.StatusCode);
        Assert.NotEqual(HttpStatusCode.Unauthorized, changePassword.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, changePassword.StatusCode);
    }

    [Fact]
    public async Task Verify_email_reports_an_unrecognised_token_distinctly()
    {
        _factory.VerificationTokens
            .GetByHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((Domain.Authentication.EmailVerificationToken?)null);

        using HttpClient client = _factory.CreateClient();

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            new Uri("/api/auth/verify-email", UriKind.Relative),
            new { token = "nonsense-token" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("auth.email.invalid_token", body.GetProperty("code").GetString());
    }

    private static (string? Title, string? Code) TitleAndCode(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        return (
            root.TryGetProperty("title", out JsonElement title) ? title.GetString() : null,
            root.TryGetProperty("code", out JsonElement code) ? code.GetString() : null);
    }
}
