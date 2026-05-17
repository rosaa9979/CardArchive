using System.Collections.Generic;

namespace TcgEngine
{
    /// <summary>
    /// Pluggable source of match/start-of-game overrides.
    /// Implementations: PlayerSetupData (per-player), LevelData (match-level, Adventure),
    /// TutorialData (match-level, Tutorial), TotalAssaultData (match-level, Total Assault).
    /// GameLogic.StartGame queries providers in priority order and applies the first
    /// non-null value; for the additive collection methods it concatenates results.
    /// Returning null / empty means "no override — defer to lower-priority providers
    /// or the global GameplayData defaults".
    /// </summary>
    public interface IGameSetupProvider
    {
        int? GetStartHp(Player player);
        int? GetStartMana(Player player);
        int? GetStartHand(Player player);

        LevelFirst? GetFirstPlayer();
        bool? GetMulligan();

        IEnumerable<CardData> GetExtraClubs(Player player);
    }
}
