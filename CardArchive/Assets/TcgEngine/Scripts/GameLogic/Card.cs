using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TcgEngine.Gameplay;

namespace TcgEngine
{
    //Represent the current state of a card during the game (data only)

    [System.Serializable]
    public class Card
    {
        public string card_id;
        public string uid;
        public int player_id;
        public string variant_id;

        public Slot slot;
        public bool exhausted;
        public int damage = 0;
        public int play_order = 0; //Order the card entered play (game_data.play_order_counter); 0 = never entered play

        //Target chosen while this card is played, for its OnPlay SelectTarget ability (AbilityData.IsPlaySelectTarget).
        //Only one kind is set at a time; cleared when the card starts a new play.
        public Slot play_target_slot = Slot.None;

        //Death phase (Phase 2): destroy effects mark the card dying instead of removing it immediately.
        //It stays on board (keeps its slot, keeps reacting to triggers) until the Death Creation Step
        //removes it. Healing cannot save a dying card; damage deaths (GetHP()<=0) are re-checked at the step.
        public bool dying = false;
        public DeathCause death_cause = DeathCause.Unknown;
        public string death_source_uid = null;   //Kill attribution (kill_count / OnKill), finalized at the death step
        public bool death_source_counter = false; //Killed by a counter-attack: kill credited but no OnKill trigger

        public int mana = 0;
        public int attack = 0;
        public int hp = 0;
        public CardWeapon weapon;
        public int range = 0;

        public int mana_ongoing = 0;
        public int attack_ongoing = 0;
        public int hp_ongoing = 0;
        public int range_ongoing = 0;

        public string equipped_uid = null;

        public List<CardTrait> traits = new List<CardTrait>();
        public List<CardTrait> ongoing_traits = new List<CardTrait>();

        public List<CardMarker> markers = new List<CardMarker>();
        public List<CardMarker> ongoing_markers = new List<CardMarker>();

        public List<CardClub> clubs = new List<CardClub>();
        public List<CardClub> ongoing_clubs = new List<CardClub>();

        public List<CardStatus> status = new List<CardStatus>();
        public List<CardStatus> ongoing_status = new List<CardStatus>();

        public List<string> abilities = new List<string>();
        public List<string> abilities_ongoing = new List<string>();

        [System.NonSerialized] private int hash = 0;
        [System.NonSerialized] private CardData data = null;
        [System.NonSerialized] private VariantData vdata = null;
        [System.NonSerialized] private List<AbilityData> abilities_data = null;

        [System.NonSerialized] public int prev_attack;
        [System.NonSerialized] public int prev_hp;
        [System.NonSerialized] public int prev_range;
        [System.NonSerialized] public bool stat_tracking_initialized;

        public Card(string card_id, string uid, int player_id) { this.card_id = card_id; this.uid = uid; this.player_id = player_id; }

        //Hearthstone rule: losing max HP (aura source gone, temp buff expired) never kills — the
        //card keeps its current HP, only capped at the new max. ClearOngoing snapshots the max
        //before UpdateOngoing recalculates the bonuses; ForgiveDamageOnHPMaxLoss then reduces
        //damage by the max-HP loss so current HP is preserved. Lethal damage already taken is
        //never forgiven (a card at 0 HP before the recalc stays at 0 and still dies).
        [System.NonSerialized] private int hpmax_snapshot;

        public virtual void Refresh() { exhausted = false; }
        public virtual void ClearOngoing() { hpmax_snapshot = GetHPMax(); ongoing_status.Clear(); ongoing_traits.Clear(); ongoing_markers.Clear(); ClearOngoingAbility(); attack_ongoing = 0; hp_ongoing = 0; mana_ongoing = 0; }

        public virtual void ForgiveDamageOnHPMaxLoss()
        {
            int loss = hpmax_snapshot - GetHPMax();
            if (loss > 0 && damage > 0)
                damage = Mathf.Max(damage - loss, 0);
        }

        public virtual void Clear()
        {
            ClearOngoing(); Refresh(); damage = 0; status.Clear();
            SetCard(CardData, VariantData); //Reset to initial stats
            equipped_uid = null;
            dying = false; death_source_uid = null; death_source_counter = false;
            death_cause = DeathCause.Unknown;
        }

