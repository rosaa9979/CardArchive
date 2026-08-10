// 수정된 ResolveQueue.cs를 단독 컴파일하기 위한 최소 스텁
using System;
using System.Collections.Generic;

namespace UnityEngine
{
    public static class Mathf
    {
        public static float Max(float a, float b) { return a > b ? a : b; }
        public static int Min(int a, int b) { return a < b ? a : b; }
    }
    public static class Debug
    {
        public static void Log(string s) { }
        public static void LogError(string s) { Console.WriteLine("[ERROR] " + s); }
    }
}
namespace UnityEngine.Events { public class UnityEvent { } }

namespace TcgEngine
{
    public enum GameState { Play = 0, GameEnded = 20 }
    public enum SelectorType { None = 0, SelectTarget = 10 }
    public enum AbilityTrigger { None = 0, OnPlay = 10, StartOfTurn = 20, OnDeath = 30, OnDeathOther = 31 }

    public static class GameConfig
    {
        public static class Timing
        {
            public const float ability = 1f;
            public const float secret = 1f;
            public const float attack = 0.3f;
            public const float callback = 0.2f;
        }
    }

    public class Slot
    {
        public int x, y, p;
        public Slot(int x, int y, int p) { this.x = x; this.y = y; this.p = p; }
        public bool IsValid() { return p >= 0; }
    }

    public class Player
    {
        public int player_id;
        public List<Card> cards_board = new List<Card>();
        public List<Card> player_ability = new List<Card>();
    }

    public class AbilityData
    {
        public string id;
        public int max_repeat;
        public Action<Sim, Card, int> effect;        // (sim, caster, current_repeat)
        public Func<Sim, bool> repeat_condition;
        public override string ToString() { return id; }
    }

    public class Card
    {
        public string uid;
        public int player_id;
        public int hp = 1;
        public bool dying;
        public bool invincible;
        public int play_order;
        public AbilityData on_death;
        public AbilityData on_death_other;
        public AbilityData on_kill;      // 이 카드가 남을 죽였을 때
        public Card death_source;        // 나를 죽인 카드 (킬 귀속)

        // ---- 전투용 ----
        public int attack = 1;           // 공격력
        public bool is_front;            // FRONT 무기 = 반격 발생
        public bool exhausted;           // 이번 전투에서 이미 공격함
        public AbilityData on_before_attack, on_before_defend;
        public AbilityData on_after_attack, on_after_defend;
        public AbilityData on_before_attack_other, on_after_attack_other;
        public int GetHP() { return hp; }
        public override string ToString() { return uid; }
    }

    public class Game
    {
        public GameState state = GameState.Play;
        public SelectorType selector = SelectorType.None;
        public List<Player> players = new List<Player>();
    }
}
