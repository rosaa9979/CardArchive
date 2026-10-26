const schedule = require('node-schedule');

const ExecuteJobs = async() =>
{
    //console.log('Run Hourly Jobs.....');

    //Add custom hourly jobs here



};

exports.InitJobs = function()
{
    schedule.scheduleJob('0 * * * *', function(){  // every hour, at minute 0
        ExecuteJobs();
    });
    
    //Test run when starting
    ExecuteJobs();
}