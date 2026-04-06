local sortedSetCacheKey = KEYS[1]
local startIndex = tonumber(ARGV[1])
local pageSize = tonumber(ARGV[2])
local competitionType = tonumber(ARGV[3])

local endIndex = startIndex + pageSize - 1

local membersWithScores = redis.call('ZRANGE', sortedSetCacheKey, startIndex, endIndex, 'WITHSCORES')

local result = {}

for i = 1, #membersWithScores, 2 do
    local memberIdentifier = membersWithScores[i]
    local memberScore = membersWithScores[i + 1]

    local relativeMember

    if competitionType == 3 then
        -- Standard competition ranking: rank = position of the first member with this score
        local first = redis.call('ZRANGEBYSCORE', sortedSetCacheKey, memberScore, memberScore, 'LIMIT', 0, 1)
        relativeMember = first[1]
    elseif competitionType == 4 then
        -- Modified competition ranking: rank = position of the last member with this score
        local last = redis.call('ZREVRANGEBYSCORE', sortedSetCacheKey, memberScore, memberScore, 'LIMIT', 0, 1)
        relativeMember = last[1]
    end

    local memberRank = redis.call('ZRANK', sortedSetCacheKey, relativeMember) + 1

    table.insert(result, { memberIdentifier, memberRank, memberScore })
end

return result
