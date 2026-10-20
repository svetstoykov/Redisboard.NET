local leaderboard = KEYS[1]
local uniqueScores = KEYS[2]
local metadata = KEYS[3]
local query = ARGV[1]
local ranking = tonumber(ARGV[2])
local startIndex = ARGV[3]
local endIndex = ARGV[4]
local members

if query == 'keys' then
    members = {}
    for i = 3, #ARGV do
        local score = redis.call('ZSCORE', leaderboard, ARGV[i])
        if score then
            table.insert(members, ARGV[i])
            table.insert(members, score)
        end
    end
elseif query == 'neighbors' then
    local targetIndex = redis.call('ZRANK', leaderboard, startIndex)
    if not targetIndex then return {} end
    local offset = tonumber(endIndex)
    startIndex = math.max(targetIndex - offset, 0)
    endIndex = targetIndex + offset
    members = redis.call('ZRANGE', leaderboard, startIndex, endIndex, 'WITHSCORES')
elseif query == 'score' then
    members = redis.call('ZRANGEBYSCORE', leaderboard, startIndex, endIndex, 'WITHSCORES')
    if #members == 0 then return {} end
    startIndex = redis.call('ZRANK', leaderboard, members[1])
else
    -- Keep caller-supplied rank bounds as strings to preserve 64-bit integer precision.
    members = redis.call('ZRANGE', leaderboard, startIndex, endIndex, 'WITHSCORES')
end

local result = {}
local previousScore
local previousRank

for i = 1, #members, 2 do
    local member = members[i]
    local score = members[i + 1]
    local rank
    if ranking == 1 then
        if query == 'keys' then
            rank = redis.call('ZRANK', leaderboard, member) + 1
        else
            rank = tonumber(startIndex) + (i + 1) / 2
        end
    elseif query ~= 'keys' and score == previousScore then
        rank = previousRank
    elseif ranking == 2 then
        rank = redis.call('ZRANK', uniqueScores, score) + 1
    elseif ranking == 3 then
        rank = redis.call('ZCOUNT', leaderboard, '-inf', '(' .. score) + 1
    else
        rank = redis.call('ZCOUNT', leaderboard, '-inf', score)
    end
    previousScore = score
    previousRank = rank
    table.insert(result, { member, rank, score, redis.call('HGET', metadata, member) })
end

if query == 'page' then return { result, redis.call('ZCARD', leaderboard) } end
return result
