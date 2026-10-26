const MaintenanceTool = require('../maintenance/maintenance.tool');
const MessageTool = require('../messages/messages.tool');

//Live operation state sent to clients with the version, login and heartbeat responses.
//Every response is the full current list: what is past or canceled is simply not in it anymore.

var LiveOpsTool = {};

LiveOpsTool.load = async () => {
    await MaintenanceTool.load();
    await MessageTool.load();
};

LiveOpsTool.getOps = () => {
    return {
        maintenance: MaintenanceTool.getInfo(),
        messages: MessageTool.getPendingInfo(),
    };
};

module.exports = LiveOpsTool;
