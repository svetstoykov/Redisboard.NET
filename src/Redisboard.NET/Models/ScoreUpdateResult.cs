namespace Redisboard.NET.Models;

/// <summary>Represents the outcome and ranking snapshot of an atomic score write.</summary>
/// <remarks>
/// Score and rank describe the same Redis script execution, including rejected writes. Both are
/// <see langword="null"/> when no complete entity exists. Applied means the write condition passed;
/// an accepted write of the same score still reports <see langword="true"/>.
/// </remarks>
public sealed record ScoreUpdateResult
{
    /// <summary>Initializes an immutable atomic write outcome.</summary>
    /// <param name="applied">Whether the write was accepted.</param>
    /// <param name="score">Caller-visible score, or <see langword="null"/> for an absent or incomplete entity.</param>
    /// <param name="rank">One-based rank, or <see langword="null"/> for an absent or incomplete entity.</param>
    public ScoreUpdateResult(bool applied, double? score, long? rank)
    {
        Applied = applied;
        Score = score;
        Rank = rank;
    }

    /// <summary>Gets whether the write was accepted.</summary>
    public bool Applied { get; }

    /// <summary>Gets the caller-visible score after the operation, or <see langword="null"/> when no complete entity exists.</summary>
    public double? Score { get; }

    /// <summary>Gets the one-based rank after the operation, or <see langword="null"/> when no complete entity exists.</summary>
    public long? Rank { get; }
}
