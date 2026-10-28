const jwt = require('jsonwebtoken');
const crypto = require('crypto');

const config = require('../config.js');
const UserModel = require('../users/users.model');
const SessionTool = require('./session.tool');

//-----Validations------

var AuthTool = {};


AuthTool.isValidJWT = (req, res, next) => {

    if (!req.headers['authorization'])
        return res.status(401).send();

    try {
        //Validate access token
        let authorization = req.headers['authorization'];
        req.jwt = jwt.verify(authorization, config.jwt_secret);

        //Validate expiry time
        const nowSeconds = Math.round(Number(new Date()) / 1000);
        const expiration = req.jwt.iat + config.jwt_expiration;
        if(nowSeconds > expiration)
            return res.status(403).send({error: "Expired"});

    } catch (err) {
        return res.status(403).send({error: "Invalid Token"});
    }

    //Banned, kicked or deleted by an admin after the token was signed
    var kick = SessionTool.check(req.jwt);
    if (kick)
        return SessionTool.sendKick(res, kick);

    return next();
};

//Log login results, user is the name sent by the client or the logged user (never log passwords)
AuthTool.logLogin = (req, success, user, reason) => {
    var today = new Date();
    var date = today.getFullYear() +'-'+(today.getMonth()+1)+'-'+today.getDate();
    var time = today.getHours() + ":" + today.getMinutes() + ":" + today.getSeconds();
    var name = String(user || "-").replace(/[\r\n\t]/g, " ").substring(0, 64);
    var type = req.originalUrl === "/auth/refresh" ? "refresh" : "login";
    var result = success ? "success" : "failed (" + reason + ")";
    console.log("[" + date + " " + time + "] Auth " + type + " " + result + ": " + name + " ip " + req.ip);
};

AuthTool.isLoginValid = async(req, res, next) => {

    var login_name = req.body ? (req.body.email || req.body.username) : null;

    if (!req.body || !req.body.password) {
        AuthTool.logLogin(req, false, login_name, "missing password");
        return res.status(400).send({error: 'Invalid params'});
    }

    //Requires EITHER username or email, dont need both
    if (!req.body.email && !req.body.username) {
        AuthTool.logLogin(req, false, login_name, "missing username");
        return res.status(400).send({error: 'Invalid params'});
    }

    var user = null;

    if(req.body.email)
        user = await UserModel.getByEmail(req.body.email);
    else if(req.body.username)
        user = await UserModel.getByUsername(req.body.username);
    if(!user) {
        AuthTool.logLogin(req, false, login_name, "user not found");
        return res.status(404).send({error: "Invalid username or password"});
    }

    let validPass = AuthTool.validatePassword(user, req.body.password);
    if(!validPass) {
        AuthTool.logLogin(req, false, user.username, "wrong password");
        return res.status(400).send({error: 'Invalid username or password'});
    }

    if(user.permission_level <= 0) {
        AuthTool.logLogin(req, false, user.username, "account disabled");
        return res.status(403).send({error: "Your account has been disabled, please contact support!"});
    }

    if(SessionTool.isBanned(user)) {
        AuthTool.logLogin(req, false, user.username, "banned");
        return SessionTool.sendKick(res, SessionTool.getBanKick(user));
    }

    req.login = {
        userId: user.id,
        username: user.username,
        email: user.email,
        permission_level: user.permission_level,
        validation_level: user.validation_level,
        provider: req.body.email ? 'email' : 'username',
    };

    return next();
};

AuthTool.isRefreshValid = async(req, res, next) => {

    if (!req.body || !req.body.refresh_token) {
        AuthTool.logLogin(req, false, null, "missing refresh token");
        return res.status(400).send();
    }

    if (!req.headers['authorization']) {
        AuthTool.logLogin(req, false, null, "missing access token");
        return res.status(401).send();
    }

	if (typeof req.body.refresh_token !== "string") {
        AuthTool.logLogin(req, false, null, "invalid refresh token");
        return res.status(400).send();
    }

    try {
        //Validate access token
        let authorization = req.headers['authorization'];
        req.jwt = jwt.verify(authorization, config.jwt_secret);

        //Validate expiry time
        const nowUnixSeconds = Math.round(Number(new Date()) / 1000);
        const expiration = req.jwt.iat + config.jwt_refresh_expiration;
        if(nowUnixSeconds > expiration) {
            AuthTool.logLogin(req, false, req.jwt.username, "token expired");
            return res.status(403).send({error: "Token Expired"});
        }

        //Validate refresh token
        let refresh_token = req.body.refresh_token;
        let hash = crypto.createHmac('sha512', req.jwt.refresh_key).update(req.jwt.userId + config.jwt_secret).digest("base64");
        if (hash !== refresh_token) {
            AuthTool.logLogin(req, false, req.jwt.username, "invalid refresh token");
            return res.status(403).send({error: 'Invalid refresh token'});
        }

		//Validate refresh key in DB
        var user = await UserModel.getById(req.jwt.userId);
        if(!user) {
            AuthTool.logLogin(req, false, req.jwt.username, "user not found");
            return res.status(404).send({error: "Invalid user"});
        }

        if(SessionTool.isBanned(user)) {
            AuthTool.logLogin(req, false, req.jwt.username, "banned");
            return SessionTool.sendKick(res, SessionTool.getBanKick(user));
        }

		if(user.refresh_key !== req.jwt.refresh_key) {
            AuthTool.logLogin(req, false, req.jwt.username, "refresh key changed");
            return res.status(403).send({error: 'Invalid refresh key'});
        }

    } catch (err) {
        AuthTool.logLogin(req, false, req.jwt ? req.jwt.username : null, "invalid token");
        return res.status(403).send({error: "Invalid Token"});
    }

    req.login = req.jwt;
    delete req.login.iat; //Delete previous iat to generate a new one
    return next();
};

AuthTool.hashPassword = (password) => {
    let saltNew = crypto.randomBytes(16).toString('base64');
    let hashNew = crypto.createHmac('sha512', saltNew).update(password).digest("base64");
    let newPass = saltNew + "$" + hashNew;
    return newPass;
}

AuthTool.validatePassword = (user, password) =>
{
    let passwordFields = user.password.split('$');
    let salt = passwordFields[0];
    let hash = crypto.createHmac('sha512', salt).update(password).digest("base64");
    return hash === passwordFields[1];
}

//--- Permisions -----

AuthTool.isPermissionLevel = (required_permission) => {
    return (req, res, next) => {
        let user_permission_level = parseInt(req.jwt.permission_level);
        if (user_permission_level >= required_permission) {
            return next();
        } else {
            return res.status(403).send({error: "Permission Denied"});
        }
    };
};

AuthTool.isSameUserOr = (required_permission) => {
    return (req, res, next) => {
        let user_permission_level = parseInt(req.jwt.permission_level);
        let userId = req.params.userId || "";
        let same_user = (req.jwt.userId === userId || req.jwt.username.toLowerCase() === userId.toLowerCase());
        if (userId && same_user) {
            return next();
        } else {
            if (user_permission_level >= required_permission) {
                return next();
            } else {
                return res.status(403).send({error: "Permission Denied"});
            }
        }
    };
};

module.exports = AuthTool;
