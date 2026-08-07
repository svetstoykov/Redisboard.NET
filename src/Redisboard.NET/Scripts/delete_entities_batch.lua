local leaderboardKey = KEYS[1]
local uniqueScoreKey = KEYS[2]
local metadataKey = KEYS[3]
local versionHashKey = KEYS[4]

for i, entityKey in ipairs(ARGV) do
  redis.call('ZREM', leaderboardKey, entityKey)
  redis.call('HDEL', metadataKey, entityKey)
  redis.call('HDEL', versionHashKey, entityKey)

  local remaining = redis.call('ZCOUNT', leaderboardKey, 0, 0)
  if remaining == 0 then
    redis.call('DEL', uniqueScoreKey)
  end
end

return 1
