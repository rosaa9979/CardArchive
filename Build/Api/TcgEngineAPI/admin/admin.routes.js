const AdminController = require('./admin.controller');
const AuthTool = require('../authorization/auth.tool');
const config = require('../config');

const ADMIN = config.permissions.ADMIN; //Admin tool only, every change is written to the audit log

exports.route = function (app) {

    // ADMIN - USERS ----------------------

    //Query: q, page
    app.get('/admin/users', [
        AuthTool.isValidJWT,
        AuthTool.isPermissionLevel(ADMIN),
        AdminController.SearchUsers
    ]);

    app.get('/admin/users/:userId', [
        AuthTool.isValidJWT,
        AuthTool.isPermissionLevel(ADMIN),
        AdminController.GetUser
    ]);

    //Body: days (0 is permanent), reason
    app.post('/admin/users/:userId/ban', app.post_limiter, [
        AuthTool.isValidJWT,
        AuthTool.isPermissionLevel(ADMIN),
        AdminController.BanUser
    ]);

    app.post('/admin/users/:userId/unban', app.post_limiter, [
        AuthTool.isValidJWT,
        AuthTool.isPermissionLevel(ADMIN),
        AdminController.UnbanUser
    ]);

    app.post('/admin/users/:userId/kick', app.post_limiter, [
        AuthTool.isValidJWT,
        AuthTool.isPermissionLevel(ADMIN),
        AdminController.KickUser
    ]);

    //Body: password (empty generates a temporary one)
    app.post('/admin/users/:userId/password', app.post_limiter, [
        AuthTool.isValidJWT,
        AuthTool.isPermissionLevel(ADMIN),
        AdminController.SetUserPassword
    ]);

    //Body: confirm (username)
    app.post('/admin/users/:userId/delete', app.post_limiter, [
        AuthTool.isValidJWT,
        AuthTool.isPermissionLevel(ADMIN),
        AdminController.DeleteUser
    ]);

    // ADMIN - ITEMS AND DECKS ----------------------

    app.get('/admin/catalog', [
        AuthTool.isValidJWT,
        AuthTool.isPermissionLevel(ADMIN),
        AdminController.GetCatalog
    ]);

    //Body: deck
    app.post('/admin/decks/check', [
        AuthTool.isValidJWT,
        AuthTool.isPermissionLevel(ADMIN),
        AdminController.CheckDeck
    ]);

    //Body: grant {coins, xp, cards, packs, decks}, reason
    app.post('/admin/users/:userId/grant', app.post_limiter, [
        AuthTool.isValidJWT,
        AuthTool.isPermissionLevel(ADMIN),
        AdminController.GrantItems
    ]);

    //Body: deck {title, hero, cards}, reason. deckId "new" adds a deck
    app.post('/admin/users/:userId/decks/:deckId', app.post_limiter, [
        AuthTool.isValidJWT,
        AuthTool.isPermissionLevel(ADMIN),
        AdminController.SaveDeck
    ]);

    //Body: reason
    app.post('/admin/users/:userId/decks/:deckId/delete', app.post_limiter, [
        AuthTool.isValidJWT,
        AuthTool.isPermissionLevel(ADMIN),
        AdminController.DeleteDeck
    ]);

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
