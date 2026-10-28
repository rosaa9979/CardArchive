const config = require('../config');
const MaintenanceTool = require('../maintenance/maintenance.tool');
const MessageTool = require('../messages/messages.tool');
const AdminTool = require('./admin.tool');
const AdminUsersTool = require('./admin.users.tool');
const AdminItemsTool = require('./admin.items.tool');
const UserModel = require('../users/users.model');
const Validator = require('../tools/validator.tool');

const message_max = 500;
const whitelist_max = 50;

function parseDate(value) {
    if (!value || typeof value !== "string")
        return null;
    var date = new Date(value);
    return isNaN(date.getTime()) ? null : date;
}

function getMaintenanceState() {
    var data = MaintenanceTool.getData();
    return {
        info: MaintenanceTool.getInfo(),
        whitelist: data.whitelist || [],
        block_match_minutes: data.block_match_minutes,
        updated_by: data.updated_by,
        updated_at: data.updated_at,
        heartbeat_interval: config.heartbeat_interval,
        min_notice: config.maintenance_min_notice,
        server_time: new Date(),
    };
}

// ----- Maintenance -----------

exports.GetMaintenance = (req, res) => {
    return res.status(200).send(getMaintenanceState());
};

//Body: shutdown_at (ISO date), end_at (ISO date, optional), message, block_match_minutes
exports.ScheduleMaintenance = async (req, res) => {
    var shutdown_at = parseDate(req.body.shutdown_at);
    var end_at = req.body.end_at ? parseDate(req.body.end_at) : null;
    var message = req.body.message || "";
    var block_minutes = Number(req.body.block_match_minutes);

    if (!shutdown_at)
        return res.status(400).send({error: "Invalid shutdown time"});
    if (req.body.end_at && (!end_at || end_at <= shutdown_at))
        return res.status(400).send({error: "Expected end time must be after the shutdown time"});
    if (typeof message !== "string" || message.length > message_max)
        return res.status(400).send({error: "Message must be at most " + message_max + " characters"});
    if (!Number.isInteger(block_minutes) || block_minutes < 0 || block_minutes > 60)
        return res.status(400).send({error: "Matchmaking block must be between 0 and 60 minutes"});

    //Every client must receive the schedule from at least one heartbeat before the shutdown
    var notice_sec = (shutdown_at.getTime() - Date.now()) / 1000;
    if (notice_sec < config.maintenance_min_notice)
        return res.status(400).send({error: "Shutdown must be at least " + Math.ceil(config.maintenance_min_notice / 60) + " minutes from now"});

    if (MaintenanceTool.isClosed())
        return res.status(400).send({error: "Maintenance is already active, open the server first"});

    var values = {state: "scheduled", shutdown_at: shutdown_at, end_at: end_at, message: message, block_match_minutes: block_minutes};
    try {
        await MaintenanceTool.update(values, req.jwt.username);
    } catch (e) {
        console.error("Maintenance schedule error:", e);
        return res.status(500).send({error: "Failed to save the maintenance schedule"});
    }

    await AdminTool.log(req, "maintenance_schedule", values);
    return res.status(200).send(getMaintenanceState());
};

//Only before the shutdown time, an active maintenance is ended with open
exports.CancelMaintenance = async (req, res) => {
    if (MaintenanceTool.getInfo().state !== "scheduled")
        return res.status(400).send({error: "No scheduled maintenance to cancel"});

    try {
        await MaintenanceTool.update({state: "none", shutdown_at: null, end_at: null, message: ""}, req.jwt.username);
    } catch (e) {
        console.error("Maintenance cancel error:", e);
        return res.status(500).send({error: "Failed to cancel the maintenance"});
    }

    await AdminTool.log(req, "maintenance_cancel", {});
    return res.status(200).send(getMaintenanceState());
};

