const express = require('express');
const mongoose = require('mongoose');
const crypto = require('node:crypto');
const { authorizeUpload } = require('./replays.access');
const { UserModel: User } = require('../users/users.model');
const { withTx } = require('../tools/transaction.tool');
const config = require('../config');
const { validate, retain } = require('./replays.validation');

const exactUsername = value => new RegExp('^' + value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&') + '$', 'i');

const Replay = mongoose.model('Replays', new mongoose.Schema({
    matchId: { type: String, unique: true, required: true },
    players: [String],
    payload: Buffer,
    digest: String
}));

exports.route = app => {
    // Only uploads need server privileges. Replay reads are public.
    // Registered before the default 100kb parser for compressed match uploads.
    app.post('/replays', app.post_limiter, authorizeUpload, express.json({ limit: '17mb' }), async (req, res) => {
        let bytes;
        try { bytes = validate(req.body); }
        catch (e) { return res.status(400).send({ error: e.message }); }
        const { matchId, players } = req.body;
        const digest = crypto.createHash('sha256').update(bytes).digest('hex');
        const limit = config.replay_limit_per_user;
        try {
            await withTx(async session => {
                const existing = await Replay.findOne({ matchId }).session(session);
                if (existing) {
                    if (existing.digest !== digest) throw new Error('Replay ID already has different content');
                    return; // Idempotent retry must not reorder the user's history.
                }
                const users = [];
                for (const username of players) {
                    const user = await User.findOne({ username: exactUsername(username) }).session(session);
                    if (!user) throw new Error('Replay participant not found');
                    users.push(user);
                }
                if (String(users[0]._id) === String(users[1]._id)) throw new Error('Duplicate participant');
                await Replay.create([{ matchId, players, payload: bytes, digest }], { session });
                const removed = new Set();
                for (const user of users) {
                    const old = user.replay_match_ids || [];
                    const next = retain(old, matchId, limit);
                    old.filter(id => !next.includes(id)).forEach(id => removed.add(id));
                    user.replay_match_ids = next;
                    await user.save({ session });
                }
                for (const id of removed) {
                    if (!await User.exists({ replay_match_ids: id }).session(session))
                        await Replay.deleteOne({ matchId: id }).session(session);
                }
            });
            return res.send({ success: true });
        } catch (e) {
            // A concurrent identical insert can lose the unique-index race.
            const existing = await Replay.findOne({ matchId }).lean();
            if (existing && existing.digest === digest) return res.send({ success: true });
            console.error('Replay save failed:', e.message);
            return res.status(409).send({ error: e.message });
        }
    });
    app.get('/replays/user/:username', async (req, res) => {
        try {
            const user = await User.findOne({ username: exactUsername(req.params.username) }).lean();
            if (!user) return res.status(404).send({ error: 'User not found' });
            return res.send({ ids: user.replay_match_ids || [] });
        } catch { return res.status(500).send({ error: 'Replay list unavailable' }); }
    });
    app.get('/replays/:matchId', async (req, res) => {
        try {
            const replay = await Replay.findOne({ matchId: req.params.matchId }).lean();
            if (!replay) return res.status(404).send({ error: 'Replay not found' });
            return res.send({ matchId: replay.matchId, players: replay.players, payload: replay.payload.toString('base64') });
        } catch { return res.status(500).send({ error: 'Replay unavailable' }); }
    });
};