        public virtual int GetAttack() { return Mathf.Max(attack + attack_ongoing, 0); }
        public virtual int GetHP() { return Mathf.Max(hp + hp_ongoing - damage, 0); }
        public virtual int GetHPMax() { return Mathf.Max(hp + hp_ongoing, 0); }
        public virtual int GetMana() { return Mathf.Max(mana + mana_ongoing, 0); }
        public virtual WeaponType GetWeaponType() { return weapon.GetWeaponType(); }
        public virtual int GetRange() { return Mathf.Max(range + range_ongoing, 0); }

        public virtual void SetCard(CardData icard, VariantData cvariant)
        {
            data = icard;
            card_id = icard.id;
            variant_id = cvariant.id;
            attack = icard.attack;
            hp = icard.hp;
            mana = icard.mana;

            SetWeapon(icard.weapon.GetWeaponID());

            SetTraits(icard);
            SetMarkers(icard);
            SetClubs(icard);
            SetAbilities(icard);
        }

        public void SetWeapon(string id)
        {
            CardWeapon new_weapon = new CardWeapon(id);

            if (new_weapon != null)
            {
                weapon = new_weapon;
                range = weapon.GetDefaultRange();
            }
        }

        public void SetTraits(CardData icard)
        {
            traits.Clear();
            foreach (TraitData trait in icard.traits)
                AddTrait(trait.id);
        }

        public void SetMarkers(CardData icard)
        {
            markers.Clear();
            if (icard.markers != null)
            {
                foreach (MarkerValue entry in icard.markers)
                    SetMarker(entry.marker.id, entry.value);
            }
        }

        public void SetClubs(CardData icard)
        {
            clubs.Clear();
            foreach (ClubData club in icard.clubs)
                SetClub(club.id);
        }

        public void SetAbilities(CardData icard)
        {
            abilities.Clear();
            abilities_ongoing.Clear();
            if (abilities_data != null)
                abilities_data.Clear();
            foreach (AbilityData ability in icard.abilities)
                AddAbility(ability);
        }
        
        //------ Traits (tribe, presence only) ---------

        public void AddTrait(string id)
        {
            if (GetTrait(id) == null)
                traits.Add(new CardTrait(id));
        }

        public void AddOngoingTrait(string id)
        {
            if (GetOngoingTrait(id) == null)
                ongoing_traits.Add(new CardTrait(id));
        }

        public void RemoveTrait(string id)
        {
            for (int i = traits.Count - 1; i >= 0; i--)
            {
                if (traits[i].id == id)
                    traits.RemoveAt(i);
            }
        }

        public CardTrait GetTrait(string id)
        {
            foreach (CardTrait trait in traits)
            {
                if (trait.id == id)
                    return trait;
            }
            return null;
        }

        public CardTrait GetOngoingTrait(string id)
        {
            foreach (CardTrait trait in ongoing_traits)
            {
                if (trait.id == id)
                    return trait;
            }
            return null;
        }

        public bool HasTrait(TraitData trait)
        {
            if (trait != null)
                return HasTrait(trait.id);
            return false;
        }

        public bool HasTrait(string id)
        {
            return GetTrait(id) != null || GetOngoingTrait(id) != null;
        }

        public List<CardTrait> GetAllTraits()
        {
            List<CardTrait> all_traits = new List<CardTrait>();
            all_traits.AddRange(traits);
            all_traits.AddRange(ongoing_traits);
            return all_traits;
        }

        //------ Markers (internal values, never displayed) ---------

        public void SetMarker(string id, int value)
        {
            CardMarker marker = GetMarker(id);
            if (marker != null)
                marker.value = value;
            else
                markers.Add(new CardMarker(id, value));
        }

        public void AddMarker(string id, int value)
        {
            CardMarker marker = GetMarker(id);
            if (marker != null)
                marker.value += value;
            else
                markers.Add(new CardMarker(id, value));
        }

        public void AddOngoingMarker(string id, int value)
        {
            CardMarker marker = GetOngoingMarker(id);
            if (marker != null)
                marker.value += value;
            else
                ongoing_markers.Add(new CardMarker(id, value));
        }

