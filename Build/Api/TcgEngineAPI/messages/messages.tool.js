const crypto = require('crypto');
const config = require('../config.js');
const MessageModel = require('./messages.model');

//Pending messages are kept in memory (sent with every heartbeat) and saved in Mongo, so a pm2 restart reloads them.
//A message is sent to clients until its show time, then it simply leaves the list.

var MessageTool = {};

var pending = []; //Not canceled, show_at in the future, sorted by show_at

MessageTool.load = async () => {
    pending = await MessageModel.getPending();
};

//Shown by every client message_delay seconds later, long enough for all heartbeats to deliver it first
MessageTool.send = async (text, admin_username) => {
    var now = new Date();
    var message = await MessageModel.create({
        id: crypto.randomBytes(8).toString('hex'),
        text: text,
        show_at: new Date(now.getTime() + config.message_delay * 1000),
        created_by: admin_username || "",
        created_at: now,
    });
    pending.push(message);
    pending.sort((a, b) => new Date(a.show_at) - new Date(b.show_at));
    return message;
};

MessageTool.cancel = async (id, admin_username) => {
    var message = await MessageModel.cancel(id, admin_username || "");
    if (message)
        pending = pending.filter((m) => m.id !== id);
    return message;
};

//Public list sent to clients, remain_sec lets them display at the same time without trusting their own clock
MessageTool.getPendingInfo = () => {
    var now = Date.now();
    pending = pending.filter((m) => new Date(m.show_at).getTime() > now);
    return pending.map((m) => ({
        id: m.id,
        text: m.text,
        remain_sec: (new Date(m.show_at).getTime() - now) / 1000,
    }));
};

MessageTool.getRecent = async (limit) => {
    return await MessageModel.getRecent(limit);
};

module.exports = MessageTool;
