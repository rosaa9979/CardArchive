const CardModel = require('../cards/cards.model');
const Activity = require("../activity/activity.model");
const config = require('../config');
const path = require('path');
const fs = require('fs/promises');

exports.AddCard = async(req, res) => 
{
    try {
        // multipart/form-data에서 JSON 데이터 파싱
        if (!req.body.card_data) {
            return res.status(400).send({ error: "Missing card_data" });
        }

        // JSON 문자열을 파싱
        let card_data;
        try {
            card_data = JSON.parse(req.body.card_data);
        } catch (parseError) {
            return res.status(400).send({ error: "Invalid JSON in card_data" });
        }

        // C# 코드에 맞춰 필드 추출
        var tid = card_data.tid;
        var type = card_data.type;
        var weapon = card_data.weapon;
        var clubs = card_data.clubs || []; 
        var mana = card_data.mana || 0;
        var attack = card_data.attack || 0;
        var hp = card_data.hp || 0;
        var cost = card_data.cost || 1;
        var packs = card_data.packs || [];

        // 유효성 검증
        if(!tid || typeof tid !== "string")
            return res.status(400).send({error: "Invalid tid parameter"});

        if(!type || typeof type !== "string")
            return res.status(400).send({error: "Invalid type parameter"});

        if(!weapon || typeof weapon !== "string")
            return res.status(400).send({error: "Invalid weapon parameter"});

        if(clubs && !Array.isArray(clubs))
            return res.status(400).send({error: "Invalid clubs parameter"});

        if(!Number.isInteger(mana) || !Number.isInteger(attack) || 
           !Number.isInteger(hp) || !Number.isInteger(cost))
            return res.status(400).send({ error: "Invalid numeric parameters" });

        if(packs && !Array.isArray(packs))
            return res.status(400).send({error: "Invalid packs parameter"});

        const image_file = req.files?.['image_file']?.[0] || null;

        var data = {
            tid: tid,
            type: type,
            weapon: weapon,
            clubs: clubs,
            mana: mana,
            attack: attack,
            hp: hp,
            cost: cost,
            packs: packs,
        }

        // Update or create
        var card = await CardModel.get(tid);
        if(card)
            card = await CardModel.update(card, data, image_file);
        else
            card = await CardModel.create(data, image_file);

        if(!card)
            return res.status(500).send({error: "Error updating card"});

        return res.status(200).send(data);
        
    } catch (error) {
        console.error('AddCard error:', error);
        return res.status(500).send({ error: "Internal server error" });
    }
};

exports.DeleteCard = async(req, res) => 
{
    CardModel.remove(req.params.tid);
    return res.status(204).send({});
};

exports.DeleteAll = async(req, res) => 
{
    CardModel.removeAll();
    return res.status(204).send({});
};

exports.GetCard = async(req, res) => 
{
    var tid = req.params.tid;

    if(!tid)
        return res.status(400).send({error: "Invalid parameters"});

    var card = await CardModel.get(tid);
    if(!card)
        return res.status(404).send({error: "Card not found: " + tid});

    return res.status(200).send(card.toObj());
};

exports.GetAll = async(req, res) => 
{
    var cards = await CardModel.getAll();

    for(var i=0; i<cards.length; i++){
        cards[i] = cards[i].toObj();
    }

    return res.status(200).send(cards);
};


