const UserModel = require('./users.model');
const UserTool = require('./users.tool');
const RewardModel = require('../rewards/rewards.model');
const DateTool = require('../tools/date.tool');
const Activity = require("../activity/activity.model");
const Validator = require('../tools/validator.tool');
const AuthTool = require('../authorization/auth.tool');
const Email = require('../tools/email.tool');
const { withTx } = require('../tools/transaction.tool');
const config = require('../config');

//Register new user
exports.RegisterUser = async (req, res, next) => {

    if(!req.body.username || !req.body.password){
        return res.status(400).send({error: 'Invalid parameters'});
    }

    var username = req.body.username;
    var password = req.body.password;
    var email = req.body.email || "";
    var avatar = req.body.avatar || "";

    //Validations
    if(!Validator.validateUsername(username)){
        return res.status(400).send({error: 'Invalid username'});
    }

    if(email && !Validator.validateEmail(email)){
        return res.status(400).send({error: 'Invalid email'});
    }

    if(!Validator.validatePassword(password)){
        return res.status(400).send({error: 'Invalid password'});
    }

    if(avatar && typeof avatar !== "string")
        return res.status(400).send({error: "Invalid avatar"});

    var user_username = await UserModel.getByUsername(username);
    if(user_username)
        return res.status(400).send({error: 'Username already exists'});

    if(email){
        var user_email = await UserModel.getByEmail(email);
        if(user_email)
            return res.status(400).send({error: 'Email already exists'});
    }

    //Check if its first user
    var nb_users = await UserModel.count();
    var permission = nb_users > 0 ? 1 : 10; //First user has 10
    var validation = nb_users > 0 ? 0 : 1;  //First user has 1

    //User Data
    var user = {};

    user.username = username;
    user.email = email;
    user.avatar = avatar;
    user.permission_level = permission;
    user.validation_level = validation;
    
    user.coins = config.start_coins;
    user.elo = config.start_elo;
    user.xp = 0; 

    user.account_create_time = new Date();
    user.last_login_time = new Date();
    user.last_online_time = new Date();
    user.email_confirm_key = UserTool.generateID(20);

    UserTool.setUserPassword(user, password);

    //Create user + log atomically
    var nUser;
    try {
        await withTx(async (session) => {
            nUser = await UserModel.create(user, { session });
            var activityData = {username: user.username, email: user.email };
            await Activity.LogActivity("register", user.username, activityData, { session });
        });
    } catch (e) {
        if (e && e.code === 11000)
            return res.status(400).send({ error: 'Username already exists' });
        console.error("RegisterUser transaction failed:", e);
        return res.status(500).send({ error: "Registration failed, please try again" });
    }

    //Send confirm email after commit (skipped when no email provided)
    if(email)
        UserTool.sendEmailConfirmKey(nUser, user.email, user.email_confirm_key);

    //Return response
    return res.status(200).send({ success: true, id: nUser._id });
};

exports.GetAll = async(req, res) => {

    let user_permission_level = parseInt(req.jwt.permission_level);
    let is_admin = (user_permission_level >= config.permissions.SERVER);
    
    var list = await UserModel.getAll();
    for(var i=0; i<list.length; i++){
        if(is_admin)
            list[i] = list[i].deleteSecrets();
        else
            list[i] = list[i].deleteAdminOnly();
    }

    return res.status(200).send(list);
};

exports.GetUser = async(req, res) => {
    var user = await UserModel.getById(req.params.userId);
    if(!user)
        user = await UserModel.getByUsername(req.params.userId);

    if(!user)
        return res.status(404).send({error: "User not found " + req.params.userId});
    
    let user_permission_level = parseInt(req.jwt.permission_level);    
    let is_admin = (user_permission_level >= config.permissions.SERVER);
    if(is_admin || req.params.userId == req.jwt.userId || req.params.userId == req.jwt.username)
        user = user.deleteSecrets();
    else
        user = user.deleteAdminOnly();

    user.server_time = new Date(); //Return server time
    return res.status(200).send(user);
};

