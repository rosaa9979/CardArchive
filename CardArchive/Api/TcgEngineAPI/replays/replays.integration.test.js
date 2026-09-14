const test = require('node:test');
const assert = require('node:assert/strict');
const zlib = require('node:zlib');
const fs = require('node:fs');
const path = require('node:path');
const { MongoMemoryReplSet } = require('mongodb-memory-server');
const mongoose = require('mongoose');
const express = require('express');
const jwt = require('jsonwebtoken');
const config = require('../config');
config.jwt_secret = 'isolated-replay-integration-test';
const { UserModel: User } = require('../users/users.model');
const routes = require('./replays.routes');

function envelope(matchId, players, finalHash = 'hash') {
    const record = { formatVersion: 1, matchId, players, initialState: { key: 'root' },
        events: [{ kind: 'random', randomResult: 'next:1:6:4' }], finalHash };
    return { matchId, players, payload: zlib.gzipSync(JSON.stringify(record)).toString('base64') };
}

test('isolated replica set: permissions, persistence, retention, retries and concurrency', { timeout: 240000 }, async t => {
    let mongo, server;
    try {
        mongo = await MongoMemoryReplSet.create({ replSet: { count: 1 }, instanceOpts: [{ ip: '127.0.0.1' }] });
        await mongoose.connect(mongo.getUri(), { dbName: 'replay_integration' });
        const Replay = mongoose.model('Replays');
        await Promise.all([User.init(), Replay.init()]);
        await User.create(['Alice', 'Bob', 'Carol', 'Dave', 'a.b'].map(username => ({ username, password: 'test' })));
        const app = express();
        app.post_limiter = (req, res, next) => next();
        routes.route(app);
        app.use(express.json());
        server = await new Promise(resolve => { const listener = app.listen(0, '127.0.0.1', () => resolve(listener)); });
        const base = `http://127.0.0.1:${server.address().port}`;
        const token = jwt.sign({ permission_level: config.permissions.SERVER }, config.jwt_secret);
        const login = username => jwt.sign({ username, permission_level: config.permissions.USER }, config.jwt_secret);
        const userToken = login('Alice');
        const request = (url, body, auth = (body ? token : '')) => fetch(base + url, {
            method: body ? 'POST' : 'GET', headers: { 'Content-Type': 'application/json', ...(auth ? { authorization: auth } : {}) },
            ...(body ? { body: JSON.stringify(body) } : {})
        });
        const save = async body => { const response = await request('/replays', body); assert.equal(response.status, 200, await response.text()); };
        const ids = async name => { const response = await request('/replays/user/' + name, null, login(name)); assert.equal(response.status, 200); return (await response.json()).ids; };
        assert.equal((await request('/replays/user/Alice', null, '')).status, 200);
        assert.equal((await request('/replays/user/Alice', null, userToken)).status, 200);
        assert.equal((await request('/replays', envelope('denied', ['Alice', 'Bob']), userToken)).status, 403);
        assert.equal((await request('/replays/user/a.b', null, login('a.b'))).status, 200);
        assert.equal((await request('/replays/user/aXb', null, login('aXb'))).status, 404);
        const shared = envelope('shared', ['Alice', 'Carol']);
        await save(shared);
        assert.equal((await request('/replays/shared', null, login('Dave'))).status, 200);
        assert.equal((await request('/replays/user/Bob', null, userToken)).status, 200);
        const loaded = await (await request('/replays/shared')).json();
        assert.deepEqual(loaded, shared);
        for (let i = 0; i < 12; i++) await save(envelope('ab' + i, ['alice', 'Bob']));
        const latest = Array.from({ length: 10 }, (_, i) => 'ab' + (11 - i));
        assert.deepEqual(await ids('ALICE'), latest);
        assert.deepEqual(await ids('Bob'), latest);
        assert.equal((await request('/replays/ab0')).status, 404);
        assert.equal((await request('/replays/shared')).status, 200);
        await save(envelope('ab5', ['alice', 'Bob']));
        assert.deepEqual(await ids('Alice'), latest);
        assert.equal((await request('/replays', envelope('ab5', ['alice', 'Bob'], 'different'))).status, 409);
        assert.equal((await request('/replays', envelope('missing', ['Alice', 'Nobody']))).status, 409);
        assert.equal((await request('/replays/missing')).status, 404);
        assert.deepEqual(await ids('Alice'), latest);
        for (let i = 0; i < 10; i++) await save(envelope('cd' + i, ['Carol', 'Dave']));
        assert.equal((await request('/replays/shared')).status, 404);
        await Promise.all(Array.from({ length: 4 }, () => save(envelope('concurrent', ['Alice', 'Bob']))));
        await Promise.all(Array.from({ length: 4 }, (_, i) => save(envelope('parallel' + i, ['Alice', 'Bob']))));
        const concurrentIds = await ids('Alice');
        assert.equal(concurrentIds.length, 10);
        assert.equal(new Set(concurrentIds).size, 10);
        assert.deepEqual(await ids('Bob'), concurrentIds);
        for (let i = 0; i < 4; i++) assert.ok(concurrentIds.includes('parallel' + i));
        assert.equal(await Replay.countDocuments({ matchId: 'concurrent' }), 1);
        const references = new Set((await User.find().lean()).flatMap(user => user.replay_match_ids));
        assert.deepEqual(new Set((await Replay.find().lean()).map(record => record.matchId)), references);
        const fixture = path.resolve(__dirname, '../../../Library/ReplayValidation/replay-smoke.json');
        if (fs.existsSync(fixture)) {
            const actual = JSON.parse(fs.readFileSync(fixture, 'utf8'));
            for (const username of actual.players)
                await User.updateOne({ username }, { $setOnInsert: { password: 'test' } }, { upsert: true });
            await save(actual);
            assert.deepEqual(await (await request('/replays/' + actual.matchId, null, login(actual.players[0]))).json(), actual);
            t.diagnostic('Actual Unity match payload survived DB round trip unchanged.');
        }
    } finally {
        if (server) await new Promise(resolve => server.close(resolve));
        await mongoose.disconnect();
        if (mongo) await mongo.stop();
    }
});
