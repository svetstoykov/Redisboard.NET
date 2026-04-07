local leaderboardKey = KEYS[1]
local uniqueScoreKey = KEYS[2]
local metadataKey = KEYS[3]

for i = 1, #ARGV do
  local entityKey = ARGV[i]

  redis.call('ZREM', leaderboardKey, entityKey)
  redis.call('ZREM', uniqueScoreKey, entityKey)
  redis.call('HDEL', metadataKey, entityKey)
end

return 1
