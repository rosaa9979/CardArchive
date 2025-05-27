using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TcgEngine
{

    //Pick X number of targets at random from the source array (count)

    [CreateAssetMenu(fileName = "filter", menuName = "TcgEngine/Filter/MostWoundedSlot", order = 10)]
    public class FilterMostWoundedSlot : FilterData
    {
        //public int amount = 1; //Number of random targets selected
        public int distance;
        public FilterPlayerType player_type;

        //public override List<Card> FilterTargets(Game data, AbilityData ability, Card caster, List<Card> source, List<Card> dest)
        //{

        //}

        //public override List<Player> FilterTargets(Game data, AbilityData ability, Card caster, List<Player> source, List<Player> dest)
        //{

        //}

        public override List<Slot> FilterTargets(Game data, AbilityData ability, Card caster, List<Slot> source, List<Slot> dest)
        {
            int maxWounded = -1;
            List<Slot> tmp = new List<Slot>();

            foreach (Slot slot in source)
            {
                int wounded = GetWoundedCount(data, caster, slot);

                if (wounded > maxWounded)
                {
                    maxWounded = wounded;
                    tmp.Clear();
                    tmp.Add(slot);
                }

                else if (wounded == maxWounded)
                    tmp.Add(slot);
            }

            return GameTool.PickXRandom(tmp, dest, 1);
        }

        //public override List<CardData> FilterTargets(Game data, AbilityData ability, Card caster, List<CardData> source, List<CardData> dest)
        //{

        //}

        private int GetWoundedCount(Game data, Card caster, Slot slot)
        {
            Player player = data.GetPlayer(caster.player_id);
            Player oplayer = data.GetOpponentPlayer(player.player_id);

            int amount = 0;
            List<Slot> slot_list = slot.GetNeighborSlot(distance);

            foreach (Slot s in slot_list)
            {
                if (s != slot)
                {
                    Card unit = data.GetSlotCard(s);


                    if (unit != null)
                    {
                        if (unit.damage > 0)
                        {
                            if (player_type == FilterPlayerType.Both)
                                amount += 1;

                            else if (player_type == FilterPlayerType.Self && unit.player_id == player.player_id)
                                amount += 1;

                            else if (player_type == FilterPlayerType.Opponent && unit.player_id == oplayer.player_id)
                                amount += 1;
                        }
                    }
                }
            }

            return amount;
        }
    }
}
