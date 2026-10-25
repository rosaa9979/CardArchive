const UserModel = require('../../users/users.model');
const CardModel = require('../../cards/cards.model');

//Counts hero + clubs + cards entries of every deck of every user.
//Only cards present in the Cards collection are counted: CardUploader only
//uploads deckbuilding=true cards, so that collection is the deckbuilding whitelist.
module.exports = {

    id: "card-usage",
    title: "카드 편성 통계",
    description: "카드별로 덱에 편성된 횟수 (덱 구성 가능 카드만)",
    order: 2,

    columns: [
        { key: "tid", label: "ID", type: "text" },
        { key: "title", label: "이름", type: "text" },
        { key: "type", label: "타입", type: "text" },
        { key: "deck_count", label: "편성된 덱 수", type: "number" },
        { key: "total_copies", label: "총 편성 장수", type: "number" },
        { key: "user_count", label: "사용 유저 수", type: "number" },
    ],

    getRows: async () => {

        //Deckbuilding card whitelist + display info
        const cards = await CardModel.getAll();
        const card_info = {};
        for (const card of cards)
            card_info[card.tid] = { title: card.title || "", type: card.type || "" };

        const users = await UserModel.getAll();
        const counts = {}; //tid -> {deck_count, total_copies, users}

        for (const user of users) {
            for (const deck of user.decks || []) {

                const entries = [];
                if (deck.hero && deck.hero.tid)
                    entries.push(deck.hero);
                for (const club of deck.clubs || [])
                    entries.push(club);
                for (const card of deck.cards || [])
                    entries.push(card);

                const seen_in_deck = new Set();
                for (const entry of entries) {
                    if (!entry || !entry.tid)
                        continue;
                    if (!card_info[entry.tid])
                        continue; //Not a deckbuilding card

                    var count = counts[entry.tid];
                    if (!count)
                        count = counts[entry.tid] = { deck_count: 0, total_copies: 0, users: new Set() };

                    count.total_copies += entry.quantity || 1;
                    count.users.add(user.username);
                    if (!seen_in_deck.has(entry.tid)) {
                        count.deck_count++;
                        seen_in_deck.add(entry.tid);
                    }
                }
            }
        }

        //One row per deckbuilding card, unused cards show 0 (balance check)
        const rows = Object.keys(card_info).map((tid) => ({
            tid: tid,
            title: card_info[tid].title || "-",
            type: card_info[tid].type || "-",
            deck_count: counts[tid] ? counts[tid].deck_count : 0,
            total_copies: counts[tid] ? counts[tid].total_copies : 0,
            user_count: counts[tid] ? counts[tid].users.size : 0,
        }));

        rows.sort((a, b) => b.deck_count - a.deck_count || a.tid.localeCompare(b.tid));
        return rows;
    },
};