        public void RemoveMarker(string id)
        {
            for (int i = markers.Count - 1; i >= 0; i--)
            {
                if (markers[i].id == id)
                    markers.RemoveAt(i);
            }
        }

        public CardMarker GetMarker(string id)
        {
            foreach (CardMarker marker in markers)
            {
                if (marker.id == id)
                    return marker;
            }
            return null;
        }

        public CardMarker GetOngoingMarker(string id)
        {
            foreach (CardMarker marker in ongoing_markers)
            {
                if (marker.id == id)
                    return marker;
            }
            return null;
        }

        //Persistent value plus ongoing contributions. Missing marker counts as 0.
        public int GetMarkerValue(MarkerData marker)
        {
            if (marker == null)
                return 0;
            int val = 0;
            CardMarker base_marker = GetMarker(marker.id);
            CardMarker ongoing_marker = GetOngoingMarker(marker.id);
            if (base_marker != null)
                val += base_marker.value;
            if (ongoing_marker != null)
                val += ongoing_marker.value;
            return val;
        }

        public bool HasMarker(MarkerData marker)
        {
            return marker != null && (GetMarker(marker.id) != null || GetOngoingMarker(marker.id) != null);
        }

        public void SetClub(string id)
        {
            CardClub club = GetClub(id);
            if (club == null)
            {
                club = new CardClub(id);
                clubs.Add(club);
            }
        }

        public void AddClub(string id)
        {
            CardClub club = GetClub(id);
            if (club == null)
                SetClub(id);
        }

        public CardClub GetClub(string id)
        {
            foreach (CardClub club in clubs)
            {
                if (club.id == id)
                    return club;
            }
            return null;
        }

        public CardClub GetOngoingClub(string id)
        {
            foreach (CardClub club in ongoing_clubs)
            {
                if (club.id == id)
                    return club;
            }
            return null;
        }
        
        public bool HasClub(ClubData club)
        {
            if (club != null)
                return HasClub(club.id);
            return false;
        }

        public bool HasClub(string id)
        {
            return GetClub(id) != null || GetOngoingClub(id) != null;
        }

        public List<CardClub> GetAllClubs()
        {
            List<CardClub> all_clubs = new List<CardClub>();
            all_clubs.AddRange(clubs);
            all_clubs.AddRange(ongoing_clubs);
            return all_clubs;
        }
        
        //------  Status Effects ---------

        public void AddStatus(StatusData status, int value, int duration)
        {
            if (status != null)
                AddStatus(status.effect, value, duration);
        }

        public void AddOngoingStatus(StatusData status, int value)
        {
            if (status != null)
                AddOngoingStatus(status.effect, value);
        }

        public void AddStatus(StatusType type, int value, int duration)
        {
            if (type != StatusType.None)
            {
                CardStatus status = GetStatus(type);
                if (status == null)
                {
                    status = new CardStatus(type, value, duration);
                    this.status.Add(status);
                }
                else
                {
                    status.value += value;
                    status.duration = Mathf.Max(status.duration, duration);
                    status.permanent = status.permanent || duration == 0;
                }
            }
        }

        public void AddOngoingStatus(StatusType type, int value)
        {
            if (type != StatusType.None)
            {
                CardStatus status = GetOngoingStatus(type);
                if (status == null)
                {
                    status = new CardStatus(type, value, 0);
                    ongoing_status.Add(status);
                }
                else
                {
                    status.value += value;
                }
            }
        }

        public void RemoveStatus(StatusType type)
        {
            for (int i = status.Count - 1; i >= 0; i--)
            {
                if (status[i].type == type)
                    status.RemoveAt(i);
            }
        }

        public List<CardStatus> GetAllStatus()
        {
            List<CardStatus> all_status = new List<CardStatus>();
            all_status.AddRange(status);
            all_status.AddRange(ongoing_status);
            return all_status;
        }

        public bool HasStatus(StatusType type)
        {
            return GetStatus(type) != null || GetOngoingStatus(type) != null;
        }

        public CardStatus GetStatus(StatusType type)
        {
            foreach (CardStatus status in status)
            {
                if (status.type == type)
                    return status;
            }
            return null;
        }

        public CardStatus GetOngoingStatus(StatusType type)
        {
            foreach (CardStatus status in ongoing_status)
            {
                if (status.type == type)
                    return status;
            }
            return null;
        }

