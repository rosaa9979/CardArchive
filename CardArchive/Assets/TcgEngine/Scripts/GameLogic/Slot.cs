using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

namespace TcgEngine
{
    /// <summary>
    /// Represent a slot in gameplay (data only)
    /// </summary>

    [System.Serializable]
    public struct Slot : INetworkSerializable
    {
        public int x; //From 1 to 5
        public int y; //Not in use, could be used to add more rows or different locations on the board
        public int p; //0 or 1, represent player ID

        public static int x_min = 1; //Dont change this, should start at 1  (0,0,0 represent invalid slot)
        public static int x_max = 7; //Number of slots in a row/zone

        public static int y_min = 1; //Dont change this, should start at 1  (0,0,0 represent invalid slot)
        public static int y_max = 3; //Set this to the number of rows/locations you want to have

        public static bool ignore_p = false; //Set to true if you dont want to use P value

        private static Dictionary<int, List<Slot>> player_slots = new Dictionary<int, List<Slot>>();
        private static Dictionary<int, List<Slot>> player_slots_include_neutral = new Dictionary<int, List<Slot>>();
        private static Dictionary<int, List<Slot>> player_self_slots = new Dictionary<int, List<Slot>>();
        private static List<Slot> all_slots = new List<Slot>();
        private static List<Slot> neutral_slots = new List<Slot>();      

        public Slot(int pid)
        {
            this.x = 0;
            this.y = 0;
            this.p = pid;
        }

        public Slot(int x, int y, int pid)
        {
            this.x = x;
            this.y = y;
            this.p = pid;
        }

        public Slot(SlotXY slot, int pid)
        {
            this.x = slot.x;
            this.y = slot.y;
            this.p = pid;
        }

        public bool IsInRangeX(Slot slot, int range)
        {
            return Mathf.Abs(x - slot.x) <= range;
        }

        public bool IsInRangeY(Slot slot, int range)
        {
            return Mathf.Abs(y - slot.y) <= range;
        }

        public bool IsInRangeP(Slot slot, int range)
        {
            return Mathf.Abs(p - slot.p) <= range;
        }

        //No Diagonal, Diagonal = 2 dist
        public bool IsInDistanceStraight(Slot slot, int dist)
        {
            int r = Mathf.Abs(x - slot.x) + Mathf.Abs(y - slot.y) + Mathf.Abs(p - slot.p);
            return r <= dist;
        }

        //Diagonal = 1 dist
        public bool IsInDistance(Slot slot, int dist)
        {
            int dx = Mathf.Abs(x - slot.x);
            int dy = Mathf.Abs(y - slot.y);
            int dp = Mathf.Abs(p - slot.p);
            return dx <= dist && dy <= dist && dp <= dist;
        }

        public bool IsPlayerSlot()
        {
            return x == 0 && y == 0;
        }

        //Check if the slot is valid one (or if out of board)
        public bool IsValid()
        {
            return x >= x_min && x <= x_max - y_max + y && y >= y_min && y <= y_max && p >= 0;
        }

        public int GetP()
        {
            return ignore_p ? 0 : p;
        }

        //Return slot P-value of player, usually its same as player_id, unless we ignore P value then its 0 for all
        public static int GetP(int pid)
        {
            return ignore_p ? 0 : pid;
        }

        //Get a random slot on player side (exclude Neutral Slot)
        public static Slot GetRandom(int pid, System.Random rand)
        {
            int p = GetP(pid);
            if (y_max > y_min)
            {
                int rand_y = rand.Next(y_min, y_max - 1 + 1);
                int rand_x = rand.Next(x_min, x_max - 3 + rand_y + 1);
                return new Slot(rand_x, rand_y, p);
            }
                
            return new Slot(rand.Next(x_min, x_max + 1), y_min, p);
        }

        //Get a random slot amongts all valid ones
        public static Slot GetRandom(System.Random rand)
        {
            if (y_max > y_min)
            {
                int rand_y = rand.Next(y_min, y_max + 1);
                int rand_x = rand.Next(x_min, x_max - y_max + rand_y + 1);

                if (rand_y == 3)
                    return new Slot(rand_x, rand_y, 2);
                return new Slot(rand_x, rand_y, rand.Next(0, 2));
            }
                
            return new Slot(rand.Next(x_min, x_max + 1), y_min, rand.Next(0, 2));
        }
		
		public static Slot Get(int x, int y, int p)
        {
            List<Slot> slots = GetAll();
            foreach (Slot slot in slots)
            {
                if (slot.x == x && slot.y == y && slot.p == p)
                    return slot;
            }
            return new Slot(x, y, p);
        }

        //Get all slots on player side (exclude Neutral Slot)
        public static List<Slot> GetAll(int pid)
        {
            int p = GetP(pid);

            if (player_slots.ContainsKey(p))
                return player_slots[p]; //Faster access

            List<Slot> list = new List<Slot>();
            for (int y = y_min; y <= y_max - 1; y++)
            {
                for (int x = x_min; x <= x_max - y_max + y; x++)
                {
                    list.Add(new Slot(x, y, p));
                }
            }
            player_slots[p] = list;
            return list;
        }

