using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using Redisboard.NET.Attributes;
using Redisboard.NET.Exceptions;
using Redisboard.NET.Interfaces;
using StackExchange.Redis;

namespace Redisboard.NET.Helpers;

/// <summary>
/// Caches compiled expression-tree delegates for the
/// <see cref="LeaderboardKeyAttribute"/> and <see cref="LeaderboardScoreAttribute"/>
/// properties of a given entity type.
/// All lookups are performed once per closed generic type via the CLR's
/// static-per-T guarantee — no dictionary needed.
/// </summary>
internal static class EntityTypeAccessor<TEntity>
    where TEntity : ILeaderboardEntity
{
    private static readonly (
        Func<TEntity, RedisValue> KeyGetter,
        Action<TEntity, RedisValue> KeySetter,
        Func<TEntity, double> ScoreGetter,
        Action<TEntity, double> ScoreSetter
    ) Accessors = ResolveAccessors();

    internal static RedisValue GetKey(TEntity entity)
    {
        return Accessors.KeyGetter(entity);
    }

    internal static void SetKey(TEntity entity, RedisValue key)
    {
        Accessors.KeySetter(entity, key);
    }

    internal static double GetScore(TEntity entity)
    {
        return Accessors.ScoreGetter(entity);
    }

    internal static void SetScore(TEntity entity, double score)
    {
        Accessors.ScoreSetter(entity, score);
    }

    private static (
        Func<TEntity, RedisValue> KeyGetter,
        Action<TEntity, RedisValue> KeySetter,
        Func<TEntity, double> ScoreGetter,
        Action<TEntity, double> ScoreSetter
    ) ResolveAccessors()
    {
        var type = typeof(TEntity);

        var allProperties = type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .ToList();

        var keyProp = ResolveSingleProperty<LeaderboardKeyAttribute>(type, allProperties);
        var scoreProp = ResolveSingleProperty<LeaderboardScoreAttribute>(type, allProperties);

        ValidateKeyPropertyType(keyProp);
        ValidateScorePropertyType(scoreProp);

        return (
            BuildKeyGetter(keyProp),
            BuildKeySetter(keyProp),
            BuildScoreGetter(scoreProp),
            BuildScoreSetter(scoreProp)
        );
    }

    // Compiled delegates

    /// <summary>
    /// Compiles: (TEntity e) => e.Key converted to RedisValue
    /// Handles string, Guid, int, long, and RedisValue property types with zero boxing.
    /// </summary>
    private static Func<TEntity, RedisValue> BuildKeyGetter(PropertyInfo prop)
    {
        var param = Expression.Parameter(typeof(TEntity), "e");
        var access = Expression.Property(param, prop);
        var t = prop.PropertyType;

        Expression body;
        if (t == typeof(RedisValue))
        {
            body = access;
        }
        else if (t == typeof(string))
        {
            // (RedisValue)(e.Key ?? "") — implicit string->RedisValue conversion
            var coalesce = Expression.Coalesce(access, Expression.Constant(string.Empty));
            body = Expression.Convert(coalesce, typeof(RedisValue));
        }
        else if (t == typeof(Guid))
        {
            // (RedisValue)e.Key.ToString()
            var toString = Expression.Call(access, typeof(Guid).GetMethod(nameof(Guid.ToString), Type.EmptyTypes)!);
            body = Expression.Convert(toString, typeof(RedisValue));
        }
        else if (t == typeof(int))
        {
            // (RedisValue)(int)e.Key — implicit int->RedisValue conversion
            body = Expression.Convert(access, typeof(RedisValue));
        }
        else if (t == typeof(long))
        {
            // (RedisValue)(long)e.Key — implicit long->RedisValue conversion
            body = Expression.Convert(access, typeof(RedisValue));
        }
        else
        {
            throw new LeaderboardConfigurationException(
                $"Unsupported key type '{t.Name}' on '{prop.DeclaringType!.FullName}.{prop.Name}'.");
        }

        return Expression.Lambda<Func<TEntity, RedisValue>>(body, param).Compile();
    }

    /// <summary>
    /// Compiles: (TEntity e, RedisValue v) => e.Key = convert(v)
    /// Directly assigns the converted value without boxing.
    /// </summary>
    private static Action<TEntity, RedisValue> BuildKeySetter(PropertyInfo prop)
    {
        var entityParam = Expression.Parameter(typeof(TEntity), "e");
        var valueParam = Expression.Parameter(typeof(RedisValue), "v");
        var t = prop.PropertyType;

        Expression converted;
        if (t == typeof(RedisValue))
        {
            converted = valueParam;
        }
        else if (t == typeof(string))
        {
            // v.ToString() — RedisValue.ToString() returns the underlying string
            var toStringMethod = typeof(RedisValue).GetMethod(nameof(RedisValue.ToString), Type.EmptyTypes)!;
            converted = Expression.Call(valueParam, toStringMethod);
        }
        else if (t == typeof(Guid))
        {
            // Guid.Parse(v.ToString())
            var toStringMethod = typeof(RedisValue).GetMethod(nameof(RedisValue.ToString), Type.EmptyTypes)!;
            var asString = Expression.Call(valueParam, toStringMethod);
            var parseMethod = typeof(Guid).GetMethod(nameof(Guid.Parse), new[] { typeof(string) })!;
            converted = Expression.Call(parseMethod, asString);
        }
        else if (t == typeof(int))
        {
            // (int)v — explicit RedisValue->int conversion
            converted = Expression.Convert(valueParam, typeof(int));
        }
        else if (t == typeof(long))
        {
            // (long)v — explicit RedisValue->long conversion
            converted = Expression.Convert(valueParam, typeof(long));
        }
        else
        {
            throw new LeaderboardConfigurationException(
                $"Unsupported key type '{t.Name}' on '{prop.DeclaringType!.FullName}.{prop.Name}'.");
        }

        var assign = Expression.Assign(Expression.Property(entityParam, prop), converted);
        return Expression.Lambda<Action<TEntity, RedisValue>>(assign, entityParam, valueParam).Compile();
    }

    /// <summary>
    /// Compiles: (TEntity e) => (double)e.Score
    /// Widens int/long/float to double at the IL level — no boxing, no Convert.ToDouble.
    /// </summary>
    private static Func<TEntity, double> BuildScoreGetter(PropertyInfo prop)
    {
        var param = Expression.Parameter(typeof(TEntity), "e");
        var access = Expression.Property(param, prop);

        // Expression.Convert emits the correct widening IL (conv.r8, etc.)
        var asDouble = prop.PropertyType == typeof(double)
            ? (Expression)access
            : Expression.Convert(access, typeof(double));

        return Expression.Lambda<Func<TEntity, double>>(asDouble, param).Compile();
    }

    /// <summary>
    /// Compiles: (TEntity e, double s) => e.Score = (T)s
    /// Narrows double to the target type at the IL level — no boxing.
    /// </summary>
    private static Action<TEntity, double> BuildScoreSetter(PropertyInfo prop)
    {
        var entityParam = Expression.Parameter(typeof(TEntity), "e");
        var scoreParam = Expression.Parameter(typeof(double), "s");

        Expression converted = prop.PropertyType == typeof(double)
            ? (Expression)scoreParam
            : Expression.Convert(scoreParam, prop.PropertyType);

        var assign = Expression.Assign(Expression.Property(entityParam, prop), converted);
        return Expression.Lambda<Action<TEntity, double>>(assign, entityParam, scoreParam).Compile();
    }

    // Validation

    private static PropertyInfo ResolveSingleProperty<TAttribute>(Type type, List<PropertyInfo> allProperties)
        where TAttribute : Attribute
    {
        var matches = allProperties
            .Where(p => p.GetCustomAttribute<TAttribute>() != null)
            .ToList();

        var attrName = typeof(TAttribute).Name;

        if (matches.Count == 0)
            throw new LeaderboardConfigurationException(
                $"Type '{type.FullName}' has no property decorated with [{attrName}]. " +
                $"Exactly one property must carry this attribute.");

        if (matches.Count > 1)
            throw new LeaderboardConfigurationException(
                $"Type '{type.FullName}' has {matches.Count} properties decorated with [{attrName}] " +
                $"({string.Join(", ", matches.Select(p => p.Name))}). Exactly one is allowed.");

        return matches[0];
    }

    private static void ValidateKeyPropertyType(PropertyInfo prop)
    {
        var t = prop.PropertyType;
        if (t != typeof(string) && t != typeof(Guid) && t != typeof(RedisValue)
            && t != typeof(int) && t != typeof(long))
            throw new LeaderboardConfigurationException(
                $"Property '{prop.DeclaringType!.FullName}.{prop.Name}' is decorated with [{nameof(LeaderboardKeyAttribute)}] " +
                $"but its type '{t.Name}' is not supported. Supported types: string, Guid, int, long, RedisValue.");
    }

    private static void ValidateScorePropertyType(PropertyInfo prop)
    {
        var t = prop.PropertyType;
        if (t != typeof(double) && t != typeof(float) && t != typeof(int) && t != typeof(long))
            throw new LeaderboardConfigurationException(
                $"Property '{prop.DeclaringType!.FullName}.{prop.Name}' is decorated with [{nameof(LeaderboardScoreAttribute)}] " +
                $"but its type '{t.Name}' is not supported. Supported types: double, float, int, long.");
    }

}