        public virtual int GetStatusValue(StatusType type)
        {
            CardStatus status1 = GetStatus(type);
            CardStatus status2 = GetOngoingStatus(type);
            int v1 = status1 != null ? status1.value : 0;
            int v2 = status2 != null ? status2.value : 0;
            return v1 + v2;
        }

        public virtual void ReduceStatusDurations()
        {
            for (int i = status.Count - 1; i >= 0; i--)
            {
                if (!status[i].permanent)
                {
                    status[i].duration -= 1;
                    if (status[i].duration <= 0)
                        status.RemoveAt(i);
                }
            }
        }

        //----- Abilities ------------

        public void AddAbility(AbilityData ability)
        {
            abilities.Add(ability.id);
			if (abilities_data != null)
				abilities_data.Add(ability);
        }

        public void RemoveAbility(AbilityData ability)
        {
            abilities.Remove(ability.id);
            if (abilities_data != null)
                abilities_data.Remove(ability);
        }

        public void AddOngoingAbility(AbilityData ability)
        {
            if (!abilities_ongoing.Contains(ability.id) && !abilities.Contains(ability.id))
            {
                abilities_ongoing.Add(ability.id);
                if (abilities_data != null)
                    abilities_data.Add(ability);
            }
        }

        public void ClearOngoingAbility()
        {
            if (abilities_data != null)
            {
                for (int i = abilities_data.Count - 1; i >= 0; i--)
                {
                    AbilityData ability = abilities_data[i];
                    if (abilities_ongoing.Contains(ability.id))
                        abilities_data.RemoveAt(i);
                }
            }

            abilities_ongoing.Clear();
        }

        public AbilityData GetAbility(AbilityTrigger trigger)
        {
            foreach (AbilityData iability in GetAbilities())
            {
                if (iability.trigger == trigger)
                    return iability;
            }
            return null;
        }

        public AbilityData GetAbility(AbilityTarget target)
        {
            foreach (AbilityData iability in GetAbilities())
            {
                if (iability.criteria_target == target)
                    return iability;
            }
            return null;
        }

        public bool HasAbility(AbilityData ability)
        {
            foreach (AbilityData iability in GetAbilities())
            {
                if (iability.id == ability.id)
                    return true;
            }
            return false;
        }

        public bool HasAbility(AbilityTrigger trigger)
        {
            AbilityData iability = GetAbility(trigger);
            if (iability != null)
                return true;
            return false;
        }

        public AbilityData GetAbility(AbilityTrigger trigger, AbilityTarget target)
        {
            foreach (AbilityData iability in GetAbilities())
            {
                if (iability.trigger == trigger && iability.criteria_target == target)
                    return iability;
            }
            return null;
        }

        public bool HasAbility(AbilityTrigger trigger, AbilityTarget target)
        {
            return GetAbility(trigger, target) != null;
        }

        public void SetPlayTarget(Card target)
        {
            SetPlayTarget(target.slot);
        }

        public void SetPlayTarget(Player target)
        {
            SetPlayTarget(new Slot(0, 0, target.player_id));
        }

        public void SetPlayTarget(Slot target)
        {
            ClearPlayTarget();
            play_target_slot = target;
        }

        public void ClearPlayTarget()
        {
            play_target_slot = Slot.None;
        }

        public bool HasActiveAbility(Game data, AbilityTrigger trigger)
        {
            AbilityData iability = GetAbility(trigger);
            if (iability != null && CanDoAbilities() && iability.AreTriggerConditionsMet(data, this))
                return true;
            return false;
        }

        public bool AreAbilityConditionsMet(AbilityTrigger ability_trigger, Game data, Card caster, Card triggerer, AbilityEventContext context = null)
        {
            foreach (AbilityData ability in GetAbilities())
            {
                if (ability && ability.trigger == ability_trigger && ability.AreTriggerConditionsMet(data, caster, triggerer, context: context))
                    return true;
            }
            return false;
        }