exports.EditUser = async(req, res) => {

    var userId = req.params.userId;
    var avatar = req.body.avatar;
    var cardback = req.body.cardback;

    if(!userId || typeof userId !== "string")
        return res.status(400).send({error: "Invalid parameters"});

    if(avatar && typeof avatar !== "string")
        return res.status(400).send({error: "Invalid avatar"});

    if(cardback && typeof cardback !== "string")
        return res.status(400).send({error: "Invalid avatar"});

    var user = await UserModel.getById(userId);
    if(!user)
        return res.status(404).send({error: "User not found: " + userId});

    var userData = {};
    
    if(avatar && avatar.length < 50)
        userData.avatar = avatar;

    if(cardback && cardback.length < 50)
        userData.cardback = cardback;

    //Add other variables you'd like to be able to edit here
    //Avoid allowing changing username, email or password here, since those require additional security validations and should have their own functions

    //Update user
    var result = await UserModel.update(user, userData);
    if(!result)
        return res.status(400).send({error: "Error updating user: " + userId});

    return res.status(200).send(result.deleteSecrets());
};

exports.EditEmail = async(req, res) => {

    var userId = req.jwt.userId;
    var email = req.body.email;

    if(!userId || typeof userId !== "string")
        return res.status(400).send({error: "Invalid parameters"});

    if(!email || !Validator.validateEmail(email))
        return res.status(400).send({error: "Invalid email"});

    var user = await UserModel.getById(userId);
    if(!user)
        return res.status(404).send({error: "User not found: " + userId});

    if(email == user.email)
        return res.status(400).send({error: "Email unchanged"});

    //Find email
    var foundUserEmail = await UserModel.getByEmail(email);
    if(foundUserEmail)
        return res.status(403).send({error: "Email already exists"});

    var prev_email = user.email;
    var userData = {};
    userData.email = email;
    userData.validation_level = 0;
    userData.email_confirm_key = UserTool.generateID(20);

    //Update user + log atomically
    var result;
    try {
        await withTx(async (session) => {
            result = await UserModel.update(user, userData, { session });
            var activityData = {prev_email: prev_email, new_email: email };
            await Activity.LogActivity("edit_email", req.jwt.username, {activityData}, { session });
        });
    } catch (e) {
        console.error("EditEmail transaction failed:", e);
        return res.status(500).send({ error: "Error updating user email, please try again" });
    }

    //Send confirmation emails after commit
    UserTool.sendEmailConfirmKey(user, email, userData.email_confirm_key);
    UserTool.sendEmailChangeEmail(user, prev_email, email);

    return res.status(200).send(result.deleteSecrets());
};

exports.EditPassword = async(req, res) => {

    var userId = req.jwt.userId;
    var password = req.body.password_new;
    var password_previous = req.body.password_previous;

    if(!userId || typeof userId !== "string")
        return res.status(400).send({error: "Invalid parameters"});

    if(!password || !password_previous || typeof password !== "string" || typeof password_previous !== "string")
        return res.status(400).send({error: "Invalid parameters"});
    
    var user = await UserModel.getById(userId);
    if(!user)
        return res.status(404).send({error: "User not found: " + userId});

    let validPass = AuthTool.validatePassword(user, password_previous);
    if(!validPass)
        return res.status(401).send({error: "Invalid previous password"});

    UserTool.setUserPassword(user, password);

    try {
        await withTx(async (session) => {
            await UserModel.save(user, ["password", "refresh_key", "password_recovery_key"], { session });
            await Activity.LogActivity("edit_password", req.jwt.username, {}, { session });
        });
    } catch (e) {
        console.error("EditPassword transaction failed:", e);
        return res.status(500).send({ error: "Error updating user password, please try again" });
    }

    //Send confirmation email after commit
    UserTool.sendEmailChangePassword(user, user.email);

    return res.status(204).send({});
};

