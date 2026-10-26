const Activity = require('../activity/activity.model');

//Audit log of admin actions, stored as activities with an "admin_" type prefix
var AdminTool = {};

AdminTool.audit_prefix = "admin_";

//Never fails the admin request: the action already happened, a missing log is only reported
AdminTool.log = async (req, action, data) => {
    try {
        await Activity.LogActivity(AdminTool.audit_prefix + action, req.jwt.username, data || {});
    } catch (e) {
        console.error("Admin audit log error (" + action + "):", e);
    }
};

AdminTool.getLogs = async (limit) => {
    try {
        var logs = await Activity.Activity.find({type: {$regex: "^" + AdminTool.audit_prefix}})
            .sort({timestamp: -1})
            .limit(limit);
        return logs.map((log) => log.toObj());
    } catch (e) {
        console.error("Admin audit read error:", e);
        return null;
    }
};

module.exports = AdminTool;
