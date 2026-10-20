local sortedSetCacheKey = KEYS[1]
local uniqueScoresSortedSetCacheKey = KEYS[2]
-- Preserve integer precision beyond the exact range of Lua numbers.
local startIndex = ARGV[1]
local endIndex = ARGV[2]

local zrangeResult = redis.call('ZRANGE', sortedSetCacheKey, startIndex, endIndex, 'WITHSCORES')

local result = {}

for i = 1, #zrangeResult, 2 do
    local memberIdentifier = zrangeResult[i]
    local memberScore = zrangeResult[i + 1]

    local memberUniqueRank = redis.call('ZRANK', uniqueScoresSortedSetCacheKey, memberScore)

    table.insert(result, { memberIdentifier, memberUniqueRank + 1, memberScore })
end

return result
