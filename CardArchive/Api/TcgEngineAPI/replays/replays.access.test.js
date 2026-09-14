const test = require('node:test');
const assert = require('node:assert/strict');
const { canBypass } = require('./replays.access');

const request = (peer = '127.0.0.1', headers = {}) => ({ socket: { remoteAddress: peer },
    headers: { 'x-cardarchive-replay-tool': 'editor', ...headers } });

test('explicit development mode accepts local editor requests', () => {
    for (const peer of ['127.0.0.1', '::1', '::ffff:127.0.0.1'])
        assert.equal(canBypass(request(peer), true, 'development'), true);
});
test('disabled, unset and production modes require normal authentication', () => {
    assert.equal(canBypass(request(), false, 'development'), false);
    assert.equal(canBypass(request(), true, undefined), false);
    assert.equal(canBypass(request(), true, 'production'), false);
});
test('remote peers, forwarded requests, browser origins and missing marker cannot bypass', () => {
    assert.equal(canBypass(request('192.168.1.10'), true, 'development'), false);
    for (const headers of [{ forwarded: 'for=192.168.1.10' }, { 'x-forwarded-for': '192.168.1.10' },
        { origin: 'https://example.com' }, { 'x-cardarchive-replay-tool': undefined }])
        assert.equal(canBypass(request('127.0.0.1', headers), true, 'development'), false);
});
