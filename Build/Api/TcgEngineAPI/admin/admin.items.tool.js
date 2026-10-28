const config = require('../config');
const UserModel = require('../users/users.model');
const UserTool = require('../users/users.tool');
const CardModel = require('../cards/cards.model');
const PackModel = require('../packs/packs.model');
const VariantModel = require('../variants/variants.model');
const DeckModel = require('../decks/decks.model');
const TransactionTool = require('../tools/transaction.tool');

//Items and decks of one user changed by an admin.
//A grant is a reward-shaped bundle {coins, xp, cards, packs, decks}: signed amounts for coins, xp, cards and packs
//(negative revokes), premade deck tids for decks. A later mail system can store the same bundle and apply it on claim.
//Every change runs in a transaction that reloads the user, a concurrent write retries it instead of being overwritten.

var AdminItemsTool = {};

AdminItemsTool.reason_max = 200;
AdminItemsTool.amount_max = 10000000;   //Coins or xp in one change
AdminItemsTool.quantity_max = 1000;     //Cards or packs of one entry
AdminItemsTool.entries_max = 100;       //Entries of one list
AdminItemsTool.deck_cards_max = 60;     //Entries of one deck, far above the deck size so only broken requests hit it

const type_hero = "아로나";
const type_club = "동아리";
const type_student = "학생";
const deck_card_types = ["학생", "비학생", "이벤트", "장소"];

//Invalid request found while applying a change, answered as a 400 and rolls the transaction back
class ActionError extends Error {}
AdminItemsTool.ActionError = ActionError;

// ----- Catalog -----------

async function loadCatalog() {
    var cards = await CardModel.getAll();
    var variants = await VariantModel.getAll();
    var packs = await PackModel.getAll();
    var decks = await DeckModel.getAll();
    var default_variant = variants.find((v) => v.is_default);
    return {
        cards: new Map(cards.map((c) => [c.tid, c])),
        variants: new Set(variants.map((v) => v.tid)),
        default_variant: default_variant ? default_variant.tid : "",
        packs: new Set(packs.map((p) => p.tid)),
        decks: new Map(decks.map((d) => [d.tid, d])),
    };
}

//Everything the admin page can pick from
AdminItemsTool.getCatalog = async () => {
    var catalog = await loadCatalog();
    return {
        cards: [...catalog.cards.values()].map((c) => ({tid: c.tid, title: c.title || "", type: c.type || "", clubs: c.clubs || []}))
            .sort((a, b) => (a.title || a.tid).localeCompare(b.title || b.tid)),
        variants: [...catalog.variants],
        default_variant: catalog.default_variant,
        packs: [...catalog.packs].sort(),
        decks: [...catalog.decks.values()].map((d) => ({
            tid: d.tid,
            title: d.title || "",
            card_count: (d.cards || []).reduce((total, c) => total + (c.quantity || 0), 0),
        })),
        rules: getRules(),
    };
};

function getRules() {
    return {
        deck_size: config.deck_size,
        student_max: config.deck_student_duplicate_max,
        non_student_max: config.deck_non_student_duplicate_max,
        club_size: config.club_size,
    };
}

// ----- Request parsing -----------

AdminItemsTool.parseReason = (value) => {
    var reason = typeof value === "string" ? value.trim() : "";
    if (reason.length === 0 || reason.length > AdminItemsTool.reason_max)
        throw new ActionError("Reason must be between 1 and " + AdminItemsTool.reason_max + " characters");
    return reason;
};

function parseInteger(value, max, name) {
    var number = value === undefined || value === null || value === "" ? 0 : Number(value);
    if (!Number.isInteger(number) || Math.abs(number) > max)
        throw new ActionError(name + " must be an integer between -" + max + " and " + max);
    return number;
}

function parseList(value, name) {
    if (value === undefined || value === null)
        return [];
    if (!Array.isArray(value) || value.length > AdminItemsTool.entries_max)
        throw new ActionError(name + " must be a list of at most " + AdminItemsTool.entries_max + " entries");
    return value;
}

function parseTid(value, name) {
    if (typeof value !== "string" || value.length === 0 || value.length > 100)
        throw new ActionError("Invalid " + name);
    return value;
}

//Same card (tid + variant) or pack listed twice is merged
function mergeEntry(list, entry, keys) {
    var found = list.find((e) => keys.every((k) => e[k] === entry[k]));
    if (found)
        found.quantity += entry.quantity;
    else
        list.push(entry);
}

