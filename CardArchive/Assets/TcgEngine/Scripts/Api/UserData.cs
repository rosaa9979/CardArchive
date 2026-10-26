using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace TcgEngine
{
    /// <summary>
    /// Contain UserData retrieved from the web api database
    /// </summary>


    [System.Serializable]
    public class UserData
    {
        public string id;
        public string username;

        public string email;
        public string avatar;
        public string cardback;
        public int permission_level = 1;
        public int validation_level = 1;

        public int coins;
        public int xp;
        public int elo;

        public int matches;
        public int victories;
        public int defeats;

        public UserCardData[] cards;
        public UserCardData[] packs;
        public UserDeckData[] decks;
        public string[] rewards;
        public string[] avatars;
        public string[] cardbacks;
        public string[] friends;

        public UserData()
        {
            cards = new UserCardData[0];
            packs = new UserCardData[0];
            decks = new UserDeckData[0];
            rewards = new string[0];
            avatars = new string[0];
            cardbacks = new string[0];
            friends = new string[0];
            permission_level = 1;
            coins = 10000;
            elo = 1000;
        }

        public int GetLevel()
        {
            return Mathf.FloorToInt(xp / 1000) + 1;
        }

        public string GetAvatar()
        {
            if (avatar != null)
                return avatar;
            return "";
        }

        public string GetCardback()
        {
            if (cardback != null)
                return cardback;
            return "";
        }

        public void SetDeck(UserDeckData deck)
        {
            for(int i=0; i<decks.Length; i++)
            {
                if (decks[i].tid == deck.tid)
                {
                    decks[i] = deck;
                    return;
                }
            }

            //Not found
            List<UserDeckData> ldecks = new List<UserDeckData>(decks);
            ldecks.Add(deck);
            this.decks = ldecks.ToArray();
        }

        public UserDeckData GetDeck(string tid)
        {
            foreach (UserDeckData deck in decks)
            {
                if (deck.tid == tid)
                    return deck;
            }
            return null;
        }

        public UserCardData GetCard(string tid, string variant)
        {
            foreach (UserCardData card in cards)
            {
                if (card.tid == tid && card.variant == variant)
                    return card;
            }
            return null;
        }

        public int GetCardQuantity(CardData card, VariantData variant)
        {
            return GetCardQuantity(card.id, variant.id, variant.is_default);
        }

        public int GetCardQuantity(string tid, string variant, bool default_variant = false)
        {
            if (cards == null)
                return 0;

            foreach (UserCardData card in cards)
            {
                if (card.tid == tid && card.variant == variant)
                    return card.quantity;
                if (card.tid == tid && card.variant == "" && default_variant)
                    return card.quantity;
            }
            return 0;
        }

        public UserCardData GetPack(string tid)
        {
            foreach (UserCardData pack in packs)
            {
                if (pack.tid == tid)
                    return pack;
            }
            return null;
        }

        public int GetPackQuantity(string tid)
        {
            if (packs == null)
                return 0;

            foreach (UserCardData pack in packs)
            {
                if (pack.tid == tid)
                    return pack.quantity;
            }
            return 0;
        }

        public int CountUniqueCards()
        {
            if (cards == null)
                return 0;

            HashSet<string> unique_cards = new HashSet<string>();
            foreach (UserCardData card in cards)
            {
                if (!unique_cards.Contains(card.tid))
                    unique_cards.Add(card.tid);
            }
            return unique_cards.Count;
        }

        public int CountCardType(VariantData variant)
        {
            int value = 0;
            foreach (UserCardData card in cards)
            {
                if (card.variant == variant.id)
                    value += 1;
            }
            return value;
        }

        public bool HasDeckCards(UserDeckData deck)
        {
            // 모든 카드를 2장씩 지금했으므로 항상 가지고 있다고 가정
            /*
            foreach (UserCardData card in deck.cards)
            {
                bool default_variant = true; //Count "" variant as valid for compatibilty with older vers
                if (GetCardQuantity(card.tid, card.variant, default_variant) < card.quantity)
                    return false;
            }
            */

            return true;
        }

        //Audit every saved deck against the current card data. Repairable decks are fixed in place
        //and returned so the caller can save them; Invalid decks are only counted.
        public List<UserDeckData> RepairDecks(out int invalid_count)
        {
            List<UserDeckData> repaired = new List<UserDeckData>();
            invalid_count = 0;

            foreach (UserDeckData deck in decks ?? new UserDeckData[0])
            {
                if (deck == null)
                    continue;

                DeckAuditResult audit = deck.Audit();
                if (audit.status == DeckAuditStatus.Invalid)
                {
                    invalid_count++;
                    Debug.Log("Deck audit: " + deck.title + " is invalid: " + string.Join(", ", audit.issues));
                }
                else if (audit.status == DeckAuditStatus.Repairable)
                {
                    deck.RepairDerived();
                    repaired.Add(deck);
                }
            }
            return repaired;
        }

        public bool IsDeckValid(UserDeckData deck)
        {
            if (Authenticator.Get().IsApi())
                return HasDeckCards(deck) && deck.IsValid();
            return deck.IsValid();
        }

        public void AddDeck(UserDeckData deck)
        {
            List<UserDeckData> udecks = new List<UserDeckData>(decks);
            udecks.Add(deck);
            decks = udecks.ToArray();

            foreach (UserCardData card in deck.cards)
            {
                AddCard(card.tid, card.variant, 1);
            }
        }
    
        public void AddPack(string tid, int quantity)
        {
            bool found = false;
            foreach (UserCardData pack in packs)
            {
                if (pack.tid == tid)
                {
                    found = true;
                    pack.quantity += quantity;
                }
            }
            if (!found)
            {
                UserCardData npack = new UserCardData();
                npack.tid = tid;
                npack.quantity = quantity;
                List<UserCardData> apacks = new List<UserCardData>(packs);
                apacks.Add(npack);
                packs = apacks.ToArray();
            }
        }

        public void AddCard(string tid, string variant, int quantity)
        {
            bool found = false;
            foreach (UserCardData card in cards)
            {
                if (card.tid == tid && card.variant == variant)
                {
                    found = true;
                    card.quantity += quantity;
                }
            }
            if (!found)
            {
                UserCardData ncard = new UserCardData();
                ncard.tid = tid;
                ncard.variant = variant;
                ncard.quantity = quantity;
                List<UserCardData> acards = new List<UserCardData>(cards);
                acards.Add(ncard);
                cards = acards.ToArray();
            }
        }

        public void AddReward(string tid)
        {
            if (!HasReward(tid))
            {
                List<string> arewards = new List<string>(rewards);
                arewards.Add(tid);
                rewards = arewards.ToArray();
            }
        }

        public bool HasCard(string card_tid, string variant, int quantity = 1)
        {
            foreach (UserCardData card in cards)
            {
                if (card.tid == card_tid && card.variant == variant && card.quantity >= quantity)
                    return true;
            }
            return false;
        }

        public bool HasPack(string pack_tid, int quantity=1)
        {
            foreach (UserCardData pack in packs)
            {
                if (pack.tid == pack_tid && pack.quantity >= quantity)
                    return true;
            }
            return false;
        }

        public bool HasReward(string reward_id)
        {
            foreach (string reward in rewards)
            {
                if (reward == reward_id)
                    return true;
            }
            return false;
        }

        public string GetCoinsString()
        {
            return coins.ToString();
        }

        public bool HasFriend(string username)
        {
            List<string> flist = new List<string>(friends);
            return flist.Contains(username);
        }

        public void AddFriend(string username)
        {
            List<string> flist = new List<string>(friends);
            if (!flist.Contains(username))
                flist.Add(username);
            friends = flist.ToArray();
        }

        public void RemoveFriend(string username)
        {
            List<string> flist = new List<string>(friends);
            if (flist.Contains(username))
                flist.Remove(username);
            friends = flist.ToArray();
        }
    }

    [System.Serializable]
    public class UserDeckData : INetworkSerializable
    {
        public string tid;
        public string title;
        public UserCardData hero;
        public UserCardData[] clubs;
        public UserCardData[] cards;

        public UserDeckData() {}

        public UserDeckData(string tid, string title)
        {
            this.tid = tid;
            this.title = title;
            hero = new UserCardData();
            clubs = new UserCardData[0];
            cards = new UserCardData[0];
        }

        public UserDeckData(DeckData deck)
        {
            tid = deck.id;
            title = deck.title;
            hero = new UserCardData(deck.hero, VariantData.GetDefault());
            clubs = new UserCardData[deck.clubs.Length];
            cards = new UserCardData[deck.cards.Length];

            for (int i = 0; i < deck.clubs.Length; i++)
            {
                clubs[i] = new UserCardData(deck.clubs[i], VariantData.GetDefault());
            }

            for (int i = 0; i < deck.cards.Length; i++)
            {
                cards[i] = new UserCardData(deck.cards[i], VariantData.GetDefault());
            }
        }

        public int GetQuantity()
        {
            int count = 0;
            foreach (UserCardData card in cards)
                count += card.quantity;
            return count;
        }

        public int GetClubQuantity()
        {
            return clubs.Length;
        }

        public bool IsValid()
        {
            return Audit().IsPlayable;
        }

        //Self check against the current card data. Card data can change after a deck was saved,
        //so the stored deck is never trusted: removed or disabled cards make it Invalid, and
        //data derived from the cards (clubs) that went stale makes it Repairable.
        public DeckAuditResult Audit()
        {
            DeckAuditResult result = new DeckAuditResult();
            GameplayData gdata = GameplayData.Get();

            if (string.IsNullOrEmpty(tid))
                result.AddError("덱 ID가 없습니다");
            if (string.IsNullOrWhiteSpace(title))
                result.AddError("덱 이름이 없습니다");

            if (hero != null && !string.IsNullOrEmpty(hero.tid))
            {
                CardData hdata = CardData.Get(hero.tid);
                if (hdata == null || !hdata.IsArona())
                    result.AddError("사용할 수 없는 영웅 카드: " + hero.tid);
            }

            int total = 0;
            Dictionary<string, int> copies = new Dictionary<string, int>();
            foreach (UserCardData card in cards ?? new UserCardData[0])
            {
                CardData icard = card != null ? CardData.Get(card.tid) : null;
                if (icard == null)
                {
                    result.AddError("존재하지 않는 카드: " + card?.tid);
                    continue;
                }
                if (!icard.deckbuilding || !IsDeckCardType(icard))
                    result.AddError("편성할 수 없는 카드: " + icard.title);
                if (card.quantity <= 0)
                    result.AddError("수량이 잘못된 카드: " + icard.title);

                total += card.quantity;
                copies.TryGetValue(icard.id, out int count);
                copies[icard.id] = count + card.quantity;
            }

            foreach (KeyValuePair<string, int> pair in copies)
            {
                CardData icard = CardData.Get(pair.Key);
                int max = icard.IsStudent() ? gdata.deck_student_duplicate_max : gdata.deck_non_student_duplicate_max;
                if (pair.Value > max)
                    result.AddError("편성 한도를 넘은 카드: " + icard.title + " (" + pair.Value + "/" + max + ")");
            }

            if (total != gdata.deck_size)
                result.AddError("덱 카드 수가 맞지 않습니다 (" + total + "/" + gdata.deck_size + ")");

            UserCardData[] required = GetRequiredClubs(cards);
            if (required.Length > gdata.club_size)
                result.AddError("동아리 수가 너무 많습니다 (" + required.Length + "/" + gdata.club_size + ")");
            else if (!HasSameClubs(required))
                result.MarkRepairable("동아리 구성이 카드와 맞지 않습니다");

            return result;
        }

        //Rebuild everything derived from the cards. Call it before using or saving a Repairable deck.
        public void RepairDerived()
        {
            clubs = GetRequiredClubs(cards);
        }

        //Club cards the deck needs, derived from the current clubs of its cards, sorted by id
        public static UserCardData[] GetRequiredClubs(IEnumerable<UserCardData> deck_cards)
        {
            List<CardData> all_clubs = CardData.GetAllClub();
            SortedDictionary<string, UserCardData> required = new SortedDictionary<string, UserCardData>(System.StringComparer.Ordinal);

            foreach (UserCardData card in deck_cards ?? new UserCardData[0])
            {
                CardData icard = card != null ? CardData.Get(card.tid) : null;
                if (icard == null || icard.clubs == null)
                    continue;

                foreach (ClubData club in icard.clubs)
                {
                    if (club == null)
                        continue;

                    foreach (CardData aclub in all_clubs)
                    {
                        if (aclub.clubs != null && System.Array.Exists(aclub.clubs, c => c != null && c.id == club.id))
                        {
                            if (!required.ContainsKey(aclub.id))
                                required[aclub.id] = new UserCardData(aclub, VariantData.GetDefault());
                            break;
                        }
                    }
                }
            }

            return new List<UserCardData>(required.Values).ToArray();
        }

        private bool HasSameClubs(UserCardData[] required)
        {
            HashSet<string> stored = new HashSet<string>();
            foreach (UserCardData club in clubs ?? new UserCardData[0])
            {
                if (club != null)
                    stored.Add(club.tid);
            }

            HashSet<string> expected = new HashSet<string>();
            foreach (UserCardData club in required)
                expected.Add(club.tid);

            return stored.SetEquals(expected) && (clubs?.Length ?? 0) == required.Length;
        }

        private static bool IsDeckCardType(CardData card)
        {
            return card.type == CardType.Student || card.type == CardType.NonStudent
                || card.type == CardType.Spell || card.type == CardType.Place;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref tid);
            serializer.SerializeValue(ref title);
            serializer.SerializeValue(ref hero);
            NetworkTool.NetSerializeArray(serializer, ref clubs);
            NetworkTool.NetSerializeArray(serializer, ref cards);
        }

        public static UserDeckData Default
        {
            get
            {
                UserDeckData deck = new UserDeckData();
                deck.tid = "";
                deck.title = "";
                deck.hero = new UserCardData();
                deck.clubs = new UserCardData[0];
                deck.cards = new UserCardData[0];
                return deck;
            }
        }
    }

    public enum DeckAuditStatus
    {
        Valid = 0,
        Repairable = 1, //Only data derived from the cards is stale, RepairDerived() fixes it
        Invalid = 2,
    }

    //Result of UserDeckData.Audit(), never stored in the deck
    public class DeckAuditResult
    {
        public DeckAuditStatus status = DeckAuditStatus.Valid;
        public List<string> issues = new List<string>();

        public bool IsPlayable => status != DeckAuditStatus.Invalid;

        public void AddError(string issue)
        {
            status = DeckAuditStatus.Invalid;
            issues.Add(issue);
        }

        public void MarkRepairable(string issue)
        {
            if (status == DeckAuditStatus.Valid)
                status = DeckAuditStatus.Repairable;
            issues.Add(issue);
        }

        public string GetFirstIssue()
        {
            return issues.Count > 0 ? issues[0] : "";
        }
    }

    [System.Serializable]
    public class UserCardData : INetworkSerializable
    {
        public string tid;
        public string variant;
        public int quantity;

        public UserCardData() { tid = ""; variant = ""; quantity = 1; }
        public UserCardData(string id, string v) { tid = id; variant = v; quantity = 1; }
        public UserCardData(CardData card, VariantData variant) 
        {
            this.tid = card != null ? card.id : "";
            this.variant = variant != null ? variant.id : "";
            this.quantity = 1;
        }
        public UserCardData(UserCardData other)
        {
            this.tid = other.tid;
            this.variant = other.variant;
            this.quantity = other.quantity;
        }


        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref tid);
            serializer.SerializeValue(ref variant);
            serializer.SerializeValue(ref quantity);
        }
    }

}

