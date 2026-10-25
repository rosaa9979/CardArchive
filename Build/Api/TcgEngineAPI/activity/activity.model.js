const mongoose = require("mongoose");
const Schema = mongoose.Schema;

const activitySchema = new Schema(
{
    type: {type: String},
    username: {type: String},
    timestamp: {type: Date},
    data: {type: Object, _id: false},
});

activitySchema.methods.toObj = function () {
  var elem = this.toObject();
  delete elem.__v;
  delete elem._id;
  return elem;
};

const Activity = mongoose.model("Activity", activitySchema);
exports.Activity = Activity;

// ------------------------------

//Throws on failure so callers wrapped in a transaction will roll back.
//Pass { session } as opts to participate in a Mongo transaction.
exports.LogActivity = async (type, username, data, opts = {}) => {
  var activity_data = {
    type: type,
    username: username,
    timestamp: Date.now(),
    data: data
  }
  const activity = new Activity(activity_data);
  return await activity.save(opts);
};

exports.GetAll = async () => {
  try {
    const logs = await Activity.find({});
    return logs;
  } catch (e) {
    return [];
  }
};

exports.Get = async (data) => {
  try {
    const logs = await Activity.find(data);
    return logs;
  } catch (e) {
    return [];
  }
};