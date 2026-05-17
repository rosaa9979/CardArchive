namespace TcgEngine
{
    /// <summary>
    /// Top-level category shown as a tab in AdventurePanel. Each value maps
    /// to a different IGameTypeView source: Tutorial → TutorialData,
    /// Adventure → LevelData, Story → (TBD), TotalAssault → TotalAssaultData.
    /// </summary>
    public enum AdventureCategory
    {
        Tutorial = 0,
        Adventure = 10,
        Story = 20,
        TotalAssault = 30,
    }
}
