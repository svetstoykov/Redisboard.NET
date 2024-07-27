local sortedSetCacheKey = KEYS[1]
local uniqueScoresSortedSetCacheKey = KEYS[2]
local memberId = ARGV[1]
local newScore = tonumber(ARGV[2])

-- Get the current score of the member
local currentScore = redis.call('ZSCORE', sortedSetCacheKey, memberId)

-- Update the main sorted set
redis.call('ZADD', sortedSetCacheKey, newScore, memberId)

-- If the score has changed, update the unique scores set
if currentScore ~= newScore then
    if currentScore then
        -- Check if any other member has the current score
        local countWithCurrentScore = redis.call('ZCOUNT', sortedSetCacheKey, currentScore, currentScore)
        if countWithCurrentScore == 0 then
            -- If no other member has this score, remove it from the unique scores set
            redis.call('ZREM', uniqueScoresSortedSetCacheKey, currentScore)
        end
    end

    -- Add the new score to the unique scores set
    redis.call('ZADD', uniqueScoresSortedSetCacheKey, newScore, newScore)
end

-- Return the new dense rank
return redis.call('ZRANK', uniqueScoresSortedSetCacheKey, newScore) + 1
