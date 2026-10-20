local changed = 0
local deadline
if ARGV[1] ~= 'persist' then
    local now = redis.call('TIME')
    deadline = now[1] * 1000 + math.floor(now[2] / 1000) + tonumber(ARGV[1])
end
for _, key in ipairs(KEYS) do
    if deadline then
        changed = changed + redis.call('PEXPIREAT', key, deadline)
    else
        changed = changed + redis.call('PERSIST', key)
    end
end
return changed > 0 and 1 or 0