exports.EditPermissions = async(req, res) => {

    var userId = req.params.userId;
    var permission_level = req.body.permission_level;

    if(!userId || typeof userId !== "string")
        return res.status(400).send({error: "Invalid parameters"});

    if(!Validator.isInteger(permission_level))
        return res.status(400).send({error: "Invalid permission"});

    var user = await UserModel.getById(userId);
    if(!user)
        return res.status(404).send({error: "User not found: " + userId});

    var userData = {};

    //Change avatar
    userData.permission_level = permission_level;

    //Update user + log atomically
    var result;
    try {
        await withTx(async (session) => {
            result = await UserModel.update(user, userData, { session });
            var activityData = {username: user.username, permission_level: userData.permission_level };
            await Activity.LogActivity("edit_permission", req.jwt.username, activityData, { session });
        });
    } catch (e) {
        console.error("EditPermissions transaction failed:", e);
        return res.status(500).send({ error: "Error updating user, please try again" });
    }

    return res.status(200).send(result.deleteSecrets());
};

exports.ResetPassword = async(req, res) => {

    var email = req.body.email;

    if(!config.smtp_enabled)
        return res.status(400).send({error: "Email SMTP is not configured"});

    if(!email || typeof email !== "string")
        return res.status(400).send({error: "Invalid parameters"});

    var user = await UserModel.getByEmail(email);
    if(!user)
        return res.status(404).send({error: "User not found: " + email});

    user.password_recovery_key = UserTool.generateID(10, true);
    await UserModel.save(user, ["password_recovery_key"]);

    UserTool.sendEmailPasswordRecovery(user, email);

    return res.status(204).send({});
};


exports.ResetPasswordConfirm = async(req, res) => {

    var email = req.body.email;
    var code = req.body.code;
    var password = req.body.password;

    if(!email || typeof email !== "string")
        return res.status(400).send({error: "Invalid parameters"});

    if(!code || typeof code !== "string")
        return res.status(400).send({error: "Invalid parameters"});

    if(!password || typeof password !== "string")
        return res.status(400).send({error: "Invalid parameters"});
    
    var user = await UserModel.getByEmail(email);
    if(!user)
        return res.status(404).send({error: "User not found: " + email});

    if(!user.password_recovery_key || user.password_recovery_key.toUpperCase() != code)
        return res.status(403).send({error: "Invalid Recovery Code"});
    
    UserTool.setUserPassword(user, password);

    var result = await UserModel.save(user, ["password", "refresh_key", "password_recovery_key"]);
    if(!result)
        return res.status(500).send({error: "Error updating user password: " + email});

    return res.status(204).send({});
};

//In this function all message are returned in direct text because the email link is accessed from browser
exports.ConfirmEmail = async (req, res) =>{

    if(!req.params.userId || !req.params.code){
        return res.status(404).send("Code invalid");
    }

    var user = await UserModel.getById(req.params.userId);
    if(!user)
        return res.status(404).send("Code invalid");

    if(user.email_confirm_key != req.params.code)
        return res.status(404).send("Code invalid");

    if(user.validation_level >= 1)
        return res.status(400).send("Email already confirmed!");

    //Code valid!
    var data = {validation_level: Math.max(user.validation_level, 1)};
    await UserModel.update(user, data);

    return res.status(200).send("Email confirmed!");
};

exports.ResendEmail = async(req, res) => 
{
    var userId = req.jwt.userId;
    var user = await UserModel.getById(userId);
    if(!user)
        return res.status(404).send({error: "User not found " + userId});

    if(user.validation_level > 0)
        return res.status(403).send({error: "Email already confirmed"});

    UserTool.sendEmailConfirmKey(user, user.email, user.email_confirm_key);

    return res.status(200).send();
}

exports.SendEmail = async (req, res) =>{

    var subject = req.body.title;
    var text = req.body.text;
    var email = req.body.email;

    if(!subject || typeof subject !== "string")
        return res.status(400).send({error: "Invalid parameters"});

    if(!text || typeof text !== "string")
        return res.status(400).send({error: "Invalid parameters"});

    if(!email || typeof email !== "string")
        return res.status(400).send({error: "Invalid parameters"});

    Email.SendEmail(email, subject, text, function(result){
        console.log("Sent email to: " + email + ": " + result);
        return res.status(200).send({success: result});
    });
};

