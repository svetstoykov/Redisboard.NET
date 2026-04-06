namespace Redisboard.NET.Enumerations;

/// <summary>
/// Defines the ranking strategy used to assign ranks to members in a leaderboard.
/// </summary>
/// <remarks>
/// Different ranking types determine how tied scores are handled and how
/// subsequent ranks are assigned. The choice of ranking type affects both
/// the rank values returned and whether gaps appear in the ranking sequence.
/// </remarks>
public enum RankingType
{
    /// <summary>
    /// Lexicographic ordering with no rank skipping.
    /// </summary>
    /// <remarks>
    /// Members are ordered by score first, then lexicographically for ties.
    /// Every member receives a unique, consecutive rank regardless of ties.
    /// <para>
    /// <b>Example:</b> Scores [100, 100, 99, 1] produce ranks [1, 2, 3, 4].
    /// </para>
    /// </remarks>
    Default = 1,

    /// <summary>
    /// Tied members share the same rank; the next distinct score receives the immediately following rank.
    /// </summary>
    /// <remarks>
    /// No gaps are introduced in the ranking sequence. Equal scores always
    /// produce equal ranks, and the next rank increments by exactly one.
    /// <para>
    /// <b>Example:</b> Scores [100, 100, 50, 40, 40] produce ranks [1, 1, 2, 3, 3].
    /// </para>
    /// </remarks>
    DenseRank = 2,

    /// <summary>
    /// Tied members share the lowest applicable rank; a gap follows to account for the number of tied members.
    /// </summary>
    /// <remarks>
    /// Also known as "1224" ranking. All tied members receive the same rank,
    /// and the next member's rank jumps by the number of tied entries,
    /// leaving a gap after each set of ties.
    /// <para>
    /// <b>Example:</b> Scores [100, 100, 50, 40, 40] produce ranks [1, 1, 3, 4, 4].
    /// </para>
    /// </remarks>
    StandardCompetition = 3,

    /// <summary>
    /// Tied members share the highest applicable rank; a gap precedes each set of tied members.
    /// </summary>
    /// <remarks>
    /// Also known as "1334" ranking. Instead of placing the gap after tied
    /// entries (as in standard competition), the gap appears before them.
    /// Each tied group receives the rank equal to the position of the last
    /// member in that group.
    /// <para>
    /// <b>Example:</b> Scores [100, 100, 50, 40, 40] produce ranks [2, 2, 3, 5, 5].
    /// </para>
    /// </remarks>
    ModifiedCompetition = 4
}