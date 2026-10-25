const StatsController = require('./stats.controller');
const AuthTool = require('../authorization/auth.tool');
const config = require('../config');

const ADMIN = config.permissions.ADMIN; //Stats contain user data, admin only

exports.route = function (app) {

    //List available stat modules (id, title, description)
    app.get('/stats', [
        AuthTool.isValidJWT,
        AuthTool.isPermissionLevel(ADMIN),
        StatsController.ListStats
    ]);

    //Get one stat's columns + rows
    app.get('/stats/:statId', [
        AuthTool.isValidJWT,
        AuthTool.isPermissionLevel(ADMIN),
        StatsController.GetStat
    ]);
};