// reward is an object containing rewards to give
exports.GiveReward = async(req, res) => 
{
    var userId = req.params.userId;
    var reward = req.body.reward;

    //Validate params
    if (!userId || typeof userId !== "string")
            return res.status(400).send({ error: "Invalid parameters" });

    if (!reward || typeof reward !== "object")
        return res.status(400).send({ error: "Invalid parameters" });

    //Get the user add update the array
    var user = await UserModel.getById(userId);
    if (!user)
        return res.status(404).send({ error: "Cant find user " + userId });
    
    var valid = await UserTool.GainUserReward(user, reward);
    if (!valid)
        return res.status(500).send({ error: "Error when adding rewards " + userId });

    //Save user + log atomically
    var updatedUser;
    try {
        await withTx(async (session) => {
            updatedUser = await UserModel.save(user, ["cards"], { session });
            var activityData =  {reward: reward, user: user.username};
            await Activity.LogActivity("reward_give", req.jwt.username, activityData, { session });
        });
    } catch (e) {
        console.error("GiveReward transaction failed:", e);
        return res.status(500).send({ error: "Error updating user, please try again" });
    }

    return res.status(200).send(updatedUser.deleteSecrets());
};

// reward is an ID of reward to give
exports.GainReward = async(req, res) => 
{
    var userId = req.params.userId;
    var rewardId = req.body.reward;

    if(!userId || !rewardId)
        return res.status(400).send({error: "Invalid parameters"});

    if(typeof rewardId !== "string")
        return res.status(400).send({error: "Invalid parameters"});

    var user = await UserModel.getById(userId);
    if(!user)
        return res.status(404).send({error: "User not found: " + userId});

    var reward = await RewardModel.get(rewardId);
    if(!reward)
        return res.status(404).send({error: "Reward not found: " + rewardId});

    if(reward.repeat && req.jwt.permission_level < config.permissions.SERVER)
        return res.status(404).send({error: "Insufficient Permission"});

    if(!reward.repeat && user.rewards.includes(rewardId))
        return res.status(403).send({error: "Reward already claimed: " + rewardId});

    if(!reward.repeat && reward.group && user.rewards.includes(reward.group))
        return res.status(403).send({error: "Reward group already claimed: " + reward.group});

    //Save reward
    if(!user.rewards.includes(reward.tid))
        user.rewards.push(reward.tid);

    if(reward.group && !user.rewards.includes(reward.group))
        user.rewards.push(reward.group);

    //Add reward to user
    var valid = await UserTool.GainUserReward(user, reward);
    
    //Check if succeed
    if(!valid)
        return res.status(500).send({error: "Failed adding reward: " + rewardId + " for " + userId});
    
    //Save user + log atomically
    try {
        await withTx(async (session) => {
            await UserModel.save(user, ["rewards", "xp", "coins", "cards", "decks", "avatars", "cardbacks"], { session });
            var activityData =  {reward: reward, user: user.username};
            await Activity.LogActivity("reward_gain", req.jwt.username, activityData, { session });
        });
    } catch (e) {
        console.error("GainReward transaction failed:", e);
        return res.status(500).send({ error: "Error updating user, please try again" });
    }

    return res.status(200).send(user.deleteSecrets());
};

exports.GetOnline = async(req, res) =>
{
    //Count online users
    var time = new Date();
    time = DateTool.addMinutes(time, -10);

    var count = 0;
    var users = await UserModel.getAll();
    var usernames = [];
    for(var i=0; i<users.length; i++)
    {
        var user = users[i];
        if(user.last_online_time > time)
        {
            usernames.push(user.username);
            count++;
        }
    }
    return res.status(200).send({online: count, total: users.length, users: usernames});
};

exports.Delete = async(req, res) => {
    UserModel.remove(req.params.userId);
    return res.status(204).send({});
};