using System.Linq.Expressions;
using System.Reflection;
using Redisboard.NET.Attributes;
using Redisboard.NET.Exceptions;
using Redisboard.NET.Interfaces;
using StackExchange.Redis;

namespace Redisboard.NET.Helpers;

/// <summary>
/// Provides cached, compiled property accessors for the key and score
/// properties of <typeparamref name="TEntity"/>.
/// Accessors are resolved once per closed generic type via the CLR's
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

    /// <summary>
    /// Reflects over <typeparamref name="TEntity"/> to find the properties marked with
    /// <see cref="LeaderboardKeyAttribute"/> and <see cref="LeaderboardScoreAttribute"/>,
    /// validates their types, and compiles expression-tree delegates for each.
    /// </summary>
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

    /// <summary>
    /// Builds a getter that reads the key property and converts it to <see cref="RedisValue"/>
    /// without boxing. Supports string, Guid, int, long, and RedisValue.
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
            // Coalesce to empty string before the implicit string -> RedisValue conversion,
            // so a null key becomes RedisValue.EmptyString rather than RedisValue.Null.
            var coalesce = Expression.Coalesce(access, Expression.Constant(string.Empty));
            body = Expression.Convert(coalesce, typeof(RedisValue));
        }
        else if (t == typeof(Guid))
        {
            // Guid has no implicit RedisValue conversion, so we go through its string form.
            var toString = Expression.Call(access, typeof(Guid).GetMethod(nameof(Guid.ToString), Type.EmptyTypes)!);
            body = Expression.Convert(toString, typeof(RedisValue));
        }
        else if (t == typeof(int))
        {
            body = Expression.Convert(access, typeof(RedisValue));
        }
        else if (t == typeof(long))
        {
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
    /// Builds a setter that converts a <see cref="RedisValue"/> to the key property's
    /// declared type and assigns it directly, without boxing.
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
            var toStringMethod = typeof(RedisValue).GetMethod(nameof(RedisValue.ToString), Type.EmptyTypes)!;
            converted = Expression.Call(valueParam, toStringMethod);
        }
        else if (t == typeof(Guid))
        {
            // Two-step: RedisValue -> string -> Guid.Parse
            var toStringMethod = typeof(RedisValue).GetMethod(nameof(RedisValue.ToString), Type.EmptyTypes)!;
            var asString = Expression.Call(valueParam, toStringMethod);
            var parseMethod = typeof(Guid).GetMethod(nameof(Guid.Parse), new[] { typeof(string) })!;
            converted = Expression.Call(parseMethod, asString);
        }
        else if (t == typeof(int))
        {
            // Uses RedisValue's explicit operator int(RedisValue).
            converted = Expression.Convert(valueParam, typeof(int));
        }
        else if (t == typeof(long))
        {
            // Uses RedisValue's explicit operator long(RedisValue).
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
    /// Builds a getter that reads the score property and widens it to <c>double</c>
    /// via IL-level conversion (e.g. conv.r8). No boxing or <see cref="Convert.ToDouble(object)"/>.
    /// </summary>
    private static Func<TEntity, double> BuildScoreGetter(PropertyInfo prop)
    {
        var param = Expression.Parameter(typeof(TEntity), "e");
        var access = Expression.Property(param, prop);

        // Expression.Convert emits the appropriate widening IL instruction
        // (conv.r8 for int/long, conv.r.un for float, identity for double).
        var asDouble = prop.PropertyType == typeof(double)
            ? (Expression)access
            : Expression.Convert(access, typeof(double));

        return Expression.Lambda<Func<TEntity, double>>(asDouble, param).Compile();
    }

    /// <summary>
    /// Builds a setter that narrows a <c>double</c> to the score property's declared type
    /// via IL-level conversion and assigns it directly, without boxing.
    /// </summary>
    private static Action<TEntity, double> BuildScoreSetter(PropertyInfo prop)
    {
        var entityParam = Expression.Parameter(typeof(TEntity), "e");
        var scoreParam = Expression.Parameter(typeof(double), "s");

        // For non-double types (float, int, long) this emits a narrowing IL instruction.
        // Callers are responsible for ensuring the value fits the target range.
        Expression converted = prop.PropertyType == typeof(double)
            ? (Expression)scoreParam
            : Expression.Convert(scoreParam, prop.PropertyType);

        var assign = Expression.Assign(Expression.Property(entityParam, prop), converted);
        return Expression.Lambda<Action<TEntity, double>>(assign, entityParam, scoreParam).Compile();
    }

    /// <summary>
    /// Finds exactly one property on <paramref name="type"/> carrying <typeparamref name="TAttribute"/>.
    /// Throws if zero or more than one match is found.
    /// </summary>
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