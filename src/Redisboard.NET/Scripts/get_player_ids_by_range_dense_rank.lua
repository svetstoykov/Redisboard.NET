local sortedSetCacheKey = KEYS[1]
local uniqueScoresSortedSetCacheKey = KEYS[2]
local startIndex = tonumber(ARGV[1])
local pageSize = tonumber(ARGV[2])

local startRank = startIndex
local endRank = startRank + pageSize

local allMembers = redis.call('zrevrange', sortedSetCacheKey, startRank, endRank)

local result = {}

for i, memberIdentifier in ipairs(allMembers) do
    local memberScore = redis.call('zscore', sortedSetCacheKey, memberIdentifier)

    local memberUniqueRank = redis.call('zrevrank', uniqueScoresSortedSetCacheKey, tostring(memberScore))

    result[i] = { memberIdentifier, memberUniqueRank + 1 }
end

return result