        //Get all slots on player side (include Neutral Slot)
        public static List<Slot> GetAllIncludeNeutral(int pid)
        {
            int p = GetP(pid);

            if (player_slots_include_neutral.ContainsKey(p))
                return player_slots_include_neutral[p]; //Faster access


            List<Slot> list = new List<Slot>();

            if (player_slots.ContainsKey(p))
                list.AddRange(player_slots[p]);
            else
            {
                for (int y = y_min; y <= y_max - 1; y++)
                {
                    for (int x = x_min; x <= x_max - y_max + y; x++)
                    {
                        list.Add(new Slot(x, y, p));
                    }
                }
                player_slots[p] = list;
            }

            list.AddRange(GetNeutral());
            player_slots_include_neutral[p] = list;
            return list;
        }

        
        //Get all valid slots
        public static List<Slot> GetAll()
        {
            if (all_slots.Count > 0)
                return all_slots; //Faster access

            for (int p = 0; p <= 1; p++)
            {
                for (int y = y_min; y <= y_max - 1; y++)
                {
                    for (int x = x_min; x <= x_max - y_max + y; x++)
                    {
                        all_slots.Add(new Slot(x, y, p));
                    }
                }
            }

            for (int x = x_min; x <= x_max; x++)
            {
                all_slots.Add(new Slot(x, 3, 2));
            }
            return all_slots;
        }

        //Get all neutral slots
        public static List<Slot> GetNeutral()
        {
            if (neutral_slots.Count > 0)
                return neutral_slots; //Faster access

            for (int x = x_min; x <= x_max; x++)
            {
                neutral_slots.Add(new Slot(x, 3, 2));
            }

            return neutral_slots;
        }

        public static List<Slot> GetPlayerSelf(int pid)
        {
            int p = GetP(pid);

            if (player_self_slots.ContainsKey(p))
                return player_self_slots[p]; //Faster access


            List<Slot> list = new List<Slot>();

            list.Add(new Slot(1, 1, p));
            list.Add(new Slot(2, 1, p));
            list.Add(new Slot(3, 1, p));
            list.Add(new Slot(4, 1, p));
            list.Add(new Slot(5, 1, p));

            list.Add(new Slot(1, 2, p));
            list.Add(new Slot(6, 2, p));

            player_self_slots[p] = list;
            return list;
        }

        public List<Slot> GetNeighborSlot()
        {
            List<Slot> slots = GetAll();
            List<Slot> neighbor_slots = new List<Slot>();

            List<(int, int)> directions = new List<(int, int)>
            {
                (-1, 0),
                (0, 1),
                (1, 1),
                (1, 0),
                (0, -1),
                (-1, -1)
            };

            foreach (var dir in directions)
            {
                int new_x = x + dir.Item1;
                int new_y = y + dir.Item2;
                int new_p = p;

                if (y != new_y)
                {
                    if (y == 3)
                    {
                        if (new_y > y)
                        {
                            new_y = y_max - (new_y - y_max);
                            new_x = new_x - 1;
                            new_p = 1;
                        }
                        else if (new_y < y)
                            new_p = 0;
                    }
                    else if (y < 3)
                    {
                        if (new_y == 3)
                            new_p = 2;
                    }
                }

                Slot new_slot = new Slot(new_x, new_y, new_p);

                foreach (Slot slot in slots)
                {
                    if (slot == new_slot)
                        neighbor_slots.Add(new_slot);
                }
            }

            return neighbor_slots;
        }

        public Dictionary<int, List<Slot>> GetRangeSlot(int range)
        {
            HashSet<Slot> visited = new HashSet<Slot>();
            Dictionary<int, List<Slot>> range_slot = new Dictionary<int, List<Slot>>();
            Queue<(Slot slot, int distance)> queue = new Queue<(Slot slot, int distance)>();

            // 시작 슬롯과 거리 0을 큐에 삽입
            queue.Enqueue((new Slot(x, y, p), 0));
            visited.Add(new Slot(x, y, p));
            range_slot.TryAdd(0, new List<Slot>());
            range_slot[0].Add(new Slot(x, y, p));

            while (queue.Count > 0)
            {
                // 현재 슬롯과 거리 정보를 큐에서 꺼냄
                var (currentSlot, currentDistance) = queue.Dequeue();

                // 현재 거리가 범위를 초과하면 탐색 중단
                if (currentDistance > range - 1)
                    continue;

                // 현재 슬롯의 모든 이웃 슬롯 탐색
                foreach (var neighbor in currentSlot.GetNeighborSlot())
                {
                    // 이웃 슬롯이 방문하지 않았다면
                    if (!visited.Contains(neighbor))
                    {
                        visited.Add(neighbor);

                        if (!range_slot.ContainsKey(currentDistance + 1))
                            range_slot.TryAdd(currentDistance + 1, new List<Slot>());

                        range_slot[currentDistance + 1].Add(neighbor);
                        queue.Enqueue((neighbor, currentDistance + 1));
                    }
                }
            }

            return range_slot;
        }  

        public static bool operator ==(Slot slot1, Slot slot2)
        {
            return slot1.x == slot2.x && slot1.y == slot2.y && slot1.p == slot2.p;
        }

        public static bool operator !=(Slot slot1, Slot slot2)
        {
            return slot1.x != slot2.x || slot1.y != slot2.y || slot1.p != slot2.p;
        }


        public override bool Equals(object o)
        {
            return base.Equals(o);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref x);
            serializer.SerializeValue(ref y);
            serializer.SerializeValue(ref p);
        }

        public static Slot None
        {
            get { return new Slot(0, 0, 0); }
        }
    }

    [System.Serializable]
    public struct SlotXY
    {
        public int x;
        public int y;
    }
}
