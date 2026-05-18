using UnityEngine;

namespace TcgEngine
{
    /// <summary>
    /// Display + launch contract for a single GameType instance shown in
    /// AdventurePanel. Each per-mode data SO (LevelData / TutorialData /
    /// TotalAssaultData / StoryData) implements this so LevelUI can render
    /// and launch entries without per-mode branching.
    /// </summary>
    public interface IGameTypeView
    {
        string GetTitle();
        Sprite GetIcon();
        DeckData GetDisplayDeck();
        string GetId();
        GameType GetGameType();

        //Triggered when the player clicks this entry in AdventurePanel.
        //Each SO decides its own launch flow (immediate start, deck selector first, etc.).
        void Launch();
    }
}
