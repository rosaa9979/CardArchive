const test = require('node:test');
const assert = require('node:assert/strict');
const zlib = require('node:zlib');
const { validate, retain } = require('./replays.validation');

function envelope(change = {}) {
    const record = { formatVersion: 1, matchId: 'match-1', players: ['a', 'b'], initialState: { key: 'root' },
        events: [{ kind: 'random', randomResult: 'next:1:6:4' }], finalHash: 'hash', ...change };
    return { matchId: 'match-1', players: ['a', 'b'], payload: zlib.gzipSync(JSON.stringify(record)).toString('base64') };
}
test('accepts compressed omniscient recording', () => assert.ok(Buffer.isBuffer(validate(envelope()))));
test('rejects mismatched participants, match and versions', () => {
    for (const change of [{ players: ['a', 'c'] }, { matchId: 'other' }, { formatVersion: 2 }, { finalHash: '' }])
        assert.throws(() => validate(envelope(change)));
});
test('rejects corrupt payload and unknown events', () => {
    assert.throws(() => validate({ ...envelope(), payload: 'AAAA' }));
    assert.throws(() => validate(envelope({ events: [{ kind: 'execute' }] })));
});
test('latest ten references, deduplication and independent player retention', () => {
    let ids = [];
    for (let i = 0; i < 12; i++) ids = retain(ids, 'm' + i, 10);
    assert.deepEqual(ids, ['m11','m10','m9','m8','m7','m6','m5','m4','m3','m2']);
    assert.deepEqual(retain(ids, 'm11', 10), ids);
    const opponent = retain([], 'm1', 10);
    assert.ok(!ids.includes('m1') && opponent.includes('m1'));
});
