local sortedSetKey = KEYS[1]
local uniqueScoresKey = KEYS[2]
local versionHashKey = KEYS[3]
local memberId = ARGV[1]
local newScore = tonumber(ARGV[2])
local expectedVersion = tonumber(ARGV[3])

local currentVersionStr = redis.call('HGET', versionHashKey, memberId)
local currentVersion = currentVersionStr and tonumber(currentVersionStr) or 0

if currentVersion ~= expectedVersion then
    return {-1, currentVersion}
end

local currentScoreStr = redis.call('ZSCORE', sortedSetKey, memberId)

redis.call('ZADD', sortedSetKey, newScore, memberId)

if currentScoreStr == false then
    redis.call('ZADD', uniqueScoresKey, newScore, tostring(newScore))
elseif tonumber(currentScoreStr) ~= newScore then
    local countWithCurrentScore = redis.call('ZCOUNT', sortedSetKey, currentScoreStr, currentScoreStr)
    if countWithCurrentScore == 0 then
        redis.call('ZREM', uniqueScoresKey, currentScoreStr)
    end
    redis.call('ZADD', uniqueScoresKey, newScore, tostring(newScore))
end

local newVersion = currentVersion + 1
redis.call('HSET', versionHashKey, memberId, tostring(newVersion))

return {1, newVersion}
