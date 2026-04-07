local leaderboardKey = KEYS[1]
local uniqueScoreKey = KEYS[2]
local metadataKey = KEYS[3]

for i = 1, #ARGV do
  local entityKey = ARGV[i]

  local scoreStr = redis.call('ZSCORE', leaderboardKey, entityKey)

  redis.call('ZREM', leaderboardKey, entityKey)
  redis.call('HDEL', metadataKey, entityKey)

  if scoreStr ~= false then
    local countWithScore = redis.call('ZCOUNT', leaderboardKey, scoreStr, scoreStr)
    if countWithScore == 0 then
      redis.call('ZREM', uniqueScoreKey, scoreStr)
    end
  end
end

return 1
