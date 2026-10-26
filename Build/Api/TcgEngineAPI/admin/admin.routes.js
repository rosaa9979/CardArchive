const AdminController = require('./admin.controller');
const AuthTool = require('../authorization/auth.tool');
const config = require('../config');

const ADMIN = config.permissions.ADMIN; //Admin tool only, every action is written to the audit log

exports.route = function (app) {

    // ADMIN - MAINTENANCE ----------------------

    app.get('/admin/maintenance', [
        AuthTool.isValidJWT,
        AuthTool.isPermissionLevel(ADMIN),
        AdminController.GetMaintenance
    ]);

    //Body: shutdown_at, end_at (optional), message, block_match_minutes
    app.post('/admin/maintenance/schedule', app.post_limiter, [
        AuthTool.isValidJWT,
        AuthTool.isPermissionLevel(ADMIN),
        AdminController.ScheduleMaintenance
    ]);

    app.post('/admin/maintenance/cancel', app.post_limiter, [
        AuthTool.isValidJWT,
        AuthTool.isPermissionLevel(ADMIN),
        AdminController.CancelMaintenance
    ]);

    app.post('/admin/maintenance/open', app.post_limiter, [
        AuthTool.isValidJWT,
        AuthTool.isPermissionLevel(ADMIN),
        AdminController.OpenServer
    ]);

    //Body: usernames
    app.post('/admin/maintenance/whitelist', app.post_limiter, [
        AuthTool.isValidJWT,
        AuthTool.isPermissionLevel(ADMIN),
        AdminController.SetWhitelist
    ]);

    // ADMIN - MESSAGES ----------------------

    app.get('/admin/messages', [
        AuthTool.isValidJWT,
        AuthTool.isPermissionLevel(ADMIN),
        AdminController.GetMessages
    ]);

    //Body: text
    app.post('/admin/messages/send', app.post_limiter, [
        AuthTool.isValidJWT,
        AuthTool.isPermissionLevel(ADMIN),
        AdminController.SendMessage
    ]);

    //Body: id
    app.post('/admin/messages/cancel', app.post_limiter, [
        AuthTool.isValidJWT,
        AuthTool.isPermissionLevel(ADMIN),
        AdminController.CancelMessage
    ]);

    // ADMIN - AUDIT LOG ----------------------

    app.get('/admin/audit', [
        AuthTool.isValidJWT,
        AuthTool.isPermissionLevel(ADMIN),
        AdminController.GetAuditLogs
    ]);
};
