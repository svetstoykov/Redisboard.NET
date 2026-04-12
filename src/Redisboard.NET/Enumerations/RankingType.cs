namespace Redisboard.NET.Enumerations;

/// <summary>
/// Defines ranking rules used when converting scores into visible rank numbers.
/// </summary>
/// <remarks>
/// Different values control how ties affect returned ranks and whether gaps appear in ranking sequence.
/// </remarks>
public enum RankingType
{
    /// <summary>
    /// Assigns unique consecutive ranks and breaks score ties lexicographically.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This mode matches Redis sorted set ordering. Equal scores do not share a rank.
    /// </para>
    /// <para>
    /// Example: scores [100, 100, 99, 1] produce ranks [1, 2, 3, 4].
    /// </para>
    /// </remarks>
    Default = 1,

    /// <summary>
    /// Assigns same rank to tied scores without leaving gaps between distinct ranks.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Equal scores share one rank and next distinct score increments rank by one.
    /// </para>
    /// <para>
    /// Example: scores [100, 100, 50, 40, 40] produce ranks [1, 1, 2, 3, 3].
    /// </para>
    /// </remarks>
    DenseRank = 2,

    /// <summary>
    /// Assigns same rank to tied scores and leaves gap after each tied group.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is standard competition ranking, also known as 1224 ranking.
    /// </para>
    /// <para>
    /// Example: scores [100, 100, 50, 40, 40] produce ranks [1, 1, 3, 4, 4].
    /// </para>
    /// </remarks>
    StandardCompetition = 3,

    /// <summary>
    /// Assigns same rank to tied scores and leaves gap before each tied group.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is modified competition ranking, also known as 1334 ranking.
    /// </para>
    /// <para>
    /// Example: scores [100, 100, 50, 40, 40] produce ranks [2, 2, 3, 5, 5].
    /// </para>
    /// </remarks>
    ModifiedCompetition = 4
}