exports.OpenServer = async (req, res) => {
    if (MaintenanceTool.getInfo().state !== "active")
        return res.status(400).send({error: "The server is not in maintenance"});

    try {
        await MaintenanceTool.update({state: "none", shutdown_at: null, end_at: null, message: ""}, req.jwt.username);
    } catch (e) {
        console.error("Maintenance open error:", e);
        return res.status(500).send({error: "Failed to open the server"});
    }

    await AdminTool.log(req, "maintenance_open", {});
    return res.status(200).send(getMaintenanceState());
};

//Body: usernames (array of usernames allowed during maintenance)
exports.SetWhitelist = async (req, res) => {
    var usernames = req.body.usernames;
    if (!Array.isArray(usernames) || usernames.length > whitelist_max)
        return res.status(400).send({error: "Whitelist must be a list of at most " + whitelist_max + " usernames"});

    var list = [];
    for (const name of usernames) {
        if (typeof name !== "string" || name.trim().length === 0 || name.length > 50)
            return res.status(400).send({error: "Invalid username in whitelist"});
        if (!list.some((n) => n.toLowerCase() === name.trim().toLowerCase()))
            list.push(name.trim());
    }

    var previous = MaintenanceTool.getData().whitelist || [];
    try {
        await MaintenanceTool.update({whitelist: list}, req.jwt.username);
    } catch (e) {
        console.error("Maintenance whitelist error:", e);
        return res.status(500).send({error: "Failed to save the whitelist"});
    }

    await AdminTool.log(req, "maintenance_whitelist", {before: previous, after: list});
    return res.status(200).send(getMaintenanceState());
};

// ----- Messages -----------

const message_list_max = 50;

async function getMessagesState() {
    return {
        messages: await MessageTool.getRecent(message_list_max),
        delay: config.message_delay,
        max_length: config.message_max_length,
        server_time: new Date(),
    };
}

exports.GetMessages = async (req, res) => {
    try {
        return res.status(200).send(await getMessagesState());
    } catch (e) {
        console.error("Messages list error:", e);
        return res.status(500).send({error: "Failed to load the messages"});
    }
};

//Body: text
exports.SendMessage = async (req, res) => {
    var text = typeof req.body.text === "string" ? req.body.text.trim() : "";
    if (text.length === 0 || text.length > config.message_max_length)
        return res.status(400).send({error: "Message must be between 1 and " + config.message_max_length + " characters"});

    var message;
    try {
        message = await MessageTool.send(text, req.jwt.username);
    } catch (e) {
        console.error("Message send error:", e);
        return res.status(500).send({error: "Failed to send the message"});
    }

    await AdminTool.log(req, "message_send", {id: message.id, text: message.text, show_at: message.show_at});
    return res.status(200).send(await getMessagesState());
};

//Body: id
exports.CancelMessage = async (req, res) => {
    var id = req.body.id;
    if (typeof id !== "string" || id.length === 0)
        return res.status(400).send({error: "Invalid message id"});

    var message;
    try {
        message = await MessageTool.cancel(id, req.jwt.username);
    } catch (e) {
        console.error("Message cancel error:", e);
        return res.status(500).send({error: "Failed to cancel the message"});
    }
    if (!message)
        return res.status(400).send({error: "No pending message with this id"});

    await AdminTool.log(req, "message_cancel", {id: message.id, text: message.text});
    return res.status(200).send(await getMessagesState());
};

// ----- Users -----------

//Query: q (part of username, exact email or exact id), page
exports.SearchUsers = async (req, res) => {
    var q = typeof req.query.q === "string" ? req.query.q.trim() : "";
    var page = Math.max(parseInt(req.query.page) || 0, 0);
    if (q.length > 100)
        return res.status(400).send({error: "Search text is too long"});

    try {
        var result = await AdminUsersTool.search(q, page);
        result.server_time = new Date();
        return res.status(200).send(result);
    } catch (e) {
        console.error("User search error:", e);
        return res.status(500).send({error: "Failed to search the users"});
    }
};

exports.GetUser = async (req, res) => {
    var detail;
    try {
        detail = await AdminUsersTool.getDetail(req.params.userId);
    } catch (e) {
        console.error("User detail error:", e);
        return res.status(500).send({error: "Failed to load the user"});
    }
    if (!detail)
        return res.status(404).send({error: "User not found"});

    detail.server_time = new Date();
    return res.status(200).send(detail);
};