        public List<AbilityData> GetAbilities()
        {
            //Load abilities data, important to do this here since this array will be null after being sent through networking (cant serialize it)
            if (abilities_data == null)
            {
                abilities_data = new List<AbilityData>(abilities.Count + abilities_ongoing.Count);
                for (int i = 0; i < abilities.Count; i++)
                    abilities_data.Add(AbilityData.Get(abilities[i]));


                for (int i = 0; i < abilities_ongoing.Count; i++)
                    abilities_data.Add(AbilityData.Get(abilities_ongoing[i]));
            }

            //Return
            return abilities_data;
        }

        //---- Action Check ---------

        public virtual bool CanAttack(bool skip_cost = false)
        {
            if (CardData.IsPlace())
                return false;
            if (GetAttack() <= 0)
                return false;
            if (HasStatus(StatusType.Paralysed))
                return false;
            if (!skip_cost && exhausted)
                return false; //no more action
            return true;
        }
        /*
        public virtual bool CanAdditionAttack()
        {
            if (weapon_type == WeaponType.MT || weapon_type == WeaponType.GL || weapon_type == WeaponType.RL || weapon_type == WeaponType.FT)
                return true;
            return false;
        }
        */

        public virtual bool CanMove(bool skip_cost = false)
        {
            //In demo we can move freely, since it has no effect
            //if (HasStatusEffect(StatusEffect.Paralysed))
            //   return false;
            //if (!skip_cost && exhausted)
            //    return false; //no more action
            return true; 
        }

        public virtual bool CanDoActivatedAbilities()
        {
            if (HasStatus(StatusType.Paralysed))
                return false;
            if (HasStatus(StatusType.Silenced))
                return false;

            return true;
        }

        public virtual bool CanDoAbilities()
        {
            if (HasStatus(StatusType.Silenced))
                return false;
            return true;
        }

        public virtual bool CanDoAnyAction()
        {
            return CanAttack() || CanMove() || CanDoActivatedAbilities();
        }

        //----------------

        public CardData CardData 
        { 
            get { 
                if(data == null || data.id != card_id)
                    data = CardData.Get(card_id); //Optimization, store for future use
                return data;
            } 
        }

        public VariantData VariantData
        {
            get
            {
                if (vdata == null || vdata.id != variant_id)
                    vdata = VariantData.Get(variant_id); //Optimization, store for future use
                return vdata;
            }
        }

        public CardData Data => CardData; //Alternate name

        public int Hash
        {
            get {
                if (hash == 0)
                    hash = Mathf.Abs(uid.GetHashCode()); //Optimization, store for future use
                return hash;
            }
        }

        public static Card Create(CardData icard, VariantData ivariant, Player player)
        {
            return Create(icard, ivariant, player, GameTool.GenerateRandomID(11, 15));
        }

        public static Card Create(CardData icard, VariantData ivariant, Player player, string uid)
        {
            Card card = new Card(icard.id, uid, player.player_id);
            card.SetCard(icard, ivariant);
            player.cards_all[card.uid] = card;
            return card;
        }

        public static Card CloneNew(Card source)
        {
            Card card = new Card(source.card_id, source.uid, source.player_id);
            Clone(source, card);
            return card;
        }

        //Clone all card variables into another var, used mostly by the AI when building a prediction tree
        public static void Clone(Card source, Card dest)
        {
            dest.card_id = source.card_id;
            dest.uid = source.uid;
            dest.player_id = source.player_id;

            dest.variant_id = source.variant_id;
            dest.slot = source.slot;
            dest.exhausted = source.exhausted;
            dest.damage = source.damage;
            dest.play_order = source.play_order;
            dest.play_target_slot = source.play_target_slot;
            dest.dying = source.dying;
            dest.death_source_uid = source.death_source_uid;
            dest.death_cause = source.death_cause;
            dest.death_source_counter = source.death_source_counter;

            dest.attack = source.attack;
            dest.hp = source.hp;
            dest.mana = source.mana;
            dest.range = source.range;
            dest.weapon = source.weapon; //CardWeapon is an immutable id/data wrapper, safe to share by reference

            dest.mana_ongoing = source.mana_ongoing;
            dest.attack_ongoing = source.attack_ongoing;
            dest.hp_ongoing = source.hp_ongoing;
            dest.range_ongoing = source.range_ongoing;

            dest.equipped_uid = source.equipped_uid;

            CardTrait.CloneList(source.traits, dest.traits);
            CardTrait.CloneList(source.ongoing_traits, dest.ongoing_traits);
            CardMarker.CloneList(source.markers, dest.markers);
            CardMarker.CloneList(source.ongoing_markers, dest.ongoing_markers);
            CardClub.CloneList(source.clubs, dest.clubs);
            CardClub.CloneList(source.ongoing_clubs, dest.ongoing_clubs);
            CardStatus.CloneList(source.status, dest.status);
            CardStatus.CloneList(source.ongoing_status, dest.ongoing_status);
            GameTool.CloneList(source.abilities, dest.abilities); 
            GameTool.CloneList(source.abilities_ongoing, dest.abilities_ongoing); 
            GameTool.CloneListRefNull(source.abilities_data, ref dest.abilities_data); //No need to deep copy since AbilityData doesn't change dynamically, its just a reference
        }

