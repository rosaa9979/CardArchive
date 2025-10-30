using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TcgEngine
{
    //Pick X number of targets at random from the source array

    [CreateAssetMenu(fileName = "filter", menuName = "TcgEngine/Filter/Random", order = 10)]
    public class FilterRandom : FilterData
    {
        public int amount = 1; //Number of random targets selected
        public bool rest = false;

        public override List<Card> FilterTargets(Game data, AbilityData ability, Card caster, List<Card> source, List<Card> dest)
        {
            if (rest && source.Count - amount > 0)
                return GameTool.PickXRandom(source, dest, source.Count - amount);
            return GameTool.PickXRandom(source, dest, amount);
        }

        public override List<Player> FilterTargets(Game data, AbilityData ability, Card caster, List<Player> source, List<Player> dest)
        {
            if (rest && source.Count - amount > 0)
                return GameTool.PickXRandom(source, dest, source.Count - amount);
            return GameTool.PickXRandom(source, dest, amount);
        }

        public override List<Slot> FilterTargets(Game data, AbilityData ability, Card caster, List<Slot> source, List<Slot> dest)
        {
            if (rest && source.Count - amount > 0)
                return GameTool.PickXRandom(source, dest, source.Count - amount);
            return GameTool.PickXRandom(source, dest, amount);
        }

        public override List<CardData> FilterTargets(Game data, AbilityData ability, Card caster, List<CardData> source, List<CardData> dest)
        {
            if (rest && source.Count - amount > 0)
                return GameTool.PickXRandom(source, dest, source.Count - amount);
            return GameTool.PickXRandom(source, dest, amount);
        }
    }
}
