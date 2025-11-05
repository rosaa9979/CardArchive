const mongoose = require('mongoose');
const Schema = mongoose.Schema;
const path = require('path');
const fs = require('fs/promises');

const upload_dir = path.join(__dirname, '../uploads/cards');

const cardsSchema = new Schema({

    tid: { type: String, index: true, unique: true, default: "" },
    type: { type: String, default: "" },
    weapon: { type: String, default: "" },
    clubs: [{type: String}],
    //team: { type: String, default: "" },
    //rarity: {type: String, default: ""},
    mana: {type: Number, default: 0},
    attack: {type: Number, default: 0},
    hp: {type: Number, default: 0},
    cost: {type: Number, default: 0},

    packs: [{type: String}],  //Card is available in those packs
    image: { type: String, default: "" },
});

cardsSchema.methods.toObj = function() {
    var card = this.toObject();
    delete card.__v;
    delete card._id;
    return card;
};

const Card = mongoose.model('Cards', cardsSchema);

async function saveImageFile(tid, image_file) {
    if (!image_file) return null;

    try {
        await fs.mkdir(upload_dir, { recursive: true });
        const relative_path = `/uploads/cards/${tid}.png`;
        const absolute_path = path.join(__dirname, `..${relative_path}`);

        await deleteImageFile(relative_path);

        await fs.writeFile(absolute_path, image_file.buffer);
        console.log(`[saveImageFile] 새 이미지 저장: ${absolute_path}`);

        return relative_path;
    } catch (error) {
        console.error(`[saveImageFile] 이미지 저장 중 오류: `, error);
        return null;
    }

}

async function deleteImageFile(image_path) {
    if (!image_path) return;

    const absolute_path = path.join(__dirname, `..${image_path}`);

    try {
        await fs.unlink(absolute_path);
        console.log(`[remove] 이미지 삭제 완료: ${absolute_path}`);
    } catch (err) {
        if (err.code !== 'ENOENT') {
            console.error(`[remove] 이미지 삭제 실패: ${absolute_path}`, err);
        }
    }
}

exports.get = async(tid) => {
    try{
        var card = await Card.findOne({tid: tid});
        return card;
    }
    catch{
        return null;
    }
};

exports.getAll = async(filter) => {

    try{
        filter = filter || {};
        var cards = await Card.find(filter);
        return cards || [];
    }
    catch{
        return [];
    }
};

exports.getByPack = async(packId, filter) => {

    try{
        filter = filter || {};
        if(packId)
        {
            filter.packs = {$in:[packId]};
        }
        var cards = await Card.find(filter);
        return cards || [];
    }
    catch{
        return [];
    }
};

exports.create = async(data, image_file) => {
    try{
        const image_path = await saveImageFile(data.tid, image_file);
        var card = new Card({ ...data, image: image_path });
        return await card.save();
    }
    catch{
        return null;
    }
};

exports.update = async (card, data, image_file) => {
    try {
        if (!card) return null;

        for (let i in data) {
            card[i] = data[i];
            card.markModified(i);
        }

        if (image_file) {
            const imagePath = await saveImageFile(card.tid, image_file);
            card.image = imagePath;
            card.markModified('image');
        }

        const updated = await card.save();
        return updated;
    } catch (err) {
        console.error('CardModel.update error:', err);
        return null;
    }
};

exports.remove = async (tid) => {
    try {
        const card = await Card.findOne({ tid });
        if (!card) return false;

        await deleteImageFile(card.image);

        const result = await Card.deleteOne({ tid });
        return result && result.deletedCount > 0;
    } catch (err) {
        console.error('CardModel.remove error:', err);
        return false;
    }
};

exports.removeAll = async () => {
    try {
        const cards = await Card.find({}, { image: 1 });
        for (const card of cards) {
            await deleteImageFile(card.image);
        }

        const result = await Card.deleteMany({});
        return result && result.deletedCount > 0;
    } catch (err) {
        console.error('CardModel.removeAll error:', err);
        return false;
    }
};