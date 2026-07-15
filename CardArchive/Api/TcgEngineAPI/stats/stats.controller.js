const StatsRegistry = require('./stats.registry');

exports.ListStats = async (req, res) => {
    return res.status(200).send(StatsRegistry.list());
};

exports.GetStat = async (req, res) => {

    const stat = StatsRegistry.get(req.params.statId);
    if (!stat)
        return res.status(404).send({ error: "Stat not found: " + req.params.statId });

    try {
        const rows = await stat.getRows();
        return res.status(200).send({
            id: stat.id,
            title: stat.title,
            columns: stat.columns,
            rows: rows,
            generated_at: new Date(),
        });
    }
    catch (e) {
        console.error("Error loading stat " + stat.id + ":", e);
        return res.status(500).send({ error: "Error loading stat: " + stat.id });
    }
};
