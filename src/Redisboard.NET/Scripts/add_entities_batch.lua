local leaderboardKey = KEYS[1]
local uniqueScoreKey = KEYS[2]
local metadataKey = KEYS[3]

for i = 1, #ARGV, 3 do
  local entityKey = ARGV[i]
  local invertedScore = tonumber(ARGV[i + 1])
  local metadata = ARGV[i + 2]

  redis.call('ZADD', leaderboardKey, invertedScore, entityKey)
  redis.call('ZADD', uniqueScoreKey, invertedScore, tostring(invertedScore))
  redis.call('HSET', metadataKey, entityKey, metadata)
end

return 1
