using System.Text.RegularExpressions;
using Unify.Domain.Common;

namespace Unify.Domain.Users;

/// <summary>
/// A normalised email address. Stored lower-cased so that lookups and the unique index in
/// Postgres agree on what "the same address" means.
/// </summary>
public sealed partial class Email : IEquatable<Email>
{
    public const int MaxLength = 320;

    private Email(string value) => Value = value;

    public string Value { get; }

    public static Email Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException("Email must not be empty.");
        }

        string normalised = value.Trim().ToLowerInvariant();

        if (normalised.Length > MaxLength)
        {
            throw new DomainException($"Email must not exceed {MaxLength} characters.");
        }

        if (!EmailPattern().IsMatch(normalised))
        {
            throw new DomainException("Email is not a valid address.");
        }

        return new Email(normalised);
    }

    public static bool TryCreate(string? value, out Email? email)
    {
        try
        {
            email = Create(value);
            return true;
        }
        catch (DomainException)
        {
            email = null;
            return false;
        }
    }

    public bool Equals(Email? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => Equals(obj as Email);

    public override int GetHashCode() => Value.GetHashCode(StringComparison.Ordinal);

    public override string ToString() => Value;

    /// <summary>
    /// Intentionally permissive: the authoritative check is the verification email, not a
    /// regex. This only rejects input that cannot possibly be an address.
    /// </summary>
    [GeneratedRegex(@"^[^@\s]+@[^@\s.]+(\.[^@\s.]+)+$", RegexOptions.CultureInvariant)]
    private static partial Regex EmailPattern();
}
