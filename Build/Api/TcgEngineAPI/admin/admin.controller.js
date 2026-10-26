const config = require('../config');
const MaintenanceTool = require('../maintenance/maintenance.tool');
const MessageTool = require('../messages/messages.tool');
const AdminTool = require('./admin.tool');

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

// ----- Audit log -----------

exports.GetAuditLogs = async (req, res) => {
    var limit = Math.min(Math.max(parseInt(req.query.limit) || 200, 1), 1000);
    var logs = await AdminTool.getLogs(limit);
    if (!logs)
        return res.status(500).send({error: "Failed to load the audit log"});
    return res.status(200).send(logs);
};
