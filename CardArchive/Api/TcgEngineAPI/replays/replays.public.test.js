const test = require('node:test');
const assert = require('node:assert/strict');
const express = require('express');
const mongoose = require('mongoose');
const { UserModel: User } = require('../users/users.model');
const { route } = require('./replays.routes');

test('public HTTP reads return history and originals without any authentication', async () => {
    const Replay = mongoose.model('Replays');
    const originalUserFind = User.findOne;
    const originalReplayFind = Replay.findOne;
    let server;
    try {
        User.findOne = () => ({ lean: async () => ({ replay_match_ids: ['match-1'] }) });
        Replay.findOne = ({ matchId }) => ({ lean: async () => matchId === 'match-1'
            ? { matchId, players: ['Alice', 'Bob'], payload: Buffer.from('record') } : null });
        const app = express();
        app.post_limiter = (req, res, next) => next();
        route(app);
        server = await new Promise(resolve => {
            const listener = app.listen(0, '127.0.0.1', () => resolve(listener));
        });
        const base = `http://127.0.0.1:${server.address().port}`;
        for (const headers of [{}, { authorization: 'invalid-token' }]) {
            const list = await fetch(base + '/replays/user/Alice', { headers });
            assert.equal(list.status, 200);
            assert.deepEqual(await list.json(), { ids: ['match-1'] });
            const replay = await fetch(base + '/replays/match-1', { headers });
            assert.equal(replay.status, 200);
            assert.deepEqual(await replay.json(), { matchId: 'match-1', players: ['Alice', 'Bob'], payload: 'cmVjb3Jk' });
        }
        assert.equal((await fetch(base + '/replays/missing')).status, 404);
        assert.equal((await fetch(base + '/replays', { method: 'POST' })).status, 401);
    } finally {
        if (server) await new Promise(resolve => server.close(resolve));
        User.findOne = originalUserFind;
        Replay.findOne = originalReplayFind;
    }
});
