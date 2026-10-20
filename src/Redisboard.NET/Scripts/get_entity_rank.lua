local leaderboard = KEYS[1]
local uniqueScores = KEYS[2]
local member = ARGV[1]
local ranking = tonumber(ARGV[2])
local score = redis.call('ZSCORE', leaderboard, member)

if not score then return false end
if ranking == 2 then
    return redis.call('ZRANK', uniqueScores, score) + 1
elseif ranking == 3 then
    return redis.call('ZCOUNT', leaderboard, '-inf', '(' .. score) + 1
else
    return redis.call('ZCOUNT', leaderboard, '-inf', score)
end
