const mongoose = require('mongoose');

//Wraps a function in a Mongo transaction. The callback receives a session
//that must be passed to every model write/read intended to be part of the
//transaction (e.g. UserModel.update(user, data, { session })).
//Throwing inside the callback aborts the transaction (rollback). Returning
//normally commits it. Requires Mongo running in replica set mode.
exports.withTx = async (fn) => {
    const session = await mongoose.startSession();
    try {
        await session.withTransaction(async () => {
            await fn(session);
        });
    } finally {
        await session.endSession();
    }
};
