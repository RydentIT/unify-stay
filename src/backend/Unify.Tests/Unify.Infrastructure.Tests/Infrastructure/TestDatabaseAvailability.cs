using System.Diagnostics;

namespace Unify.Infrastructure.Tests.Infrastructure;

/// <summary>
/// Decides, once per test run, whether a Postgres is reachable for the integration tests.
/// </summary>
public static class TestDatabaseAvailability
{
    /// <summary>Set by CI to point at a Postgres service container.</summary>
    public const string ConnectionStringVariable = "UNIFY_TEST_POSTGRES";

    private static readonly Lazy<string?> ExternalConnectionString = new(() =>
    {
        string? value = Environment.GetEnvironmentVariable(ConnectionStringVariable);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    });

    private static readonly Lazy<bool> DockerAvailable = new(ProbeDocker);

    public static string? ExternalConnectionStringOrNull => ExternalConnectionString.Value;

    public static bool IsAvailable => ExternalConnectionString.Value is not null || DockerAvailable.Value;

    public static string SkipReason =>
        $"No Postgres available: set {ConnectionStringVariable} or start a Docker daemon " +
        "so Testcontainers can provision one.";

    /// <summary>
    /// Asks the Docker CLI whether a daemon is actually running. Testcontainers would fail at
    /// container start otherwise, and a skipped test is far more useful than an opaque failure
    /// on a developer machine with Docker Desktop stopped.
    /// </summary>
    private static bool ProbeDocker()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("docker", "info --format {{.ServerVersion}}")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });

            if (process is null)
            {
                return false;
            }

            if (!process.WaitForExit(TimeSpan.FromSeconds(15)))
            {
                process.Kill(entireProcessTree: true);
                return false;
            }

            return process.ExitCode == 0;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // Docker CLI is not installed at all.
            return false;
        }
    }
}

/// <summary>
/// A <see cref="FactAttribute"/> that skips itself when no Postgres can be reached, so a
/// developer without Docker still gets a green run instead of a wall of red.
/// </summary>
public sealed class RequiresPostgresFactAttribute : FactAttribute
{
    public RequiresPostgresFactAttribute()
    {
        if (!TestDatabaseAvailability.IsAvailable)
        {
            Skip = TestDatabaseAvailability.SkipReason;
        }
    }
}
