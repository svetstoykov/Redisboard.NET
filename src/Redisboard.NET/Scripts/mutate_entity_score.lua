local leaderboard = KEYS[1]
local uniqueScores = KEYS[2]
local metadata = KEYS[3]
local member = ARGV[1]
local operation = ARGV[2]
local value = tonumber(ARGV[3])
local condition = tonumber(ARGV[4])
local ranking = tonumber(ARGV[5])
local direction = tonumber(ARGV[7])
local previous = redis.call('ZSCORE', leaderboard, member)
local hasMetadata = redis.call('HEXISTS', metadata, member) == 1

-- Never create or modify a partial legacy record.
if (previous and not hasMetadata) or (not previous and hasMetadata) then
    return {0, false, false}
end
if not previous and operation ~= 'add' then
    return {0, false, false}
end

local nextScore = value
if operation == 'increment' then
    nextScore = (tonumber(previous) * direction) + value
end
if nextScore ~= nextScore or nextScore == math.huge or nextScore == -math.huge then
    return {-1}
end

local applied = condition == 0
    or (condition == 1 and previous ~= false)
    or (condition == 2 and previous == false)
    or (condition == 3 and (previous == false or nextScore > (tonumber(previous) * direction)))
    or (condition == 4 and (previous == false or nextScore < (tonumber(previous) * direction)))

local score = previous
if applied then
    redis.call('ZADD', leaderboard, string.format('%.17g', nextScore * direction), member)
    score = redis.call('ZSCORE', leaderboard, member)
    redis.call('ZADD', uniqueScores, score, score)
    if previous and previous ~= score and redis.call('ZCOUNT', leaderboard, previous, previous) == 0 then
        redis.call('ZREMRANGEBYSCORE', uniqueScores, previous, previous)
    end
    if operation == 'add' then
        redis.call('HSET', metadata, member, ARGV[6])
    end
end

if not score then return {0, false, false} end
local rank
if ranking == 1 then
    rank = redis.call('ZRANK', leaderboard, member) + 1
elseif ranking == 2 then
    rank = redis.call('ZRANK', uniqueScores, score) + 1
elseif ranking == 3 then
    rank = redis.call('ZCOUNT', leaderboard, '-inf', '(' .. score) + 1
else
    rank = redis.call('ZCOUNT', leaderboard, '-inf', score)
end
return {applied and 1 or 0, score, rank}
