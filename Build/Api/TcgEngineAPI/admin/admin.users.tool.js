const crypto = require('crypto');
const config = require('../config');
const UserModel = require('../users/users.model');
const UserTool = require('../users/users.tool');
const CardModel = require('../cards/cards.model');
const MatchModel = require('../matches/matches.model');
const MarketModel = require('../market/market.model');
const Activity = require('../activity/activity.model');
const SessionTool = require('../authorization/session.tool');
const AdminItemsTool = require('./admin.items.tool');
const TransactionTool = require('../tools/transaction.tool');

//User lookup and account actions (ban, kick, password, delete) of the admin tool
var AdminUsersTool = {};

AdminUsersTool.page_size = 50;
AdminUsersTool.match_limit = 20;
AdminUsersTool.activity_limit = 50;

const list_fields = "username permission_level coins elo matches victories defeats last_login_time last_online_time account_create_time ban_time ban_until";

function escapeRegex(text) {
    return text.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

//Online while the last heartbeat is recent enough, a crashed or killed client turns offline after the margin
AdminUsersTool.isOnline = (user, now) => {
    if (!user.last_online_time)
        return false;
    var max_age = config.heartbeat_interval * config.online_margin * 1000;
    return (now || Date.now()) - new Date(user.last_online_time).getTime() <= max_age;
};

//An exact id, an exact email, or part of the username
function getSearchFilter(q) {
    if (!q)
        return {};
    if (/^[0-9a-f]{24}$/i.test(q))
        return {_id: q};
    if (q.includes("@"))
        return {email: new RegExp("^" + escapeRegex(q) + "$", "i")};
    return {username: new RegExp(escapeRegex(q), "i")};
}

function toListRow(user, now) {
    return {
        id: user._id.toHexString(),
        username: user.username,
        permission_level: user.permission_level,
        coins: user.coins,
        elo: user.elo,
        matches: user.matches,
        victories: user.victories,
        defeats: user.defeats,
        last_login_time: user.last_login_time,
        last_online_time: user.last_online_time,
        account_create_time: user.account_create_time,
        online: AdminUsersTool.isOnline(user, now),
        banned: SessionTool.isBanned(user, now),
    };
}

//Most recently online first, so online users are always on top
AdminUsersTool.search = async (q, page) => {
    var size = AdminUsersTool.page_size;
    var users = await UserModel.UserModel.find(getSearchFilter(q), list_fields)
        .sort({last_online_time: -1, _id: 1})
        .skip(page * size)
        .limit(size + 1);

    var now = Date.now();
    return {
        users: users.slice(0, size).map((user) => toListRow(user, now)),
        page: page,
        has_more: users.length > size,
    };
};

// ----- Detail -----------

async function getCardTitles(tids) {
    var titles = {};
    if (tids.size === 0)
        return titles;
    var cards = await CardModel.getAll({tid: {$in: [...tids]}});
    for (var card of cards)
        titles[card.tid] = card.title || "";
    return titles;
}

function toCardEntry(entry, titles) {
    return {
        tid: entry.tid,
        title: titles[entry.tid] || "",
        variant: entry.variant || "",
        quantity: entry.quantity || 1,
    };
}

function toDeck(deck, titles, catalog) {
    var cards = (deck.cards || []).filter((c) => c && c.tid).map((c) => toCardEntry(c, titles));
    return {
        issues: AdminItemsTool.getDeckIssues(deck, catalog),
        tid: deck.tid,
        title: deck.title || "",
        hero: deck.hero && deck.hero.tid ? toCardEntry(deck.hero, titles) : null,
        clubs: (deck.clubs || []).filter((c) => c && c.tid).map((c) => toCardEntry(c, titles)),
        cards: cards,
        card_count: cards.reduce((total, c) => total + c.quantity, 0),
    };
}

function getMatchResult(match, username) {
    if (!match.completed)
        return "ongoing";
    if (!match.winner)
        return "none"; //Tie or canceled by the server
    return match.winner === username ? "win" : "lose";
}

function toMatch(match, username) {
    return {
        tid: match.tid,
        opponent: (match.players || []).filter((p) => p !== username).join(", "),
        result: getMatchResult(match, username),
        mode: match.mode,
        ranked: match.ranked,
        start: match.start,
        end: match.end,
    };
}

//Activities of the user and admin actions on the account (logged under the admin name), most recent first
async function getActivities(user) {
    var limit = AdminUsersTool.activity_limit;
    var own = await Activity.Activity.find({username: user.username}).sort({timestamp: -1}).limit(limit);
    var admin = await Activity.Activity.find({"data.user_id": user.id}).sort({timestamp: -1}).limit(limit);
    return own.concat(admin)
        .sort((a, b) => new Date(b.timestamp).getTime() - new Date(a.timestamp).getTime())
        .slice(0, limit);
}

//Every collection of one user, card ids resolved to their names. Null when the user does not exist.
AdminUsersTool.getDetail = async (id) => {
    var user = /^[0-9a-f]{24}$/i.test(id) ? await UserModel.getById(id) : null;
    if (!user)
        user = await UserModel.getByUsername(id);
    if (!user)
        return null;

    var tids = new Set();
    for (var deck of user.decks || []) {
        if (deck.hero && deck.hero.tid)
            tids.add(deck.hero.tid);
        for (var entry of (deck.clubs || []).concat(deck.cards || []))
            if (entry && entry.tid)
                tids.add(entry.tid);
    }
    for (var card of user.cards || [])
        tids.add(card.tid);
    var titles = await getCardTitles(tids);
    var catalog = await AdminItemsTool.loadCatalog();

    var matches = await MatchModel.listByPlayer(user.username, AdminUsersTool.match_limit);
    var activities = await getActivities(user);

    var profile = user.deleteSecrets();
    for (var key of ["cards", "packs", "decks", "replay_match_ids", "friends", "friends_requests"])
        delete profile[key];

    return {
        user: profile,
        online: AdminUsersTool.isOnline(user),
        banned: SessionTool.isBanned(user),
        manageable: AdminUsersTool.canManage(user),
        friend_count: (user.friends || []).length,
        decks: (user.decks || []).filter((d) => d && d.tid).map((d) => toDeck(d, titles, catalog)),
        cards: (user.cards || []).map((c) => toCardEntry(c, titles))
            .sort((a, b) => (a.title || a.tid).localeCompare(b.title || b.tid)),
        packs: (user.packs || []).map((p) => ({tid: p.tid, quantity: p.quantity})),
        matches: matches.map((m) => toMatch(m, user.username)),
        activities: activities.map((a) => a.toObj()),
    };
};

// ----- Account actions -----------

AdminUsersTool.ban_reason_max = 200;
AdminUsersTool.ban_days_max = 3650;
AdminUsersTool.temp_password_length = 10;

//Only player accounts, so an admin can never lock out another admin or a game server
AdminUsersTool.canManage = (user) => {
    return user.permission_level < config.permissions.SERVER;
};

//Ends every session of the user now: access tokens signed before are refused, refresh tokens stop matching
function revokeSessions(user, now) {
    user.session_revoke_time = now;
    user.refresh_key = "";
}

async function saveRestrictions(user) {
    var saved = await user.save(); //Throws, the cache must never differ from the database
    SessionTool.update(saved);
    return saved;
}

//days 0 is permanent, banning a banned user replaces its ban
AdminUsersTool.ban = async (user, days, reason, admin_username) => {
    var now = new Date();
    user.ban_time = now;
    user.ban_until = days > 0 ? new Date(now.getTime() + days * 24 * 3600 * 1000) : null;
    user.ban_reason = reason;
    user.ban_by = admin_username;
    revokeSessions(user, now);
    return await saveRestrictions(user);
};

AdminUsersTool.unban = async (user) => {
    user.ban_time = null;
    user.ban_until = null;
    user.ban_reason = "";
    user.ban_by = "";
    return await saveRestrictions(user);
};

AdminUsersTool.kick = async (user) => {
    revokeSessions(user, new Date());
    return await saveRestrictions(user);
};

//Easy to read and type, without 0/o or 1/l
function generatePassword(length) {
    var characters = "abcdefghijkmnpqrstuvwxyz23456789";
    var bytes = crypto.randomBytes(length);
    var result = "";
    for (var i = 0; i < length; i++)
        result += characters.charAt(bytes[i] % characters.length);
    return result;
}

//Empty password generates a temporary one, returned once and never stored in clear
AdminUsersTool.setPassword = async (user, password) => {
    var generated = !password;
    if (generated)
        password = generatePassword(AdminUsersTool.temp_password_length);

    UserTool.setUserPassword(user, password);
    revokeSessions(user, new Date());
    await saveRestrictions(user);
    return {password: generated ? password : "", generated: generated};
};

//Removes the account, its market offers and its name from the friend lists of other users.
//Matches, replays and activities keep the name as history.
AdminUsersTool.remove = async (user) => {
    var summary = {
        username: user.username,
        email: user.email,
        coins: user.coins,
        elo: user.elo,
        matches: user.matches,
        cards: (user.cards || []).reduce((total, c) => total + (c.quantity || 0), 0),
        decks: (user.decks || []).length,
        account_create_time: user.account_create_time,
        market_offers: 0,
    };

    await TransactionTool.withTx(async (session) => {
        var result = await UserModel.UserModel.deleteOne({_id: user._id}, {session});
        if (result.deletedCount !== 1)
            throw new Error("User already deleted");
        summary.market_offers = await MarketModel.removeBySeller(user.username, {session});
        await UserModel.UserModel.updateMany(
            {$or: [{friends: user.username}, {friends_requests: user.username}]},
            {$pull: {friends: user.username, friends_requests: user.username}},
            {session});
    });

    SessionTool.setDeleted(user.id);
    return summary;
};

module.exports = AdminUsersTool;
