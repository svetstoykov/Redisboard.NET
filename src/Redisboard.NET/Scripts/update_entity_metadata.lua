local memberId = ARGV[1]
if redis.call('ZSCORE', KEYS[1], memberId) == false or redis.call('HEXISTS', KEYS[2], memberId) == 0 then
    return false
end

return redis.call('HSET', KEYS[2], memberId, ARGV[2])