        //Clone a var that could be null
        public static void CloneNull(Card source, ref Card dest)
        {
            //Source is null
            if (source == null)
            {
                dest = null;
                return;
            }

            //Dest is null
            if (dest == null)
            {
                dest = CloneNew(source);
                return;
            }

            //Both arent null, just clone
            Clone(source, dest);
        }

        //Clone dictionary completely
        public static void CloneDict(Dictionary<string, Card> source, Dictionary<string, Card> dest)
        {
            foreach (KeyValuePair<string, Card> pair in source)
            {
                bool valid = dest.TryGetValue(pair.Key, out Card val);
                if (valid)
                    Clone(pair.Value, val);
                else
                    dest[pair.Key] = CloneNew(pair.Value);
            }
        }

        //Clone list by keeping references from ref_dict
        public static void CloneListRef(Dictionary<string, Card> ref_dict, List<Card> source, List<Card> dest)
        {
            for (int i = 0; i < source.Count; i++)
            {
                Card scard = source[i];
                bool valid = ref_dict.TryGetValue(scard.uid, out Card rcard);
                if (valid)
                {
                    if (i < dest.Count)
                        dest[i] = rcard;
                    else
                        dest.Add(rcard);
                }
            }

            if(dest.Count > source.Count)
                dest.RemoveRange(source.Count, dest.Count - source.Count);
        }
    }

    [System.Serializable]
    public class CardStatus
    {
        public StatusType type;
        public int value;
        public int duration = 1;
        public bool bad_status;
        public bool permanent = true;

        [System.NonSerialized]
        private StatusData data = null;

        public CardStatus() { }

        public CardStatus(StatusType type, int value, int duration)
        {
            this.type = type;
            this.value = value;
            this.duration = duration;
            this.permanent = (duration == 0);
        }

        public StatusData StatusData { 
            get
            {
                if (data == null || data.effect != type)
                    data = StatusData.Get(type);
                return data;
            }
        }

        public StatusData Data => StatusData; //Alternate name

        public static CardStatus CloneNew(CardStatus copy)
        {
            CardStatus status = new CardStatus(copy.type, copy.value, copy.duration);
            status.permanent = copy.permanent;
            return status;
        }

        public static void Clone(CardStatus source, CardStatus dest)
        {
            dest.type = source.type;
            dest.value = source.value;
            dest.duration = source.duration;
            dest.permanent = source.permanent;
        }

        public static void CloneList(List<CardStatus> source, List<CardStatus> dest)
        {
            for (int i = 0; i < source.Count; i++)
            {
                if (i < dest.Count)
                    Clone(source[i], dest[i]);
                else
                    dest.Add(CloneNew(source[i]));
            }

            if (dest.Count > source.Count)
                dest.RemoveRange(source.Count, dest.Count - source.Count);
        }
    }

    [System.Serializable]
    public class CardTrait
    {
        public string id; //Tribe: presence only, no value

        [System.NonSerialized]
        private TraitData data = null;

        public CardTrait(string id)
        {
            this.id = id;
        }

        public CardTrait(TraitData trait)
        {
            this.id = trait.id;
        }

        public TraitData TraitData
        {
            get
            {
                if (data == null || data.id != id)
                    data = TraitData.Get(id);
                return data;
            }
        }

        public TraitData Data => TraitData; //Alternate name


        public static CardTrait CloneNew(CardTrait copy)
        {
            return new CardTrait(copy.id);
        }

