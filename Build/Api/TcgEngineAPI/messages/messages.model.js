const mongoose = require('mongoose');
const Schema = mongoose.Schema;

//Message sent by an admin to every connected player, displayed by all clients at show_at
const messageSchema = new Schema({
    id: {type: String, required: true, unique: true},
    text: {type: String, default: ""},
    show_at: {type: Date, required: true, index: true},
    created_by: {type: String, default: ""},
    created_at: {type: Date, default: null},
    canceled: {type: Boolean, default: false},
    canceled_by: {type: String, default: ""},
    canceled_at: {type: Date, default: null},
});

messageSchema.methods.toObj = function () {
    var elem = this.toObject();
    delete elem.__v;
    delete elem._id;
    return elem;
};

const Message = mongoose.model("Message", messageSchema);
exports.Message = Message;

//Messages not shown yet and not canceled
exports.getPending = async () => {
    try {
        var list = await Message.find({canceled: false, show_at: {$gt: new Date()}}).sort({show_at: 1});
        return list.map((m) => m.toObj());
    } catch (e) {
        console.error("Messages load error:", e);
        return [];
    }
};

exports.getRecent = async (limit) => {
    var list = await Message.find({}).sort({created_at: -1}).limit(limit);
    return list.map((m) => m.toObj());
};

//Throws on failure so the admin request reports the error
exports.create = async (data) => {
    var message = new Message(data);
    var saved = await message.save();
    return saved.toObj();
};

//Only a pending message can be canceled, returns null when there is none
exports.cancel = async (id, admin_username) => {
    var doc = await Message.findOneAndUpdate(
        {id: id, canceled: false, show_at: {$gt: new Date()}},
        {$set: {canceled: true, canceled_by: admin_username, canceled_at: new Date()}},
        {new: true});
    return doc ? doc.toObj() : null;
};
