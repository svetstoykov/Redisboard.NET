local sortedSetCacheKey = KEYS[1]
local startIndex = tonumber(ARGV[1])
local pageSize = tonumber(ARGV[2])
local competitionType = tonumber(ARGV[3])

local endIndex = startIndex + pageSize

local membersWithScores = redis.call('zrange', sortedSetCacheKey, startIndex, endIndex, 'WITHSCORES')

local result = {}

for i = 1, #membersWithScores, 2 do
    local memberIdentifier = membersWithScores[i]
    local memberScore = membersWithScores[i + 1]

    local allMembersWithSameScore = redis.call('zrangebyscore', sortedSetCacheKey, memberScore, memberScore, 'limit', 0, -1)

    local relativeMemberWithSameScore = "";

    if (competitionType == 3) then -- standard competition ranking (SCR)
        -- get first member with same score
        relativeMemberWithSameScore = allMembersWithSameScore[1]
    elseif (competitionType == 4) then -- modified competition ranking (MCR)
        -- get last member with same score
        relativeMemberWithSameScore = allMembersWithSameScore[#allMembersWithSameScore]
    end

    local memberRank = redis.call('zrank', sortedSetCacheKey, relativeMemberWithSameScore) + 1

    table.insert(result, { memberIdentifier, memberRank })
end

return result
