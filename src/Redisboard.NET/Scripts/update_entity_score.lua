local sortedSetCacheKey = KEYS[1]
local uniqueScoresSortedSetCacheKey = KEYS[2]
local memberId = ARGV[1]
local newScore = tonumber(ARGV[2])

local currentScoreStr = redis.call('ZSCORE', sortedSetCacheKey, memberId)

redis.call('ZADD', sortedSetCacheKey, newScore, memberId)

if currentScoreStr == false then
    -- New member, just add the score to the unique set
    redis.call('ZADD', uniqueScoresSortedSetCacheKey, newScore, tostring(newScore))
elseif tonumber(currentScoreStr) ~= newScore then
    -- Score changed, maintain the unique scores set
    local countWithCurrentScore = redis.call('ZCOUNT', sortedSetCacheKey, currentScoreStr, currentScoreStr)
    if countWithCurrentScore == 0 then
        redis.call('ZREM', uniqueScoresSortedSetCacheKey, currentScoreStr)
    end

    redis.call('ZADD', uniqueScoresSortedSetCacheKey, newScore, tostring(newScore))
end

return redis.call('ZRANK', uniqueScoresSortedSetCacheKey, tostring(newScore)) + 1