// ----- User account actions -----------

//Loads the target of an account action, sends the error and returns null when it cannot be changed
async function getManagedUser(req, res) {
    var id = req.params.userId;
    var user = /^[0-9a-f]{24}$/i.test(id) ? await UserModel.getById(id) : null;
    if (!user) {
        res.status(404).send({error: "User not found"});
        return null;
    }
    if (!AdminUsersTool.canManage(user)) {
        res.status(400).send({error: "Admin and game server accounts cannot be changed here"});
        return null;
    }
    return user;
}

//Every action answers the refreshed detail, like GetUser
async function sendDetail(res, user, extra) {
    var detail = await AdminUsersTool.getDetail(user.id);
    if (!detail)
        return res.status(404).send({error: "User not found"});
    detail.server_time = new Date();
    return res.status(200).send(Object.assign(detail, extra || {}));
}

//Body: days (0 is permanent), reason
exports.BanUser = async (req, res) => {
    var days = Number(req.body.days);
    var reason = typeof req.body.reason === "string" ? req.body.reason.trim() : "";
    if (!Number.isInteger(days) || days < 0 || days > AdminUsersTool.ban_days_max)
        return res.status(400).send({error: "Ban duration must be between 0 (permanent) and " + AdminUsersTool.ban_days_max + " days"});
    if (reason.length === 0 || reason.length > AdminUsersTool.ban_reason_max)
        return res.status(400).send({error: "Reason must be between 1 and " + AdminUsersTool.ban_reason_max + " characters"});

    var user = await getManagedUser(req, res);
    if (!user)
        return;

    try {
        user = await AdminUsersTool.ban(user, days, reason, req.jwt.username);
    } catch (e) {
        console.error("User ban error:", e);
        return res.status(500).send({error: "Failed to ban the user"});
    }

    await AdminTool.log(req, "user_ban", {user_id: user.id, username: user.username, days: days, until: user.ban_until, reason: reason});
    return sendDetail(res, user);
};

exports.UnbanUser = async (req, res) => {
    var user = await getManagedUser(req, res);
    if (!user)
        return;
    if (!user.ban_time)
        return res.status(400).send({error: "User is not banned"});

    var previous = {until: user.ban_until, reason: user.ban_reason, by: user.ban_by};
    try {
        user = await AdminUsersTool.unban(user);
    } catch (e) {
        console.error("User unban error:", e);
        return res.status(500).send({error: "Failed to unban the user"});
    }

    await AdminTool.log(req, "user_unban", {user_id: user.id, username: user.username, previous: previous});
    return sendDetail(res, user);
};

//Logs the user out of every device, the next request or heartbeat sends the client back to the login
exports.KickUser = async (req, res) => {
    var user = await getManagedUser(req, res);
    if (!user)
        return;

    try {
        user = await AdminUsersTool.kick(user);
    } catch (e) {
        console.error("User kick error:", e);
        return res.status(500).send({error: "Failed to log out the user"});
    }

    await AdminTool.log(req, "user_kick", {user_id: user.id, username: user.username});
    return sendDetail(res, user);
};

//Body: password (empty generates a temporary one, sent back once)
exports.SetUserPassword = async (req, res) => {
    var password = req.body.password || "";
    if (password && !Validator.validatePassword(password))
        return res.status(400).send({error: "Invalid password"});

    var user = await getManagedUser(req, res);
    if (!user)
        return;

    var result;
    try {
        result = await AdminUsersTool.setPassword(user, password);
    } catch (e) {
        console.error("User password error:", e);
        return res.status(500).send({error: "Failed to change the password"});
    }

    //Never log the password itself
    await AdminTool.log(req, "user_password", {user_id: user.id, username: user.username, generated: result.generated});
    return sendDetail(res, user, {temp_password: result.password});
};

