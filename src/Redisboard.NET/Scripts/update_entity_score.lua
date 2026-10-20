local sortedSetCacheKey = KEYS[1]
local uniqueScoresSortedSetCacheKey = KEYS[2]
local memberId = ARGV[1]
local newScore = ARGV[2]

local currentScoreStr = redis.call('ZSCORE', sortedSetCacheKey, memberId)
if currentScoreStr == false or redis.call('HEXISTS', KEYS[3], memberId) == 0 then
    return false
end

redis.call('ZADD', sortedSetCacheKey, newScore, memberId)
local newScoreStr = redis.call('ZSCORE', sortedSetCacheKey, memberId)
redis.call('ZADD', uniqueScoresSortedSetCacheKey, newScoreStr, newScoreStr)

if currentScoreStr ~= false and currentScoreStr ~= newScoreStr then
    local countWithCurrentScore = redis.call('ZCOUNT', sortedSetCacheKey, currentScoreStr, currentScoreStr)
    if countWithCurrentScore == 0 then
        redis.call('ZREMRANGEBYSCORE', uniqueScoresSortedSetCacheKey, currentScoreStr, currentScoreStr)
    end
end


return redis.call('ZRANK', uniqueScoresSortedSetCacheKey, newScoreStr) + 1
