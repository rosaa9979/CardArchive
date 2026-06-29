namespace TcgEngine
{
    // ------------------------------------------------------------------
    // Data-driven dispatch enums for the table-export refactor.
    //
    // Each member name MUST equal the matching subclass name with its
    // base-class prefix stripped, e.g.
    //   EffectSetStat        -> EffectType.SetStat
    //   ConditionSlotDist    -> ConditionType.SlotDist
    //   FilterRandomCount    -> FilterType.RandomCount
    //   SortTrait            -> SortType.Trait
    //   RepeatCountType      -> RepeatType.CountType
    // The exporter relies on this convention to map an asset's runtime
    // class to its `type` value via reflection.
    // ------------------------------------------------------------------

    public enum EffectType
    {
        None = 0,

        SetStat = 10, AddStat = 11, ResetStat = 12, SetStatCustom = 13,
        AddStatRoll = 14, AddStatCount = 15, AddStatTotalCount = 16,
        CopyStat = 17, CycleStat = 18,

        Damage = 20, DamageRatio = 21, DamageCount = 22, Heal = 23,

        Draw = 30, Discard = 31, Shuffle = 32, Create = 33, CreateCard = 34,
        SendPile = 35, MovePileTopToBottom = 36, ClearTemp = 37,

        Play = 40, PlayCard = 41, UseCard = 42, Transform = 43,

        SummonSlot = 50, Move = 51, Knockback = 52, Attack = 53, AttackRedirect = 54,

        AddAbility = 60, RemoveAbility = 61, AddTrait = 62, RemoveTrait = 63, AddClub = 64,

        ClearStatus = 70, Attach = 71, ChangeWeapon = 72, ChangeOwner = 73,
        Destroy = 74, DestroyEquip = 75, Exhaust = 76,

        Mana = 80, Roll = 81, BossGauge = 82, StoreCount = 83, SetClubCardUI = 84,

        Conditional = 90, ConditionalCaster = 91,
    }

    public enum ConditionType
    {
        None = 0,

        Stat = 10, StatCustom = 11, PlayerStat = 12, ClubStatMatch = 13, BossGauge = 14,

        CardType = 20, CardData = 21, Status = 22, Damaged = 23, Exhaust = 24,
        Equipped = 25, Deckbuilding = 26,

        Owner = 30, OwnerAI = 31, Self = 32, Target = 33, Triggered = 34,

        Count = 40, CardPile = 41, PilePosition = 42, CanPlace = 43,

        SlotDist = 50, SlotRange = 51, SlotNeighbor = 52, SlotPid = 53, SlotLocate = 54,
        SlotAttachmentEmpty = 55, SlotUnitEmpty = 56, WideAreaRange = 57,

        Turn = 60, Rolled = 61, Once = 62, Possibility = 63,

        LastTypeExist = 70, LastTypeRange = 71,

        CompositeOr = 80,
    }

    // Unified comparison operator for the Condition table: merges
    // ConditionOperatorInt {Equal..Less} and ConditionOperatorBool
    // {IsTrue,IsFalse} (IsTrue->Equal, IsFalse->NotEqual).
    public enum ConditionOperator
    {
        Equal = 0, NotEqual = 1, GreaterEqual = 2, LessEqual = 3, Greater = 4, Less = 5,
    }

    // Backing [Flags] enums for the Condition table's shared `area_mask` column.
    // The table stores a plain !Int (the external tool has no flags-enum support);
    // these exist so the exporter/runtime read & write the bits type-safely instead
    // of magic numbers. Bit layout is identical (1/2/4) so one int column serves both.
    //   SlotSideMask  <- ConditionSlotPid    (player / opponent / neutral)
    //   SlotZoneMask  <- ConditionSlotLocate (inside / outside / neutral)
    [System.Flags]
    public enum SlotSideMask
    {
        None = 0,
        Player = 1,
        Opponent = 2,
        Neutral = 4,
    }

    [System.Flags]
    public enum SlotZoneMask
    {
        None = 0,
        Inside = 1,
        Outside = 2,
        Neutral = 4,
    }

    public enum FilterType
    {
        None = 0,
        First = 1, Random = 2, RandomCount = 3,
        HighestStat = 4, LowestStat = 5,
        MostUnitSlot = 6, MostWoundedSlot = 7,
    }

    public enum SortType
    {
        None = 0,
        Trait = 1,
    }

    public enum RepeatType
    {
        None = 0,
        CountType = 1,
        StaticValue = 2,
    }
}
