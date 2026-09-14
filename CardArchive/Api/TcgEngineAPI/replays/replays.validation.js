const zlib = require('node:zlib');

const MAX_COMPRESSED = 12 * 1024 * 1024;
exports.validate = function (body) {
    if (!body || typeof body.matchId !== 'string' || !/^[\w-]{1,128}$/.test(body.matchId)
        || !Array.isArray(body.players) || body.players.length !== 2
        || body.players.some(p => typeof p !== 'string' || !p || p.length > 100)
        || typeof body.payload !== 'string' || body.payload.length > MAX_COMPRESSED * 4 / 3
        || !/^[A-Za-z0-9+/]+={0,2}$/.test(body.payload)) throw new Error('Invalid replay envelope');
    const bytes = Buffer.from(body.payload, 'base64');
    if (bytes.length > MAX_COMPRESSED) throw new Error('Replay too large');
    const record = JSON.parse(zlib.gunzipSync(bytes, { maxOutputLength: 128 * 1024 * 1024 }));
    if (record.formatVersion !== 1 || record.matchId !== body.matchId
        || JSON.stringify(record.players) !== JSON.stringify(body.players)
        || !record.initialState || !Array.isArray(record.events) || record.events.length > 1000000
        || typeof record.finalHash !== 'string' || !record.finalHash)
        throw new Error('Invalid replay record');
    for (const event of record.events) {
        if (!['state', 'event', 'interaction', 'random'].includes(event.kind)) throw new Error('Unknown replay entry');
        if (event.kind === 'state' && !Array.isArray(event.changes)) throw new Error('Invalid state changes');
        if (event.kind === 'event' && typeof event.packet !== 'string') throw new Error('Invalid event packet');
        if (event.kind === 'interaction' && !event.interaction) throw new Error('Invalid interaction');
    }
    return bytes;
};

exports.retain = (ids, matchId, limit) => [matchId, ...ids.filter(id => id !== matchId)].slice(0, limit);