//Validated grant with every id checked against the catalog, empty entries removed
AdminItemsTool.parseGrant = (value, catalog) => {
    if (!value || typeof value !== "object" || Array.isArray(value))
        throw new ActionError("Invalid grant");

    var grant = {
        coins: parseInteger(value.coins, AdminItemsTool.amount_max, "Coins"),
        xp: parseInteger(value.xp, AdminItemsTool.amount_max, "XP"),
        cards: [],
        packs: [],
        decks: [],
    };

    for (var entry of parseList(value.cards, "Cards")) {
        var tid = parseTid(entry && entry.tid, "card");
        var variant = entry.variant || catalog.default_variant;
        if (!catalog.cards.has(tid))
            throw new ActionError("Unknown card: " + tid);
        if (!catalog.variants.has(variant))
            throw new ActionError("Unknown variant: " + variant);
        var quantity = parseInteger(entry.quantity, AdminItemsTool.quantity_max, "Card quantity");
        if (quantity !== 0)
            mergeEntry(grant.cards, {tid: tid, variant: variant, quantity: quantity}, ["tid", "variant"]);
    }

    for (var entry of parseList(value.packs, "Packs")) {
        var tid = parseTid(entry && entry.tid, "pack");
        if (!catalog.packs.has(tid))
            throw new ActionError("Unknown pack: " + tid);
        var quantity = parseInteger(entry.quantity, AdminItemsTool.quantity_max, "Pack quantity");
        if (quantity !== 0)
            mergeEntry(grant.packs, {tid: tid, quantity: quantity}, ["tid"]);
    }

    for (var tid of parseList(value.decks, "Decks")) {
        parseTid(tid, "deck");
        if (!catalog.decks.has(tid))
            throw new ActionError("Unknown premade deck: " + tid);
        grant.decks.push(tid);
    }

    grant.cards = grant.cards.filter((c) => c.quantity !== 0);
    grant.packs = grant.packs.filter((p) => p.quantity !== 0);
    var empty = !grant.coins && !grant.xp && !grant.cards.length && !grant.packs.length && !grant.decks.length;
    if (empty)
        throw new ActionError("Nothing to grant");
    return grant;
};

// ----- Apply -----------

function addAmount(user, key, amount, name) {
    var value = (user[key] || 0) + amount;
    if (value < 0)
        throw new ActionError("Not enough " + name + " to revoke (" + (user[key] || 0) + ")");
    user[key] = value;
}

//Adds a signed quantity to a card or pack list, an entry reaching 0 is removed
function addQuantity(list, entry, same, name) {
    var index = list.findIndex(same);
    var current = index >= 0 ? list[index].quantity || 0 : 0;
    var quantity = current + entry.quantity;
    if (quantity < 0)
        throw new ActionError("Not enough " + name + " to revoke: " + entry.tid + " (" + current + ")");

    if (index >= 0 && quantity === 0)
        list.splice(index, 1);
    else if (index >= 0)
        list[index].quantity = quantity;
    else
        list.push(Object.assign({}, entry));
}

function newDeckTid(user) {
    var tid;
    do {
        tid = UserTool.generateID(7);
    } while ((user.decks || []).some((d) => d && d.tid === tid));
    return tid;
}

//Club cards the deck needs, derived from the clubs of its cards like UserDeckData.GetRequiredClubs
function getRequiredClubs(cards, catalog) {
    var club_ids = new Set();
    for (var entry of cards) {
        var card = catalog.cards.get(entry.tid);
        for (var club of (card && card.clubs) || [])
            club_ids.add(club);
    }

    var required = [];
    for (var card of catalog.cards.values()) {
        if (card.type === type_club && (card.clubs || []).some((c) => club_ids.has(c)))
            required.push({tid: card.tid, variant: catalog.default_variant, quantity: 1});
    }
    return required.sort((a, b) => a.tid.localeCompare(b.tid));
}

//Cards saved without variant by older versions count as the default variant
function sameCard(entry, catalog) {
    return (e) => e.tid === entry.tid && (e.variant || catalog.default_variant) === entry.variant;
}

function samePack(entry) {
    return (e) => e.tid === entry.tid;
}

function copyEntry(entry, catalog) {
    return {tid: entry.tid, variant: entry.variant || catalog.default_variant, quantity: entry.quantity || 1};
}

//Premade decks are added with their cards, like a reward deck (UserTool.addDecks)
function addPremadeDeck(user, premade, catalog) {
    var cards = (premade.cards || []).filter((c) => c && c.tid).map((c) => copyEntry(c, catalog));
    for (var card of cards)
        addQuantity(user.cards, card, sameCard(card, catalog), "cards");

    user.decks.push({
        tid: newDeckTid(user),
        title: premade.title || "",
        hero: premade.hero && premade.hero.tid ? {tid: premade.hero.tid, variant: premade.hero.variant || catalog.default_variant} : {},
        cards: cards,
        clubs: getRequiredClubs(cards, catalog),
    });
}

AdminItemsTool.applyGrant = (user, grant, catalog) => {
    addAmount(user, "coins", grant.coins, "coins");
    addAmount(user, "xp", grant.xp, "xp");
    for (var card of grant.cards)
        addQuantity(user.cards, card, sameCard(card, catalog), "cards");
    for (var pack of grant.packs)
        addQuantity(user.packs, pack, samePack(pack), "packs");
    for (var tid of grant.decks)
        addPremadeDeck(user, catalog.decks.get(tid), catalog);
};

//Loads the user inside a transaction, applies change(user) and saves it. Returns what change returns.
//The callback can run again when Mongo retries the transaction, so it must only change the user it receives.
AdminItemsTool.update = async (userId, change) => {
    var result;
    await TransactionTool.withTx(async (session) => {
        var user = await UserModel.UserModel.findById(userId).session(session);
        if (!user)
            throw new ActionError("User not found");
        user.cards = user.cards || [];
        user.packs = user.packs || [];
        user.decks = user.decks || [];
        result = change(user);
        user.markModified("decks");
        await user.save({session});
    });
    return result;
};

