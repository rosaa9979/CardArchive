const config = require('../config.js');
const UserModel = require('../users/users.model');

//Sessions closed by an admin (ban, kick, password set, account delete).
//Access tokens are only signed, so the users whose tokens must be refused before they expire
//are kept in memory and checked on every authenticated request (isValidJWT).
//Rebuilt from Mongo on startup, a deleted user is also refused by the heartbeat once the document is gone.

var SessionTool = {};

var closed = new Map(); //userId -> {revoke_sec, ban_until_ms, ban_reason, deleted}

const KickMessages = {
    banned: "Account banned",
    revoked: "Session closed by an admin",
    deleted: "Account deleted",
};

//A ban is active while ban_time is set and ban_until is empty (permanent) or in the future
SessionTool.isBanned = (user, now) => {
    if (!user.ban_time)
        return false;
    return !user.ban_until || new Date(user.ban_until).getTime() > (now || Date.now());
};

SessionTool.getBanKick = (user) => {
    return {reason: "banned", until: user.ban_until ? new Date(user.ban_until).toISOString() : "", message: user.ban_reason || ""};
};

SessionTool.sendKick = (res, kick) => {
    return res.status(403).send({error: KickMessages[kick.reason], kick: kick});
};

SessionTool.load = async () => {
    var since = new Date(Date.now() - config.jwt_expiration * 1000);
    var users = await UserModel.UserModel.find(
        {$or: [{ban_time: {$ne: null}}, {session_revoke_time: {$gte: since}}]},
        "ban_time ban_until ban_reason session_revoke_time");

    closed.clear();
    for (var user of users)
        SessionTool.update(user);
    if (closed.size > 0)
        console.log("Closed sessions loaded: " + closed.size + " users");
};

//Called with the saved user after every change of its ban or session_revoke_time
SessionTool.update = (user) => {
    var entry = {
        revoke_sec: user.session_revoke_time ? Math.floor(new Date(user.session_revoke_time).getTime() / 1000) : 0,
        ban_until_ms: 0,
        ban_reason: "",
        banned: SessionTool.isBanned(user),
        deleted: false,
    };
    if (entry.banned) {
        entry.ban_until_ms = user.ban_until ? new Date(user.ban_until).getTime() : 0;
        entry.ban_reason = user.ban_reason || "";
    }

    var id = user._id.toHexString();
    if (entry.revoke_sec || entry.banned)
        closed.set(id, entry);
    else
        closed.delete(id);
};

SessionTool.setDeleted = (userId) => {
    closed.set(userId, {revoke_sec: 0, ban_until_ms: 0, ban_reason: "", banned: false, deleted: true});
};

//Kick info when the token must be refused, null when it is still valid
SessionTool.check = (token) => {
    var entry = closed.get(token.userId);
    if (!entry)
        return null;

    var now = Date.now();
    if (entry.deleted)
        return {reason: "deleted", until: "", message: ""};

    var ban_active = entry.banned && (!entry.ban_until_ms || entry.ban_until_ms > now);
    if (ban_active)
        return {reason: "banned", until: entry.ban_until_ms ? new Date(entry.ban_until_ms).toISOString() : "", message: entry.ban_reason};

    //Tokens signed in the same second as the revoke are kept, they come from the login that follows it
    if (token.iat < entry.revoke_sec)
        return {reason: "revoked", until: "", message: ""};

    //Ban over and every token older than the revoke expired: nothing left to refuse
    if (now / 1000 - entry.revoke_sec > config.jwt_expiration)
        closed.delete(token.userId);
    else if (entry.banned)
        entry.banned = false;
    return null;
};

module.exports = SessionTool;
