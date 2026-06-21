using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TcgEngine.AI
{
    /// <summary>
    /// Values and calculations for various values of the AI decision-making, adjusting these can improve your AI
    /// Heuristic: Represent the score of a board state, high score favor AI, low score favor the opponent
    /// Action Score: Represent the score of an individual action, to proritize actions if too many in a single node
    /// Action Sort Order: Value to determine the order actions should be executed in a single turn to avoid searching same things in different order, executed in ascending order
    /// </summary>

    public class AIHeuristic
    {
        //---------- Heuristic PARAMS -------------

        public int board_card_value = 20;       //Score of having cards on board
        public int secret_card_value = 10;       //Score of having cards in secret zone
        public int hand_card_value = 5;         //Score of having cards in hand
        public int kill_value = 5;              //Score of killing a card

        public int player_hp_value = 4;         //Score per player hp
        public int card_attack_value = 3;       //Score per board card attack
        public int card_hp_value = 2;           //Score per board card hp
        public int card_status_value = 15;       //Score per status on card (multiplied by hvalue of StatusData)

        //---------- Placement (slot) PARAMS -------------

        public int slot_coverage_value     = 8;   //Per enemy card within attack range from this slot (x2 for buildings)
        public int slot_kill_bonus         = 12;  //If a covered enemy can be killed this combat
        public int slot_face_value         = 30;  //Reaches enemy hero/base AND no enemy unit blocks range (win-condition)
        public int slot_face_value_blocked = 6;   //Reaches enemy base but an enemy unit in range will be hit first
        public int slot_exposure_value     = 6;   //Per enemy that can hit this slot (scaled by attacker value -> protect carries)
        public int slot_row_fit_value      = 5;   //Weapon type fits the row (BACK->inside, FRONT->outside)

        //-----------

        private int ai_player_id;           //ID of this AI, usually the human is 0 and AI is 1
        private int ai_level;               //ai level (level 10 is the best, level 1 is the worst)
        private int heuristic_modifier;     //Randomize heuristic for lower level ai
        private int placement_epsilon;      //Slot-score band width for placement noise (separate scale from heuristic_modifier)
        private System.Random random_gen;
        private readonly HashSet<ClubData> club_buf = new HashSet<ClubData>(); //Reused, single AI thread
        private readonly Dictionary<Slot, int> threat_buf = new Dictionary<Slot, int>(); //Reused: enemy threat count per slot
        private readonly List<Slot> cand_slots = new List<Slot>();   //Reused: playable slot candidates
        private readonly List<int> cand_scores = new List<int>();    //Reused: base score per candidate (parallel to cand_slots)

        public AIHeuristic(int player_id, int level)
        {
            ai_player_id = player_id;
            ai_level = level;
            heuristic_modifier = GetHeuristicModifier();
            placement_epsilon = GetPlacementEpsilon();
            random_gen = new System.Random();
        }

        public int CalculateHeuristic(Game data, NodeState node)
        {
            Player aiplayer = data.GetPlayer(ai_player_id);
            Player oplayer = data.GetOpponentPlayer(ai_player_id);
            return CalculateHeuristic(data, node, aiplayer, oplayer);
        }

        //Calculate full heuristic
        //Should return a value between -10000 and 10000 (unless its a win)
        public int CalculateHeuristic(Game data, NodeState node, Player aiplayer, Player oplayer)
        {
            int score = 0;

            //Victories
            if (aiplayer.IsDead())
                score += -100000 + node.tdepth * 1000; //Add node depth to seek surviving longest
            if (oplayer.IsDead())
                score += 100000 - node.tdepth * 1000; //Reduce node depth to seek fastest win

            //Board state
            score += aiplayer.cards_board.Count * board_card_value;
            score += aiplayer.cards_equip.Count * board_card_value;
            score += aiplayer.cards_secret.Count * secret_card_value;
            score += aiplayer.cards_hand.Count * hand_card_value;
            score += aiplayer.kill_count * kill_value;
            score += aiplayer.hp * player_hp_value;

            score -= oplayer.cards_board.Count * board_card_value;
            score -= oplayer.cards_equip.Count * board_card_value;
            score -= oplayer.cards_secret.Count * secret_card_value;
            score -= oplayer.cards_hand.Count * hand_card_value;
            score -= oplayer.kill_count * kill_value;
            score -= oplayer.hp * player_hp_value;


            foreach (Card card in aiplayer.cards_board)
            {
                score += card.GetAttack() * card_attack_value;
                score += card.GetHP() * card_hp_value;

                foreach (CardStatus status in card.status)
                    score += status.StatusData.hvalue * card_status_value;
                foreach (CardStatus status in card.ongoing_status)
                    score += status.StatusData.hvalue * card_status_value;
            }
            foreach (Card card in oplayer.cards_board)
            {
                score -= card.GetAttack() * card_attack_value;
                score -= card.GetHP() * card_hp_value;

                foreach (CardStatus status in card.status)
                    score -= status.StatusData.hvalue * card_status_value;
                foreach (CardStatus status in card.ongoing_status)
                    score -= status.StatusData.hvalue * card_status_value;
            }

            //Club synergy (position-independent, each club scored on its own)
            score += GetClubScore(data, aiplayer);
            score -= GetClubScore(data, oplayer);

            if (heuristic_modifier > 0)
                score += random_gen.Next(-heuristic_modifier, heuristic_modifier);

            return score;
        }

        //Score the board-composition value of a player's clubs.
        //Each club is evaluated independently using its ClubSynergyType; other clubs never matter.
        private int GetClubScore(Game data, Player player)
        {
            club_buf.Clear();
            foreach (Card card in player.cards_board)
            {
                foreach (CardClub cc in card.GetAllClubs())
                {
                    if (cc.ClubData != null)
                        club_buf.Add(cc.ClubData);
                }
            }

            int total = 0;
            foreach (ClubData club in club_buf)
            {
                int n = data.GetClubCount(player, club); //Count of this club on board
                if (n <= 0)
                    continue;

                switch (club.synergy_type)
                {
                    case ClubSynergyType.OnOff:
                        if (n >= club.synergy_threshold)
                            total += club.synergy_value; //Fixed once active, stacking adds nothing
                        break;
                    case ClubSynergyType.Count:
                        total += club.synergy_value * n; //More of this club = better
                        break;
                    case ClubSynergyType.Individual:
                        break; //No synergy value
                }
            }
            return total;
        }

        //--------- Placement ---------

        //Pick the single best empty slot to play 'card' on (m=1).
        //Only playable slots are considered, so the returned slot is always valid (or Slot.None).
        public Slot GetBestSlot(Game data, Player player, Card card, List<Slot> empty_mem)
        {
            if (empty_mem != null)
                empty_mem.Clear();
            //Include neutral slots as summon candidates (not just the player's own side)
            List<Slot> empties = empty_mem != null ? empty_mem : new List<Slot>();
            foreach (Slot s in Slot.GetAllIncludeNeutral(player.player_id))
            {
                if (data.GetSlotCard(s) == null)
                    empties.Add(s);
            }
            Player oplayer = data.GetOpponentPlayer(player.player_id);

            //Precompute enemy threat once: how many enemies can hit each slot.
            //This does NOT depend on where we place our card, so it is hoisted out of the per-slot loop.
            threat_buf.Clear();
            foreach (Card e in oplayer.cards_board)
            {
                foreach (Slot ts in e.slot.GetNeighborSlot(e.GetRange()))
                {
                    threat_buf.TryGetValue(ts, out int c);
                    threat_buf[ts] = c + 1;
                }
            }

            //Pass 1: deterministic base score for every playable slot, and track the maximum.
            cand_slots.Clear();
            cand_scores.Clear();
            int max_score = int.MinValue;
            for (int i = 0; i < empties.Count; i++)
            {
                Slot s = empties[i];
                if (!data.CanPlayCard(card, s))
                    continue; //e.g. Place cards may only go on outside slots

                int sc = EvaluateSlot(data, player, oplayer, card, s, threat_buf);
                cand_slots.Add(s);
                cand_scores.Add(sc);
                if (sc > max_score)
                    max_score = sc;
            }

            if (cand_slots.Count == 0)
                return Slot.None; //No playable slot

            //Pass 2: noise as an epsilon BAND (not an additive override). Only slots within 'epsilon' of the
            //best are candidates, so a clearly-better slot is alone in the band and always wins; near-ties get
            //randomized. epsilon = placement_epsilon (own curve, sized to EvaluateSlot's scale; 0 at lvl10).
            int epsilon = placement_epsilon;
            int threshold = max_score - epsilon;
            Slot best = Slot.None;
            int tie = 0;
            for (int i = 0; i < cand_slots.Count; i++)
            {
                if (cand_scores[i] < threshold)
                    continue; //outside the band
                tie++;
                if (random_gen.Next(tie) == 0) //reservoir sampling -> uniform among band members
                    best = cand_slots[i];
            }

            return best;
        }

        //Static placement score for putting 'card' on slot 's' (no game cloning).
        //Considers: attack-range coverage, reaching the enemy base (conditional), survival exposure, weapon-row fit.
        //'threat' = precomputed enemy-threat count per slot (see GetBestSlot).
        public int EvaluateSlot(Game data, Player player, Player oplayer, Card card, Slot s, Dictionary<Slot, int> threat)
        {
            List<Slot> reach = s.GetNeighborSlot(card.GetRange());
            int score = 0;

            //(1) Enemy cards within attack range from this slot
            bool has_unit_target = false;
            foreach (Card e in oplayer.cards_board)
            {
                if (!reach.Contains(e.slot))
                    continue;
                has_unit_target = true;
                score += slot_coverage_value;
                if (e.CardData.IsPlace())
                    score += slot_coverage_value;       //Buildings are priority targets
                if (card.GetAttack() >= e.GetHP())
                    score += slot_kill_bonus;            //Can kill it this combat
            }

            //(1b) Reaching the enemy base (face). Combat hits units first, so weight by whether range is blocked.
            List<Slot> enemy_inside = Slot.GetInsideSlot(oplayer.player_id);
            bool face_reach = false;
            for (int i = 0; i < reach.Count; i++)
            {
                if (enemy_inside.Contains(reach[i]))
                {
                    face_reach = true;
                    break;
                }
            }
            if (face_reach)
                score += has_unit_target ? slot_face_value_blocked : slot_face_value;

            //(2) Survival exposure - O(1) lookup of how many enemies cover this slot (scale by attack -> protect carries)
            threat.TryGetValue(s, out int threat_count);
            if (threat_count > 0)
                score -= slot_exposure_value * (1 + card.GetAttack() / 4) * threat_count;

            //(3) Weapon-row fit: ranged units want the back (inside), melee wants the front (outside)
            bool inside = Slot.GetInsideSlot(player.player_id).Contains(s);
            WeaponType wtype = card.weapon != null ? card.GetWeaponType() : WeaponType.NONE;
            if (wtype == WeaponType.BACK && inside)
                score += slot_row_fit_value;
            if (wtype == WeaponType.FRONT && !inside)
                score += slot_row_fit_value;

            return score;
        }

        //This calculates the score of an individual action, instead of the board state
        //When too many actions are possible in a single node, only the ones with best action score will be evaluated
        //Make sure to return a positive value
        public int CalculateActionScore(Game data, AIAction order)
        {
            if (order.type == GameAction.EndTurn)
                return 0; //Other orders are better

            if (order.type == GameAction.CancelSelect)
                return 0; //Other orders are better

            if (order.type == GameAction.CastAbility)
            {
                return 200;
            }

            if (order.type == GameAction.PlayCard)
            {
                Player player = data.GetPlayer(ai_player_id);
                Card card = data.GetCard(order.card_uid);
                if (card.CardData.IsBoardCard())
                    return 200 + (card.GetMana() * 5) - (30 * player.cards_board.Count); //High cost cards are better to play, better to play when not a lot of cards in play
                else if (card.CardData.IsEquipment())
                    return 200 + (card.GetMana() * 5) - (30 * player.cards_equip.Count);
                else
                    return 200 + (card.GetMana() * 5);
            }

            if (order.type == GameAction.Move)
            {
                return 100;
            }

            return 100; //Other actions are better than End/Cancel
        }

        //Within the same turn, actions can only be executed in sorting order, make sure it returns positive value higher than 0 or it wont be sorted
        //This prevents calculating all possibilities of A->B->C  B->C->A   C->A->B  etc..
        //If two AIActions with same sorting value, or if sorting value is 0, ai will test all ordering variations (slower)
        //This would not be necessary in a game with only 1 action per turn (such as chess) but is useful for AI that can perform multiple actions in 1 turn
        //Ordering could be improved, pretty much random now
        public int CalculateActionSort(Game data, AIAction order)
        {
            if (order.type == GameAction.EndTurn)
                return 0; //End turn can always be performed, 0 means any order
            if (data.selector != SelectorType.None)
                return 0; //Selector actions not affected by sorting

            Card card = data.GetCard(order.card_uid);
            Card target = order.target_uid != null ? data.GetCard(order.target_uid) : null;
            bool is_spell = card != null && !card.CardData.IsBoardCard();

            int type_sort = 0;
            if (order.type == GameAction.PlayCard && is_spell)
                type_sort = 1; //Play Spells first
            if (order.type == GameAction.CastAbility)
                type_sort = 2; //Card Abilities second
            if (order.type == GameAction.Move)
                type_sort = 3; //Move third
            if (order.type == GameAction.PlayCard && !is_spell)
                type_sort = 7; //Play Citizens last

            int card_sort = card != null ? (card.Hash % 100) : 0;
            int target_sort = target != null ? (target.Hash % 100) : 0;
            int sort = type_sort * 10000 + card_sort * 100 + target_sort + 1;
            return sort;
        }

        //Lower level AI add a random number to their heuristic
        private int GetHeuristicModifier()
        {
            if (ai_level >= 10)
                return 0;
            if (ai_level == 9)
                return 5;
            if (ai_level == 8)
                return 10;
            if (ai_level == 7)
                return 20;
            if (ai_level == 6)
                return 30;
            if (ai_level == 5)
                return 40;
            if (ai_level == 4)
                return 50;
            if (ai_level == 3)
                return 75;
            if (ai_level == 2)
                return 100;
            if (ai_level <= 1)
                return 200;
            return 0;
        }

        //Placement noise band width, on EvaluateSlot's score scale (~0-60), separate from heuristic_modifier.
        //0 at lvl10 (strict best); widens at lower levels so weaker AIs vary placement among comparable slots.
        private int GetPlacementEpsilon()
        {
            if (ai_level >= 10)
                return 0;
            if (ai_level == 9)
                return 1;
            if (ai_level == 8)
                return 2;
            if (ai_level == 7)
                return 4;
            if (ai_level == 6)
                return 6;
            if (ai_level == 5)
                return 8;
            if (ai_level == 4)
                return 10;
            if (ai_level == 3)
                return 14;
            if (ai_level == 2)
                return 18;
            if (ai_level <= 1)
                return 25;
            return 0;
        }

        //Check if this node represent one of the players winning
        public bool IsWin(NodeState node)
        {
            return node.hvalue > 50000 || node.hvalue < -50000;
        }

    }
}
