namespace Redisboard.NET.Enumerations;

/// <summary>Defines the condition under which a proposed score is accepted.</summary>
/// <remarks>Higher and lower compare caller-visible scores, independently of ranking rules. Equal scores fail both comparisons.</remarks>
public enum ScoreUpdateCondition
{
    /// <summary>Accepts every write.</summary>
    Always = 0,
    /// <summary>Accepts writes only for complete existing entities.</summary>
    Exists = 1,
    /// <summary>Accepts writes only when neither ranking nor metadata exists.</summary>
    NotExists = 2,
    /// <summary>Accepts strictly higher scores, or inserts an absent entity when metadata is supplied.</summary>
    Higher = 3,
    /// <summary>Accepts strictly lower scores, or inserts an absent entity when metadata is supplied.</summary>
    Lower = 4
}
