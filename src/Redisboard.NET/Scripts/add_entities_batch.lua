local leaderboardKey = KEYS[1]
local uniqueScoreKey = KEYS[2]
local metadataKey = KEYS[3]

for i = 1, #ARGV, 3 do
  local entityKey = ARGV[i]
  local invertedScore = ARGV[i + 1]
  local metadata = ARGV[i + 2]

  local previousScoreStr = redis.call('ZSCORE', leaderboardKey, entityKey)

  redis.call('ZADD', leaderboardKey, invertedScore, entityKey)
  local currentScoreStr = redis.call('ZSCORE', leaderboardKey, entityKey)
  redis.call('ZADD', uniqueScoreKey, currentScoreStr, currentScoreStr)

  if previousScoreStr ~= false and previousScoreStr ~= currentScoreStr then
    local countWithPreviousScore = redis.call('ZCOUNT', leaderboardKey, previousScoreStr, previousScoreStr)
    if countWithPreviousScore == 0 then
      redis.call('ZREMRANGEBYSCORE', uniqueScoreKey, previousScoreStr, previousScoreStr)
    end
  end

  redis.call('HSET', metadataKey, entityKey, metadata)
end

return 1
