local cacheKey = KEYS[1]
local startIndex = tonumber(ARGV[1])
local pageSize = tonumber(ARGV[2])

local startRank = startIndex
local endRank = startRank + pageSize

local allMembers = redis.call('zrevrange', cacheKey, 0, endRank - 1)

local result = {}

local previousScore = nil
local currentRank = 0
local resultIndex = 0

for i, memberIdentifier in ipairs(allMembers) do
    local memberScore = redis.call('zscore', cacheKey, memberIdentifier)

    if memberScore ~= previousScore then
        currentRank = currentRank + 1
        previousScore = memberScore
    end

    if i >= startIndex then
        result[resultIndex] = {memberIdentifier, currentRank}

        resultIndex = resultIndex + 1
    end
end

return result
