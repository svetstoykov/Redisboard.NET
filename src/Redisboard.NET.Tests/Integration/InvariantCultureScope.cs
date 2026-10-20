using System.Globalization;

namespace Redisboard.NET.Tests.Integration;

/// <summary>Temporarily selects invariant culture for a test's execution context.</summary>
/// <remarks>
/// Keeps score-index regressions independent of the culture-sensitive parsing tracked by RB-002.
/// <see cref="Dispose"/> restores the culture selected before construction.
/// </remarks>
internal sealed class InvariantCultureScope : IDisposable
{
    private readonly CultureInfo _previousCulture = CultureInfo.CurrentCulture;

    /// <summary>Initializes the scope and selects <see cref="CultureInfo.InvariantCulture"/>.</summary>
    public InvariantCultureScope()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
    }

    /// <summary>Restores the execution context's previous culture.</summary>
    public void Dispose()
    {
        CultureInfo.CurrentCulture = _previousCulture;
    }
}
