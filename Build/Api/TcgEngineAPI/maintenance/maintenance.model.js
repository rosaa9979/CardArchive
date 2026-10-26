const mongoose = require('mongoose');
const Schema = mongoose.Schema;

//Single document (key "main") holding the server maintenance schedule
const maintenanceSchema = new Schema({
    key: {type: String, required: true, unique: true, default: "main"},
    state: {type: String, default: "none"},          //none, scheduled, active (scheduled becomes active once shutdown_at is reached)
    shutdown_at: {type: Date, default: null},        //Time when every client leaves and the API starts answering 503
    end_at: {type: Date, default: null},             //Expected reopening time, display only
    message: {type: String, default: ""},
    block_match_minutes: {type: Number, default: 5}, //Game server stops matchmaking this many minutes before shutdown_at
    whitelist: [{type: String}],                     //Usernames still allowed during maintenance (testers)
    updated_by: {type: String, default: ""},
    updated_at: {type: Date, default: null},
});

maintenanceSchema.methods.toObj = function () {
    var elem = this.toObject();
    delete elem.__v;
    delete elem._id;
    return elem;
};

const Maintenance = mongoose.model("Maintenance", maintenanceSchema);
exports.Maintenance = Maintenance;

exports.get = async () => {
    try {
        return await Maintenance.findOne({key: "main"});
    } catch (e) {
        console.error("Maintenance load error:", e);
        return null;
    }
};

//Throws on failure so the admin request reports the error instead of a stale state
exports.save = async (data) => {
    return await Maintenance.findOneAndUpdate({key: "main"}, {$set: data}, {upsert: true, new: true});
};
