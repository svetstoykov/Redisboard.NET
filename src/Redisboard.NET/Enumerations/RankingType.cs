namespace Redisboard.NET.Enumerations;

public enum RankingType
{
    /// <summary>
    /// Specifies the default ranking type for Redis sorted sets.
    /// </summary>
    /// <remarks>
    /// The default ranking type in Redis sorted sets is lexicographical ordering.
    /// Members are ordered by score first, and if there are ties in scores, they are then ordered lexicographically.
    /// There is no skipping in the records ranking.
    /// Example:  [{John, 100}, {Micah, 100}, {Alex, 99}, {Tim, 1}], the ranks are [1, 2, 3, 4]
    /// </remarks>
    Default = 1,

    /// <summary>
    /// Specifies the Dense Rank ranking type.
    /// </summary>
    /// <remarks>
    /// In dense ranking, items that compare equally receive the same ranking number, and the next items receive the immediately following ranking number.
    /// If there are ties in scores, all tied members receive the same rank,
    /// and the next member will have the next rank after the number of tied members.
    /// Example: For scores [100, 100, 50, 40, 40], the ranks would be [1, 1, 2, 3, 3]
    /// </remarks>
    DenseRank = 2,

    /// <summary>
    /// Specifies the Standard Competition ranking type.
    /// </summary>
    /// <remarks>
    /// In Standard Competition Ranking (SCR)  items that compare equal receive the same ranking number, and then a gap is left in the ranking numbers.
    /// The number of ranking numbers that are left out in this gap is one less than the number of items that compared equal.
    /// and if there are ties in scores, all tied members receive the same rank.
    /// The next member's rank is incremented by the number of tied members plus one.
    /// Example: For scores [100, 100, 50, 40, 40], the ranks would be [1, 1, 3, 4, 4]
    /// </remarks>
    StandardCompetition = 3,

    /// <summary>
    /// Specifies the Modified Competition ranking type.
    /// </summary>
    /// <remarks>
    /// Modified Competition Ranking (MCR) is done by leaving the gaps in the ranking numbers before the sets of equal-ranking items (rather than after them as in standard competition ranking).
    /// If there are ties in scores, all tied members receive the same rank,
    /// and the next member's rank is incremented by one regardless of the number of tied members.
    /// For scores [100, 100, 50, 40, 40], the ranks would be [1, 3, 3, 5, 5]   
    /// </remarks>
    ModifiedCompetition = 4
}