//Body: confirm (the username, typed by the admin)
exports.DeleteUser = async (req, res) => {
    var user = await getManagedUser(req, res);
    if (!user)
        return;
    if (req.body.confirm !== user.username)
        return res.status(400).send({error: "Type the username to confirm the delete"});

    var summary;
    try {
        summary = await AdminUsersTool.remove(user);
    } catch (e) {
        console.error("User delete error:", e);
        return res.status(500).send({error: "Failed to delete the user"});
    }

    await AdminTool.log(req, "user_delete", Object.assign({user_id: user.id}, summary));
    return res.status(200).send({deleted: summary, server_time: new Date()});
};

// ----- User items and decks -----------

//Runs an item or deck change on a managed user, answers the refreshed detail.
//change(user, catalog) returns the audit data; invalid requests throw ActionError and change nothing.
async function runItemsAction(req, res, action, change) {
    var user = await getManagedUser(req, res);
    if (!user)
        return;

    var data;
    try {
        var reason = AdminItemsTool.parseReason(req.body.reason);
        var catalog = await AdminItemsTool.loadCatalog();
        var prepare = change(catalog);
        data = await AdminItemsTool.update(user.id, (target) => prepare(target));
        data.reason = reason;
    } catch (e) {
        if (e instanceof AdminItemsTool.ActionError)
            return res.status(400).send({error: e.message});
        console.error("User " + action + " error:", e);
        return res.status(500).send({error: "Failed to update the user"});
    }

    await AdminTool.log(req, action, Object.assign({user_id: user.id, username: user.username}, data));
    return sendDetail(res, user);
}

exports.GetCatalog = async (req, res) => {
    try {
        return res.status(200).send(await AdminItemsTool.getCatalog());
    } catch (e) {
        console.error("Catalog error:", e);
        return res.status(500).send({error: "Failed to load the catalog"});
    }
};

//Body: deck {title, hero, cards}, answers the deck as it would be saved (clubs derived) and its rule warnings
exports.CheckDeck = async (req, res) => {
    try {
        var catalog = await AdminItemsTool.loadCatalog();
        var deck = AdminItemsTool.parseDeck(req.body.deck, catalog);
        return res.status(200).send({deck: deck, issues: AdminItemsTool.getDeckIssues(deck, catalog)});
    } catch (e) {
        if (e instanceof AdminItemsTool.ActionError)
            return res.status(400).send({error: e.message});
        console.error("Deck check error:", e);
        return res.status(500).send({error: "Failed to check the deck"});
    }
};

//Body: grant {coins, xp, cards, packs, decks} with signed amounts, reason
exports.GrantItems = async (req, res) => {
    return runItemsAction(req, res, "user_grant", (catalog) => {
        var grant = AdminItemsTool.parseGrant(req.body.grant, catalog);
        return (user) => {
            var before = {coins: user.coins, xp: user.xp};
            AdminItemsTool.applyGrant(user, grant, catalog);
            return {grant: grant, before: before, after: {coins: user.coins, xp: user.xp}};
        };
    });
};

//Body: deck {title, hero, cards}, reason. deckId "new" adds a deck.
exports.SaveDeck = async (req, res) => {
    var tid = req.params.deckId === "new" ? "" : req.params.deckId;
    return runItemsAction(req, res, "user_deck_save", (catalog) => {
        var deck = AdminItemsTool.parseDeck(req.body.deck, catalog);
        return (user) => {
            var result = AdminItemsTool.saveDeck(user, tid, deck);
            return {deck: result.deck, previous: result.previous, issues: AdminItemsTool.getDeckIssues(result.deck, catalog)};
        };
    });
};

//Body: reason
exports.DeleteDeck = async (req, res) => {
    return runItemsAction(req, res, "user_deck_delete", () => {
        return (user) => ({deck: AdminItemsTool.removeDeck(user, req.params.deckId)});
    });
};

// ----- Audit log -----------

exports.GetAuditLogs = async (req, res) => {
    var limit = Math.min(Math.max(parseInt(req.query.limit) || 200, 1), 1000);
    var logs = await AdminTool.getLogs(limit);
    if (!logs)
        return res.status(500).send({error: "Failed to load the audit log"});
    return res.status(200).send(logs);
};
