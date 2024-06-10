local cacheKey = KEYS[1]
local startIndex = tonumber(ARGV[1])
local pageSize = tonumber(ARGV[2])
local competitionType = tonumber(ARGV[3])

local startRank = startIndex
local endRank = startRank + pageSize

local allMembers = redis.call('zrevrange', cacheKey, startRank, endRank - 1)

local result = {}

for i, memberIdentifier in ipairs(allMembers) do

    local memberScore = redis.call('zscore', cacheKey, memberIdentifier)
    local allMembersWithSameScore = redis.call('zrevrangebyscore', cacheKey, memberScore, memberScore, 'limit', 0, -1)

    local memberRank = 0
    local relativeMemberWithSameScore = "";

    if (competitionType == 3) then -- standard competition ranking (SCR)
        
        -- get first member with same score
        relativeMemberWithSameScore = allMembersWithSameScore[1]
    elseif (competitionType == 4) then -- modified competition ranking (MCR)
        
        -- get last member with same score
        relativeMemberWithSameScore = allMembersWithSameScore[#allMembersWithSameScore]
    end

    memberRank = redis.call('zrevrank', cacheKey, relativeMemberWithSameScore) + 1

    result[i] = {memberIdentifier, memberRank}
end

return result
