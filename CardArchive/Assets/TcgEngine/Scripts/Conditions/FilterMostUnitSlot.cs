using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TcgEngine
{
    public enum FilterPlayerType
    {
        Self = 0,
        Opponent = 1,
        Both = 2,
    }

    //Pick X number of targets at random from the source array (count)

    [CreateAssetMenu(fileName = "filter", menuName = "TcgEngine/Filter/MostUnitSlot", order = 10)]
    public class FilterMostUnitSlot : FilterData
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
            int maxUnit = -1;
            List<Slot> tmp = new List<Slot>();

            foreach (Slot slot in source)
            {
                int unit = GetUnitCount(data, caster, slot);

                if (unit > maxUnit)
                {
                    maxUnit = unit;
                    tmp.Clear();
                    tmp.Add(slot);
                }

                else if (unit == maxUnit)
                    tmp.Add(slot);
            }

            return GameTool.PickXRandom(tmp, dest, 1);
        }

        //public override List<CardData> FilterTargets(Game data, AbilityData ability, Card caster, List<CardData> source, List<CardData> dest)
        //{

        //}

        private int GetUnitCount(Game data, Card caster, Slot slot)
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
                            if (player_type == FilterPlayerType.Both)
                                amount += 1;

                            else if (player_type == FilterPlayerType.Self && unit.player_id == player.player_id)
                                amount += 1;

                            else if (player_type == FilterPlayerType.Opponent && unit.player_id == oplayer.player_id)
                                amount += 1;
                    }
                }
            }

            return amount;
        }
    }
}
