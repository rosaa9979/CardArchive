const Auth = require('../authorization/auth.tool');
const config = require('../config');

// A client header is not proof of editor identity. Bypass is an explicit local
// development capability, never a way to authenticate against a remote API.
exports.canBypass = (req, enabled = config.replay_local_tool_bypass, environment = process.env.NODE_ENV) => {
    const peer = req.socket && req.socket.remoteAddress;
    return enabled === true && environment === 'development'
        && ['127.0.0.1', '::1', '::ffff:127.0.0.1'].includes(peer)
        && req.headers['x-cardarchive-replay-tool'] === 'editor'
        && !req.headers.origin && !req.headers.forwarded && !req.headers['x-forwarded-for'];
};

exports.authorizeUpload = (req, res, next) => {
    if (exports.canBypass(req)) return next();
    return Auth.isValidJWT(req, res, () => Auth.isPermissionLevel(config.permissions.SERVER)(req, res, next));
};
