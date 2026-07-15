const fs = require('fs');
const path = require('path');

//Stat modules are auto-loaded from the modules/ folder.
//To add a new stat: create modules/<name>.stat.js exporting
//{ id, title, description, order, columns, getRows }
//The frontend renders any stat generically from its columns definition.

const modules_dir = path.join(__dirname, 'modules');

var stats = {};

for (const file of fs.readdirSync(modules_dir)) {
    if (!file.endsWith('.stat.js'))
        continue;
    const stat = require(path.join(modules_dir, file));
    if (!stat.id || !stat.columns || typeof stat.getRows !== 'function') {
        console.error("Invalid stat module: " + file);
        continue;
    }
    stats[stat.id] = stat;
}

//List of available stats (without data), for the dashboard menu
exports.list = () => {
    return Object.values(stats)
        .sort((a, b) => (a.order || 0) - (b.order || 0))
        .map((s) => ({ id: s.id, title: s.title, description: s.description || "" }));
};

exports.get = (id) => {
    return stats[id] || null;
};