        public static void Clone(CardTrait source, CardTrait dest)
        {
            dest.id = source.id;
        }

        public static void CloneList(List<CardTrait> source, List<CardTrait> dest)
        {
            for (int i = 0; i < source.Count; i++)
            {
                if (i < dest.Count)
                    Clone(source[i], dest[i]);
                else
                    dest.Add(CloneNew(source[i]));
            }

            if (dest.Count > source.Count)
                dest.RemoveRange(source.Count, dest.Count - source.Count);
        }
    }

    [System.Serializable]
    public class CardMarker
    {
        public string id; //Internal value used by rule logic, never displayed as a tag
        public int value;

        [System.NonSerialized]
        private MarkerData data = null;

        public CardMarker(string id, int value)
        {
            this.id = id;
            this.value = value;
        }

        public MarkerData MarkerData
        {
            get
            {
                if (data == null || data.id != id)
                    data = MarkerData.Get(id);
                return data;
            }
        }

        public static CardMarker CloneNew(CardMarker copy)
        {
            return new CardMarker(copy.id, copy.value);
        }

        public static void Clone(CardMarker source, CardMarker dest)
        {
            dest.id = source.id;
            dest.value = source.value;
        }

        public static void CloneList(List<CardMarker> source, List<CardMarker> dest)
        {
            for (int i = 0; i < source.Count; i++)
            {
                if (i < dest.Count)
                    Clone(source[i], dest[i]);
                else
                    dest.Add(CloneNew(source[i]));
            }

            if (dest.Count > source.Count)
                dest.RemoveRange(source.Count, dest.Count - source.Count);
        }
    }

    [System.Serializable]
    public class CardClub
    {
        public string id;

        [System.NonSerialized]
        private ClubData data = null;

        public CardClub(string id)
        {
            this.id = id;
        }

        public CardClub(ClubData club, int value)
        {
            this.id = club.id;
        }

        public ClubData ClubData
        {
            get
            {
                if (data == null || data.id != id)
                    data = ClubData.Get(id);
                return data;
            }
        }

        public ClubData Data => ClubData; //Alternate name

        public static CardClub CloneNew(CardClub copy)
        {
            CardClub status = new CardClub(copy.id);
            return status;
        }

        public static void Clone(CardClub source, CardClub dest)
        {
            dest.id = source.id;
        }

        public static void CloneList(List<CardClub> source, List<CardClub> dest)
        {
            for (int i = 0; i < source.Count; i++)
            {
                if (i < dest.Count)
                    Clone(source[i], dest[i]);
                else
                    dest.Add(CloneNew(source[i]));
            }

            if (dest.Count > source.Count)
                dest.RemoveRange(source.Count, dest.Count - source.Count);
        }
    }

    [System.Serializable]
    public class CardWeapon
    {
        public string id;

        [System.NonSerialized]
        private WeaponData data = null;

        public CardWeapon(string id)
        {
            this.id = id;
            this.data = WeaponData.Get(id);
        }

        public WeaponData WeaponData
        {
            get
            {
                if (data == null || data.GetWeaponID() != id)
                    data = WeaponData.Get(id);

                return data;
            }
        }

        public string GetWeaponID()
        {
            if (data == null)
                data = WeaponData;

            return data.GetWeaponID();
        }

        public WeaponType GetWeaponType()
        {
            if (data == null)
                data = WeaponData;

            return data.GetWeaponType();
        }

        public int GetDefaultRange()
        {
            if (data == null)
                data = WeaponData;

            return data.GetDefaultRange();
        }

        public Color32 GetWeaponColor()
        {
            if (data == null)
                data = WeaponData;

            return data.GetWeaponColor();
        }

        public List<Card> SearchTarget(GameLogic logic, Card attacker)
        {
            return WeaponData.SearchTarget(logic, attacker); //Lazy-load: data may be null on cloned/deserialized weapons
        }

        public void AttackTarget(GameLogic logic, Card attacker, List<Card>targets)
        {
            WeaponData.AttackTarget(logic, attacker, targets);
        }

        public void AttackTarget(GameLogic logic, Card attacker, Player player)
        {
            WeaponData.AttackTarget(logic, attacker, player);
        }
    }
}