// ----- Decks -----------

//Validated deck content: unknown or misplaced cards are refused, game rules only produce warnings
AdminItemsTool.parseDeck = (value, catalog) => {
    if (!value || typeof value !== "object")
        throw new ActionError("Invalid deck");

    var title = typeof value.title === "string" ? value.title.trim() : "";
    if (title.length === 0 || title.length > 50)
        throw new ActionError("Deck title must be between 1 and 50 characters");

    var hero = {};
    if (value.hero && value.hero.tid) {
        var hero_card = catalog.cards.get(value.hero.tid);
        if (!hero_card || hero_card.type !== type_hero)
            throw new ActionError("Not a hero card: " + value.hero.tid);
        hero = {tid: hero_card.tid, variant: value.hero.variant || catalog.default_variant};
        if (!catalog.variants.has(hero.variant))
            throw new ActionError("Unknown variant: " + hero.variant);
    }

    if (!Array.isArray(value.cards) || value.cards.length > AdminItemsTool.deck_cards_max)
        throw new ActionError("Deck cards must be a list of at most " + AdminItemsTool.deck_cards_max + " entries");

    var cards = [];
    for (var entry of value.cards) {
        var tid = parseTid(entry && entry.tid, "card");
        var card = catalog.cards.get(tid);
        if (!card)
            throw new ActionError("Unknown card: " + tid);
        if (!deck_card_types.includes(card.type))
            throw new ActionError("Card cannot be in a deck: " + (card.title || tid));
        var variant = entry.variant || catalog.default_variant;
        if (!catalog.variants.has(variant))
            throw new ActionError("Unknown variant: " + variant);
        var quantity = Number(entry.quantity);
        if (!Number.isInteger(quantity) || quantity < 1 || quantity > AdminItemsTool.quantity_max)
            throw new ActionError("Invalid quantity: " + (card.title || tid));
        mergeEntry(cards, {tid: tid, variant: variant, quantity: quantity}, ["tid", "variant"]);
    }

    return {title: title, hero: hero, cards: cards, clubs: getRequiredClubs(cards, catalog)};
};

//Same checks as UserDeckData.Audit for what the game server would refuse (rules from config, must match GameplayData).
//Works on any stored deck, also one with cards removed from the game since.
AdminItemsTool.getDeckIssues = (deck, catalog) => {
    var rules = getRules();
    var issues = [];
    var hero = deck.hero && deck.hero.tid ? catalog.cards.get(deck.hero.tid) : null;
    if (deck.hero && deck.hero.tid && (!hero || hero.type !== type_hero))
        issues.push("사용할 수 없는 영웅 카드: " + deck.hero.tid);

    var total = 0;
    var copies = new Map();
    var cards = (deck.cards || []).filter((c) => c && c.tid);
    for (var entry of cards) {
        var card = catalog.cards.get(entry.tid);
        if (!card)
            issues.push("존재하지 않는 카드: " + entry.tid);
        else if (!deck_card_types.includes(card.type))
            issues.push("편성할 수 없는 카드: " + (card.title || entry.tid));
        total += entry.quantity || 0;
        copies.set(entry.tid, (copies.get(entry.tid) || 0) + (entry.quantity || 0));
    }

    for (var [tid, count] of copies) {
        var card = catalog.cards.get(tid);
        if (!card)
            continue;
        var max = card.type === type_student ? rules.student_max : rules.non_student_max;
        if (count > max)
            issues.push("편성 한도를 넘은 카드: " + (card.title || tid) + " (" + count + "/" + max + ")");
    }
    if (total !== rules.deck_size)
        issues.push("덱 카드 수가 맞지 않습니다 (" + total + "/" + rules.deck_size + ")");

    var clubs = getRequiredClubs(cards, catalog);
    if (clubs.length > rules.club_size)
        issues.push("동아리 수가 너무 많습니다 (" + clubs.length + "/" + rules.club_size + ")");
    return issues;
};

//Replaces the deck with this tid, or adds a new deck when tid is empty. Returns {deck, previous}.
AdminItemsTool.saveDeck = (user, tid, deck) => {
    var index = tid ? user.decks.findIndex((d) => d && d.tid === tid) : -1;
    if (tid && index < 0)
        throw new ActionError("Deck not found: " + tid);

    var saved = Object.assign({tid: tid || newDeckTid(user)}, deck);
    var previous = index >= 0 ? user.decks[index] : null;
    if (index >= 0)
        user.decks[index] = saved;
    else
        user.decks.push(saved);
    return {deck: saved, previous: previous};
};

AdminItemsTool.removeDeck = (user, tid) => {
    var index = user.decks.findIndex((d) => d && d.tid === tid);
    if (index < 0)
        throw new ActionError("Deck not found: " + tid);
    return user.decks.splice(index, 1)[0];
};

AdminItemsTool.loadCatalog = loadCatalog;

module.exports = AdminItemsTool;
