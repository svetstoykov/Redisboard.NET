local sortedSetCacheKey = KEYS[1]
local uniqueScoresSortedSetCacheKey = KEYS[2]
local startIndex = tonumber(ARGV[1])
local pageSize = tonumber(ARGV[2])

local endIndex = startIndex + pageSize - 1

local zrangeResult = redis.call('ZRANGE', sortedSetCacheKey, startIndex, endIndex, 'WITHSCORES')

local result = {}

for i = 1, #zrangeResult, 2 do
    local memberIdentifier = zrangeResult[i]
    local memberScore = zrangeResult[i + 1]

    local memberUniqueRank = redis.call('ZRANK', uniqueScoresSortedSetCacheKey, tostring(memberScore))

    table.insert(result, { memberIdentifier, memberUniqueRank + 1, memberScore })
end

return result
