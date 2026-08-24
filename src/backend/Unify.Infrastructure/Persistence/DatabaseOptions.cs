using System.ComponentModel.DataAnnotations;

namespace Unify.Infrastructure.Persistence;

/// <summary>
/// Bound from the "Database" configuration section. The connection string itself is never
/// committed - it arrives from the environment (see .env.example / ConnectionStrings__Unify).
/// </summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    [Required(AllowEmptyStrings = false, ErrorMessage =
        "A Postgres connection string is required. Set ConnectionStrings__Unify or Database__ConnectionString.")]
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>Applies pending migrations during host startup. Intended for Development only.</summary>
    public bool RunMigrationsOnStartup { get; set; }

    [Range(1, 300)]
    public int CommandTimeoutSeconds { get; set; } = 30;
}
