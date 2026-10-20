local sortedSetCacheKey = KEYS[1]
local uniqueScoresSortedSetCacheKey = KEYS[2]
local memberId = ARGV[1]
local newScore = ARGV[2]

local currentScoreStr = redis.call('ZSCORE', sortedSetCacheKey, memberId)

redis.call('ZADD', sortedSetCacheKey, newScore, memberId)
local newScoreStr = redis.call('ZSCORE', sortedSetCacheKey, memberId)

if currentScoreStr ~= false and currentScoreStr ~= newScoreStr then
    local countWithCurrentScore = redis.call('ZCOUNT', sortedSetCacheKey, currentScoreStr, currentScoreStr)
    if countWithCurrentScore == 0 then
        redis.call('ZREMRANGEBYSCORE', uniqueScoresSortedSetCacheKey, currentScoreStr, currentScoreStr)
    end
end

redis.call('ZADD', uniqueScoresSortedSetCacheKey, newScoreStr, newScoreStr)

return redis.call('ZRANK', uniqueScoresSortedSetCacheKey, newScoreStr) + 1
