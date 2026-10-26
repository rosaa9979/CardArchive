const jwt = require('jsonwebtoken');
const config = require('../config.js');
const MaintenanceModel = require('./maintenance.model');

//Maintenance state is kept in memory (checked on every request) and saved in Mongo,
//so a pm2 restart reloads it. A scheduled maintenance becomes active by itself at shutdown_at,
//clients receive the shutdown time in advance (heartbeat, login, version) and all leave at the same time.

var MaintenanceTool = {};

const default_data = {
    state: "none",
    shutdown_at: null,
    end_at: null,
    message: "",
    block_match_minutes: 5,
    whitelist: [],
    updated_by: "",
    updated_at: null,
};

var data = Object.assign({}, default_data);

//Requests that answer the maintenance state themselves instead of being blocked
const open_paths = ["/version", "/auth", "/auth/refresh"];

MaintenanceTool.load = async () => {
    var doc = await MaintenanceModel.get();
    data = Object.assign({}, default_data, doc ? doc.toObj() : {});
    if (data.state !== "none")
        console.log("Maintenance loaded: " + data.state + " at " + data.shutdown_at);
};

//Saves and caches new values, throws if Mongo fails so the cache never differs from the database
MaintenanceTool.update = async (values, admin_username) => {
    values.updated_by = admin_username || "";
    values.updated_at = new Date();
    var doc = await MaintenanceModel.save(values);
    data = Object.assign({}, default_data, doc.toObj());
    return data;
};

MaintenanceTool.getData = () => {
    return data;
};

//Public state sent to clients, remain_sec lets them count down without trusting their own clock
MaintenanceTool.getInfo = () => {
    var now = Date.now();
    var shutdown = data.shutdown_at ? new Date(data.shutdown_at).getTime() : 0;
    var state = data.state;
    if (state === "scheduled" && now >= shutdown)
        state = "active";

    var block_at = shutdown - (data.block_match_minutes || 0) * 60 * 1000;
    return {
        state: state,
        shutdown_at: shutdown ? new Date(shutdown).toISOString() : "",
        remain_sec: state === "scheduled" ? Math.max(0, (shutdown - now) / 1000) : 0,
        end_at: data.end_at ? new Date(data.end_at).toISOString() : "",
        message: data.message || "",
        matchmaking_blocked: state === "active" || (state === "scheduled" && now >= block_at),
        block_match_minutes: data.block_match_minutes || 0,
    };
};

MaintenanceTool.isClosed = () => {
    return MaintenanceTool.getInfo().state === "active";
};

//Game servers, admins and whitelisted testers keep access during maintenance
MaintenanceTool.canBypass = (login) => {
    if (!login)
        return false;
    if (parseInt(login.permission_level) >= config.permissions.SERVER)
        return true;
    var username = String(login.username || "").toLowerCase();
    return (data.whitelist || []).some((name) => name.toLowerCase() === username);
};

//Same ops format as the other responses, no messages since every client leaves
MaintenanceTool.sendClosed = (res) => {
    return res.status(503).send({error: "Server maintenance", ops: {maintenance: MaintenanceTool.getInfo(), messages: []}});
};

//Only reads who is calling, the route itself still validates the token
function readToken(req) {
    var authorization = req.headers['authorization'];
    if (!authorization)
        return null;
    try {
        return jwt.verify(authorization, config.jwt_secret);
    } catch (e) {
        return null;
    }
}

MaintenanceTool.blockRequests = (req, res, next) => {
    if (!MaintenanceTool.isClosed())
        return next();
    if (open_paths.includes(req.path))
        return next();
    if (MaintenanceTool.canBypass(readToken(req)))
        return next();
    return MaintenanceTool.sendClosed(res);
};

module.exports = MaintenanceTool;
