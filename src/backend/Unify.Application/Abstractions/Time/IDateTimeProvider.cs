namespace Unify.Application.Abstractions.Time;

/// <summary>Injected clock, so time-dependent handlers stay testable.</summary>
public interface IDateTimeProvider
{
    DateTimeOffset UtcNow { get; }
}
