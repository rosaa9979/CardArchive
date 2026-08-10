using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TcgEngine.Tools
{
    // Plays the scene -> writes .xlsx tables under <project>/Tools/DataExport/.
    // Most ScriptableObject types map 1:1 to a file (CardData, AbilityData, ...).
    // The data-driven families are different:
    //   - Effect.xlsx collapses each subclass into one row with columns
    //     compressed BY DATA TYPE (generic str/ref/int slot pools — see the
    //     "Data-driven tables" region).
    //   - Condition.xlsx bundles 5 sheets (Condition/WideAreaRange/Filter/
    //     Sort/Repeat) with hand-mapped SEMANTIC columns sharing one column
    //     vocabulary (value/range/flag/scope/stat/pile/card_kind/ref_*) —
    //     see docs/condition-family-datatable-schema.md.
    //
    // Output follows the external table-tool spec (see `Skill (2).xlsx`):
    // a left margin of empty columns, then `!`-prefixed directives. The parser
    // locks onto the directive cell, so the margin is just layout.
    //
    // Each .xlsx contains 4 sheets:
    //   1. Reference — external table dependencies. Row 1 blank; then one row
    //                  per dependency: (col A empty, B `!Reference`, C `<File>.xlsx`).
    //   2. Enum      — enum definitions. Content starts at col D. Each block:
    //                    D `!Enum`       E <EnumName> F `!Int` G `!GenerateCsEnum` H <backing>
    //                    D `!Enumerator` E <ValueName> F <EnumName> G <intValue>
    //                  Blocks separated by blank rows. One owner table per enum.
    //   3. _컬럼 설명 — per-column dictionary skeleton (No/Name/Type auto-filled;
    //                  Detail/설명/비고 blank for designers). Content at col B.
    //   4. {Table}   — actual data. Content starts at col D:
    //                    row 1 : D `!Table` E <TableName> F `!AutoKey;!GenerateCsEnum`
    //                    row 2 : field names (id first), terminated by `!EndField`
    //                    row 3 : type row (`!Id` for id, `_ID_<Table>` for table
    //                            refs, bare `<EnumName>` for enums, `!String`/`!Int`/
    //                            `!Float`/`!Bool` for primitives; external assets ->
    //                            `!String`; lists keep a trailing `[]`)
    //                    row 4+: data
    //                    last  : `!EndTable` in the table's first content column (D)
    [DisallowMultipleComponent]
    public class DataTableExporter : MonoBehaviour
    {
        const string EndTable = "!EndTable";
        const string EndField = "!EndField";
        const string TableMarker = "!Table";
        const string TableOptions = "!AutoKey;!GenerateCsEnum";
        const string EnumMarker = "!Enum";
        const string EnumeratorMarker = "!Enumerator";

        // Empty left-margin columns before the table content (cols A-C), matching
        // the spec's layout. Reference sheet uses a 1-col margin instead (col B).
        const int DataMargin = 3;

        [Header("Output")]
        [Tooltip("Folder, relative to the project root, where the .xlsx files will be written.")]
        public string output_folder = "Tools/DataExport";

        [Header("Behavior")]
        [Tooltip("Run the export automatically when the scene starts playing.")]
        public bool run_on_start = true;
        [Tooltip("Quit/exit Play mode after the export finishes.")]
        public bool exit_when_done = true;

        // ------------------------------------------------------------------
        void Start()
        {
            if (run_on_start)
                Run();
        }

        [ContextMenu("Run Export Now")]
        public void Run()
        {
            string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", output_folder));
            Directory.CreateDirectory(outDir);

            int total = 0;
            float t0 = Time.realtimeSinceStartup;

            try
            {
                total += Export<CardData>(outDir, "CardData",   BuildCardRow,    CardColumns);
                total += Export<AcademyData>(outDir, "Academy", BuildSimpleRow,  null);
                total += Export<ClubData>(outDir, "Club",       BuildSimpleRow,  null);
                total += Export<AbilityData>(outDir, "Ability", BuildAbilityRow, AbilityColumns);
                total += ExportWorkbook(outDir, "Effect", BuildEffectTable());
                total += Export<WeaponData>(outDir, "Weapon",   BuildWeaponRow,  WeaponColumns);
                total += Export<TraitData>(outDir, "Trait",     BuildSimpleRow,  null);
                total += Export<StatusData>(outDir, "Status",   BuildSimpleRow,  null);
                total += ExportWorkbook(outDir, "Condition",
                    BuildConditionTable(),
                    BuildFilterTable(),
                    BuildSortTable(),
                    BuildRepeatTable(),
                    BuildWideAreaRangeTable());
                total += Export<PackData>(outDir, "Pack",       BuildSimpleRow,  null);
                total += Export<RarityData>(outDir, "Rarity",   BuildSimpleRow,  null);
                total += Export<VariantData>(outDir, "Variant", BuildSimpleRow,  null);
                total += Export<CardbackData>(outDir, "Cardback", BuildSimpleRow, null);
                total += Export<AvatarData>(outDir, "Avatar",   BuildSimpleRow,  null);
                total += Export<DeckData>(outDir, "Deck",       BuildSimpleRow,  null);
                total += Export<LevelData>(outDir, "Level",     BuildSimpleRow,  null);
                total += Export<RewardData>(outDir, "Reward",   BuildSimpleRow,  null);
                total += Export<TeamData>(outDir, "Team",       BuildSimpleRow,  null);
                total += Export<TotalAssaultData>(outDir, "TotalAssault", BuildSimpleRow, null);
                total += Export<TutorialData>(outDir, "Tutorial", BuildSimpleRow, null);
                total += Export<DescValueData>(outDir, "DescValue", BuildSimpleRow, null);
                total += Export<AssetData>(outDir, "Asset",     BuildSimpleRow,  null);
                total += Export<GameplayData>(outDir, "Gameplay", BuildSimpleRow, null);
                total += Export<NetworkData>(outDir, "Network", BuildSimpleRow,  null);
            }
            catch (Exception e)
            {
                Debug.LogError("[DataTableExporter] FAILED: " + e);
                return;
            }

            float dt = Time.realtimeSinceStartup - t0;
            Debug.Log(string.Format(
                "<b>[DataTableExporter] Wrote {0} rows across 24 files in {1:F2}s.</b>\nOutput: {2}",
                total, dt, outDir));

            if (exit_when_done)
            {
#if UNITY_EDITOR
                EditorApplication.delayCall += () => EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }
        }

        // ------------------------------------------------------------------
        // Core export pipeline
        // ------------------------------------------------------------------
        delegate IDictionary<string, object> RowBuilder<T>(T asset) where T : ScriptableObject;

        int Export<T>(string outDir, string fileBase,
                      RowBuilder<T> rowBuilder, IList<string> fixedCols)
            where T : ScriptableObject
        {
            var assets = Resources.LoadAll<T>("");
            if (assets == null || assets.Length == 0)
            {
                Debug.LogWarning(string.Format("[DataTableExporter] {0}: no assets found.", fileBase));
                return 0;
            }

            var sorted = assets
                .Where(a => a != null)
                .OrderBy(a => RefName(a), StringComparer.Ordinal)
                .ToArray();

            var dataRows = new List<IDictionary<string, object>>(sorted.Length);
            foreach (var a in sorted)
            {
                IDictionary<string, object> row = rowBuilder(a);
                if (row == null) continue;
                if (!row.ContainsKey("id") || row["id"] == null ||
                    (row["id"] is string s && string.IsNullOrEmpty(s)))
                {
                    row["id"] = a.name;
                }
                dataRows.Add(row);
            }

            // Column ordering: id, then fixedCols (excluding id), then alphabetical extras.
            var cols = new List<string> { "id" };
            var seen = new HashSet<string> { "id" };
            if (fixedCols != null)
            {
                foreach (var c in fixedCols)
                    if (c != "id" && seen.Add(c)) cols.Add(c);
            }
            var extras = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var row in dataRows)
                foreach (var k in row.Keys)
                    if (!seen.Contains(k)) extras.Add(k);
            foreach (var c in extras) { seen.Add(c); cols.Add(c); }

            // Build a {column -> FieldInfo} map by walking every loaded asset's type.
            var fieldByCol = new Dictionary<string, FieldInfo>();
            foreach (var a in sorted)
            {
                for (var t = a.GetType(); t != null && t != typeof(UnityEngine.Object); t = t.BaseType)
                {
                    var fields = t.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                    foreach (var f in fields)
                    {
                        if (f.IsStatic || f.IsNotSerialized) continue;
                        if (!fieldByCol.ContainsKey(f.Name)) fieldByCol[f.Name] = f;
                    }
                }
            }

            var typeRow = cols.Select(c => DescribeColumnType(c, fileBase, fieldByCol)).ToList();

            var refSheet  = BuildReferencesSheet(fileBase);
            var enumSheet = BuildEnumsSheet(fileBase);
            var descSheet = BuildColumnDescSheet(fileBase, cols, typeRow);
            var dataSheet = BuildDataSheet(fileBase, cols, typeRow, dataRows);

            string outPath = Path.Combine(outDir, fileBase + ".xlsx");
            SimpleXlsx.Write(outPath, new[] { refSheet, enumSheet, descSheet, dataSheet });
            Debug.Log(string.Format("[DataTableExporter] {0,-10} {1,4} rows -> {2}",
                                    fileBase, dataRows.Count, outPath));
            return dataRows.Count;
        }

        // ------------------------------------------------------------------
        // Sheet 1: References
        // ------------------------------------------------------------------
        static readonly Dictionary<string, string[]> REFERENCES = new Dictionary<string, string[]>
        {
            { "CardData",  new[] { "Club.xlsx", "Weapon.xlsx", "Trait.xlsx", "Ability.xlsx", "Pack.xlsx" } },
            { "Academy",   new string[0] },
            { "Club",      new[] { "Academy.xlsx" } },
            { "Ability",   new[] { "Condition.xlsx", "Effect.xlsx", "Status.xlsx", "Ability.xlsx" } },
            { "Weapon",    new string[0] },
            { "Trait",     new string[0] },
            { "Status",    new string[0] },
            { "Pack",      new[] { "Rarity.xlsx", "Variant.xlsx" } },
            { "Rarity",    new string[0] },
            { "Variant",   new string[0] },
            { "Cardback",  new string[0] },
            { "Avatar",    new string[0] },
            { "Deck",      new[] { "CardData.xlsx" } },
            { "Level",     new[] { "Deck.xlsx", "Pack.xlsx", "CardData.xlsx" } },
            { "Reward",    new[] { "Pack.xlsx", "CardData.xlsx", "Deck.xlsx" } },
            { "Team",      new string[0] },
            { "TotalAssault", new[] { "Deck.xlsx", "Pack.xlsx", "CardData.xlsx", "Level.xlsx" } },
            { "Tutorial",  new[] { "Deck.xlsx", "Pack.xlsx", "CardData.xlsx", "Level.xlsx" } },
            { "DescValue", new string[0] },
            { "Asset",     new string[0] },
            { "Gameplay",  new[] { "CardData.xlsx", "Deck.xlsx" } },
            { "Network",   new string[0] },
        };

        const string ReferenceMarker = "!Reference";

        SimpleXlsx.Sheet BuildReferencesSheet(string table)
        {
            var rows = new List<IList<object>>();
            // Row 1: blank spacer (column A is reserved/empty across the sheet).
            rows.Add(new List<object> { null, null, null });

            string[] refs;
            if (REFERENCES.TryGetValue(table, out refs))
            {
                foreach (var r in refs)
                    // col A empty, col B `!Reference`, col C `<File>.xlsx`
                    rows.Add(new List<object> { null, ReferenceMarker, r });
            }

            return new SimpleXlsx.Sheet { Name = "Reference", Rows = rows, FreezeRows = 0 };
        }

        // ------------------------------------------------------------------
        // Sheet 2: Enums (deduped — each enum has exactly one owner table)
        // ------------------------------------------------------------------
        static readonly Dictionary<string, Type[]> ENUM_OWNER = new Dictionary<string, Type[]>
        {
            { "CardData",  new[] { typeof(CardType) } },
            { "Ability",   new[] { typeof(AbilityTrigger), typeof(AbilityTarget) } },
            // Effect.xlsx owns the enums SHARED by the Condition family too
            // (ConditionStatType/ConditionPlayerType/ConditionLastType +
            // virtual PilePosType) — Condition.xlsx already references
            // Effect.xlsx, and owning them here avoids a reference cycle.
            // Dropped from the table (merged into shared columns):
            //   EffectStatType->ConditionStatType(+Range), EffectPlayerType->
            //   ConditionPlayerType, EffectActionType->ConditionLastType
            //   (+Self/AbilityTriggerer), DeckInsert->PilePosType,
            //   BossGaugeType->ConditionStatType(Boss*).
            { "Effect",    new[] {
                typeof(EffectType),
                typeof(EffectTotalCountType),
                typeof(EffectDamageType), typeof(EffectValueType),
                typeof(PileType), typeof(EffectStatusType),
                typeof(ConditionStatType), typeof(ConditionPlayerType),
                typeof(ConditionLastType),
            } },
            { "Weapon",    new[] { typeof(WeaponType) } },
            { "Status",    new[] { typeof(StatusType) } },
            // Condition.xlsx bundles Condition/Filter/Sort/Repeat/WideAreaRange
            // sheets. Enums shared with Effect live in Effect.xlsx (see above);
            // FilterPlayerType/PosMode are gone from the table (filters share
            // ConditionPlayerType, PosMode is exported as virtual PilePosType).
            { "Condition", new[] {
                typeof(ConditionType), typeof(FilterType), typeof(SortType), typeof(RepeatType),
                typeof(ConditionOperator), typeof(ConditionTargetType),
            } },
            { "Pack",      new[] { typeof(PackType) } },
            { "Level",     new[] { typeof(LevelFirst) } },
            { "Gameplay",  new[] { typeof(TcgEngine.AI.AIType) } },
            { "Network",   new[] { typeof(SoloType), typeof(AuthenticatorType) } },
        };

        // v2 (docs/condition-family-datatable-schema.md): the TABLE-level enum
        // shape can differ from the runtime C# enum until the matching code
        // TODOs land. Three escape hatches:
        //   ENUM_OVERRIDES      — full member replacement, keyed by enum NAME
        //   ENUM_MEMBER_EXCLUDE — members hidden from the table
        //   ENUM_VIRTUAL        — enums with no runtime Type yet (per table;
        //                         members must come from ENUM_OVERRIDES)
        struct EnumMember
        {
            public string name; public int value;
            public EnumMember(string n, int v) { name = n; value = v; }
        }

        static readonly Dictionary<string, EnumMember[]> ENUM_OVERRIDES = new Dictionary<string, EnumMember[]>
        {
            // TODO-2: ConditionStatType extended with Range (EffectStatType) and
            // the boss gauges (BossGaugeType) — ONE shared `stat` enum for the
            // Condition/Filter/Effect `stat` columns.
            { "ConditionStatType", new[] {
                new EnumMember("None", 0), new EnumMember("Attack", 10),
                new EnumMember("HP", 20), new EnumMember("Mana", 30),
                new EnumMember("Range", 35),
                new EnumMember("BossSkill", 40), new EnumMember("BossAtg", 41),
                new EnumMember("BossGroggy", 42),
            } },
            // EffectActionType merged in: Self/AbilityTriggerer join the last-*
            // members so `last_type` serves Attack/AttackRedirect/MoveUnit too.
            { "ConditionLastType", new[] {
                new EnumMember("None", 0), new EnumMember("LastAttacked", 1),
                new EnumMember("LastTargeted", 2), new EnumMember("LastSummoned", 3),
                new EnumMember("LastDestroyed", 4), new EnumMember("LastPlayed", 5),
                new EnumMember("LastSelected", 6),
                new EnumMember("Self", 10), new EnumMember("AbilityTriggerer", 11),
            } },
            // TODO-3: ConditionPilePosition.PosMode promoted to top-level
            // PilePosType (also replaces DeckInsert: Top/Bottom).
            { "PilePosType", new[] {
                new EnumMember("Top", 0), new EnumMember("Bottom", 1), new EnumMember("Index", 2),
            } },
            // Single-select versions of the SlotSideMask/SlotZoneMask bits
            // (values match the mask bits for a trivial loader mapping).
            { "SlotSide", new[] {
                new EnumMember("None", 0), new EnumMember("Player", 1),
                new EnumMember("Opponent", 2), new EnumMember("Neutral", 4),
            } },
            { "SlotZone", new[] {
                new EnumMember("None", 0), new EnumMember("Inside", 1),
                new EnumMember("Outside", 2), new EnumMember("Neutral", 4),
            } },
        };

        static readonly Dictionary<string, string[]> ENUM_MEMBER_EXCLUDE = new Dictionary<string, string[]>
        {
            // TODO-4: WideAreaRange has its own sheet — not a Condition dispatch value.
            { "ConditionType", new[] { "WideAreaRange" } },
        };

        static readonly Dictionary<string, string[]> ENUM_VIRTUAL = new Dictionary<string, string[]>
        {
            { "Condition", new[] { "SlotSide", "SlotZone" } },
            { "Effect", new[] { "PilePosType" } },
        };

        SimpleXlsx.Sheet BuildEnumsSheet(string table)
        {
            var rows = new List<IList<object>>();
            rows.Add(new List<object>());  // row 1: blank spacer

            bool first = true;
            Action<string, string, IEnumerable<EnumMember>> emit = (name, backing, members) =>
            {
                if (!first) rows.Add(new List<object>());  // blank separator between blocks
                first = false;

                // D `!Enum` E <Name> F `!Int` G `!GenerateCsEnum` H <backing>
                rows.Add(Indent(DataMargin, EnumMarker, name, "!Int", "!GenerateCsEnum", backing));

                // D `!Enumerator` E <ValueName> F <EnumName> G <intValue>
                foreach (var m in members)
                    rows.Add(Indent(DataMargin, EnumeratorMarker, m.name, name, m.value));
            };

            Type[] enumTypes;
            if (ENUM_OWNER.TryGetValue(table, out enumTypes))
            {
                foreach (var t in enumTypes)
                {
                    EnumMember[] over;
                    if (ENUM_OVERRIDES.TryGetValue(t.Name, out over))
                    {
                        emit(t.Name, "int", over);
                        continue;
                    }

                    string[] excl;
                    ENUM_MEMBER_EXCLUDE.TryGetValue(t.Name, out excl);
                    var members = new List<EnumMember>();
                    foreach (var v in Enum.GetValues(t))
                    {
                        string n = v.ToString();
                        if (excl != null && Array.IndexOf(excl, n) >= 0) continue;
                        members.Add(new EnumMember(n, Convert.ToInt32(v)));
                    }
                    emit(t.Name, EnumBackingToken(Enum.GetUnderlyingType(t)), members);
                }
            }

            string[] virtuals;
            if (ENUM_VIRTUAL.TryGetValue(table, out virtuals))
            {
                foreach (var vn in virtuals)
                {
                    EnumMember[] over;
                    if (ENUM_OVERRIDES.TryGetValue(vn, out over))
                        emit(vn, "int", over);
                }
            }

            return new SimpleXlsx.Sheet { Name = "Enum", Rows = rows, FreezeRows = 0 };
        }

        static string EnumBackingToken(Type ut)
        {
            if (ut == typeof(byte)) return "byte";
            if (ut == typeof(sbyte)) return "sbyte";
            if (ut == typeof(short)) return "short";
            if (ut == typeof(ushort)) return "ushort";
            if (ut == typeof(uint)) return "uint";
            if (ut == typeof(long)) return "long";
            if (ut == typeof(ulong)) return "ulong";
            return "int";
        }

        // ------------------------------------------------------------------
        // Sheet 3: _컬럼 설명 — per-column dictionary (skeleton only).
        //   No / Name / Type auto-filled; Detail / 설명 / 비고 left blank for
        //   designers to fill in. Content starts at col B (1-col margin).
        // ------------------------------------------------------------------
        SimpleXlsx.Sheet BuildColumnDescSheet(string fileBase, List<string> cols, List<string> typeRow)
        {
            const int margin = 1;
            var rows = new List<IList<object>>(cols.Count + 6);

            rows.Add(new List<object>());                       // row 1: blank spacer
            rows.Add(Indent(margin, "TableName", fileBase));    // B: label, C: table name
            rows.Add(Indent(margin, "설명", null));             // table description (blank)
            // header row, terminated by !EndField — colored to stand out
            rows.Add(HeaderRow(margin, "No", "Name", "Type", "Detail", "설명", "비고", EndField));

            for (int i = 0; i < cols.Count; i++)
                rows.Add(Indent(margin, i + 1, cols[i], typeRow[i], null, null, null));

            rows.Add(Indent(margin, EndTable));                 // bottom-left end marker
            return new SimpleXlsx.Sheet { Name = "_컬럼 설명", Rows = rows, FreezeRows = 0 };
        }

        // ------------------------------------------------------------------
        // Sheet 4: Data  (content starts at col D — see DataMargin)
        //   row 1 : `!Table` <TableName> <options>
        //   row 2 : field names (id first) + `!EndField`
        //   row 3 : type row
        //   row 4+: data
        //   last  : `!EndTable` in the first content column
        // ------------------------------------------------------------------
        SimpleXlsx.Sheet BuildDataSheet(string fileBase, List<string> cols, List<string> typeRow,
                                        List<IDictionary<string, object>> dataRows)
        {
            var rows = new List<IList<object>>(dataRows.Count + 6);

            // Row 1: !Table | <TableName> | options
            rows.Add(Indent(DataMargin, TableMarker, fileBase, TableOptions));

            // Row 2: field names, terminated by !EndField (top-right marker)
            var header = new List<object>(cols.Count + 1);
            foreach (var c in cols) header.Add(c);
            header.Add(EndField);
            rows.Add(Indent(DataMargin, header));

            // Row 3: type row
            rows.Add(Indent(DataMargin, typeRow));

            // Data rows
            foreach (var dr in dataRows)
            {
                var row = new List<object>(cols.Count);
                foreach (var c in cols)
                {
                    object v;
                    dr.TryGetValue(c, out v);
                    if (v is bool bb) v = bb ? 1 : 0;   // bool -> 0/1 int
                    row.Add(v);
                }
                rows.Add(Indent(DataMargin, row));
            }

            // Bottom-left end-of-rows marker, in the first content column
            rows.Add(Indent(DataMargin, EndTable));

            return new SimpleXlsx.Sheet { Name = fileBase, Rows = rows, FreezeRows = 3 };
        }

        // Prepend `margin` empty cells, then the given content cells.
        static List<object> Indent(int margin, params object[] cells)
        {
            var row = new List<object>(margin + (cells != null ? cells.Length : 0));
            for (int i = 0; i < margin; i++) row.Add(null);
            if (cells != null) row.AddRange(cells);
            return row;
        }

        // Overload for a pre-built cell list (e.g. a data row).
        static List<object> Indent(int margin, IEnumerable<object> cells)
        {
            var row = new List<object>(margin + 8);
            for (int i = 0; i < margin; i++) row.Add(null);
            if (cells != null) row.AddRange(cells);
            return row;
        }

        // Same as Indent, but wraps every content cell so it renders with the
        // header style (bold + fill) — margin cells stay unstyled (layout only).
        static List<object> HeaderRow(int margin, params object[] cells)
            => HeaderRow(margin, (IEnumerable<object>)cells);

        static List<object> HeaderRow(int margin, IEnumerable<object> cells)
        {
            var row = new List<object>(margin + 8);
            for (int i = 0; i < margin; i++) row.Add(null);
            if (cells != null)
                foreach (var c in cells)
                    row.Add(new SimpleXlsx.StyledCell { Value = c, Style = SimpleXlsx.HeaderStyle });
            return row;
        }

        // ------------------------------------------------------------------
        // Type description for the type row
        // ------------------------------------------------------------------
        // Map from ScriptableObject runtime type -> table name used in `_ID_*`.
        static readonly Dictionary<Type, string> TABLE_NAME_BY_TYPE = new Dictionary<Type, string>
        {
            { typeof(CardData),     "CardData" },
            { typeof(AbilityData),  "Ability" },
            { typeof(EffectData),   "Effect" },
            { typeof(ConditionWideAreaRange), "WideAreaRange" },
            { typeof(ConditionData), "Condition" },
            { typeof(FilterData),   "Filter" },
            { typeof(SortData),     "Sort" },
            { typeof(RepeatConditionData), "Repeat" },
            { typeof(ClubData),     "Club" },
            { typeof(AcademyData),  "Academy" },
            { typeof(TraitData),    "Trait" },
            { typeof(StatusData),   "Status" },
            { typeof(WeaponData),   "Weapon" },
            { typeof(PackData),     "Pack" },
            { typeof(VariantData),  "Variant" },
            { typeof(CardbackData), "Cardback" },
            { typeof(AvatarData),   "Avatar" },
            { typeof(DeckData),     "Deck" },
            { typeof(LevelData),    "Level" },
            { typeof(RarityData),   "Rarity" },
            { typeof(RewardData),   "Reward" },
            { typeof(TeamData),     "Team" },
            { typeof(TotalAssaultData), "TotalAssault" },
            { typeof(TutorialData), "Tutorial" },
            { typeof(DescValueData), "DescValue" },
            { typeof(AssetData),    "Asset" },
            { typeof(GameplayData), "Gameplay" },
            { typeof(NetworkData),  "Network" },
        };

        static string TableNameOf(Type t)
        {
            for (var bt = t; bt != null; bt = bt.BaseType)
            {
                string name;
                if (TABLE_NAME_BY_TYPE.TryGetValue(bt, out name)) return name;
            }
            return t.Name;
        }

        string DescribeColumnType(string column, string sheetName, Dictionary<string, FieldInfo> fieldByCol)
        {
            if (column == "id") return "!Id";

            FieldInfo f;
            if (!fieldByCol.TryGetValue(column, out f)) return "!String";
            return DescribeFieldType(f.FieldType);
        }

        string DescribeFieldType(Type t)
        {
            bool isList = false;
            if (t.IsArray)
            {
                t = t.GetElementType();
                isList = true;
            }
            else if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>))
            {
                t = t.GetGenericArguments()[0];
                isList = true;
            }

            string s;
            if (t.IsEnum) s = t.Name;                       // bare enum name (defined in Enum sheet)
            else if (t == typeof(string)) s = "!String";
            else if (t == typeof(int) || t == typeof(long) || t == typeof(short) || t == typeof(byte)
                     || t == typeof(uint) || t == typeof(ulong) || t == typeof(ushort) || t == typeof(sbyte))
                s = "!Int";
            else if (t == typeof(float) || t == typeof(double)) s = "!Int";  // float -> scaled int (10000 = 1.0); spec has no float
            else if (t == typeof(bool)) s = "!Int";   // bool exported as 0/1 int
            else if (typeof(ScriptableObject).IsAssignableFrom(t)) s = "_ID_" + TableNameOf(t);
            else if (typeof(UnityEngine.Object).IsAssignableFrom(t)) s = "!String";  // external asset -> path string
            else s = t.Name;  // structs (TraitStat, PackRarity, WeightedCard, ...)

            return isList ? s + "[]" : s;
        }

        // ------------------------------------------------------------------
        // Generic / fallback row builder — every public serialized field
        // ------------------------------------------------------------------
        IDictionary<string, object> BuildSimpleRow<T>(T asset) where T : ScriptableObject
        {
            var row = new Dictionary<string, object>();
            DumpAllFields(asset, row);
            return row;
        }

        // ==================================================================
        // Data-driven tables (Effect / Condition family) — one row per asset,
        // hand-mapped SEMANTIC columns, shared vocabulary across every sheet.
        // ==================================================================
        class DDTable
        {
            public string sheet;
            public List<string> cols = new List<string>();
            public List<string> toks = new List<string>();
            public List<IDictionary<string, object>> rows = new List<IDictionary<string, object>>();
            public List<IList<object>> doc = new List<IList<object>>();   // per-type slot meaning (row 0 = header)
            public SortedSet<string> refDeps = new SortedSet<string>(StringComparer.Ordinal);
        }

        // ==================================================================
        // Effect semantic table (v2) — same philosophy as the Condition family:
        //   - ONE value per cell; single-use bools -> `flag` (0/1);
        //     self/opponent bools + EffectPlayerType -> `scope`
        //     (ConditionPlayerType: All->Both, Player->Self).
        //   - shared enums: EffectStatType & BossGaugeType -> `stat`
        //     (ConditionStatType +Range +Boss*), EffectActionType ->
        //     `last_type` (ConditionLastType +Self/AbilityTriggerer),
        //     DeckInsert -> `pile_pos` (PilePosType).
        //   - list cells only where the meaning is "run/use ALL":
        //     effects_true/effects_false (sequential), weighted_cards
        //     (weighted random pick, `card=weight;...`).
        //   - id = plain asset name (matches Ability.effects FK via RefName).
        // ==================================================================
        DDTable BuildEffectTable()
        {
            var t = NewDDTable("Effect",
                "id", "!Id", "type", "EffectType",
                "value", "!Int", "flag", "!Int",
                "scope", "ConditionPlayerType", "stat", "ConditionStatType",
                "total", "EffectTotalCountType",
                "pile", "PileType", "pile_pos", "PilePosType",
                "last_type", "ConditionLastType",
                "damage_kind", "EffectDamageType", "value_kind", "EffectValueType",
                "status_kind", "EffectStatusType",
                "card_kind", "CardType",
                "ref_card", "_ID_CardData", "ref_club", "_ID_Club",
                "ref_trait", "_ID_Trait", "bonus_trait", "_ID_Trait",
                "ref_status", "_ID_Status", "ref_ability", "_ID_Ability",
                "ref_weapon", "_ID_Weapon", "ref_condition", "_ID_Condition",
                "effects_true", "_ID_Effect", "effects_false", "_ID_Effect",
                "weighted_cards", "!String",
                "x", "!Int", "y", "!Int", "dx", "!Int", "dy", "!Int");
            foreach (var d in new[] { "CardData.xlsx", "Club.xlsx", "Trait.xlsx", "Status.xlsx",
                                      "Ability.xlsx", "Weapon.xlsx", "Condition.xlsx" }) t.refDeps.Add(d);

            // [type별 슬롯 의미] — col -> original field name
            DocRow(t, "SetStat", "stat", "type");
            DocRow(t, "AddStat", "stat", "type", "flag", "use_stored_value");
            DocRow(t, "ResetStat");
            DocRow(t, "SetStatCustom", "ref_trait", "trait");
            DocRow(t, "AddStatRoll", "stat", "type");
            DocRow(t, "AddStatCount", "stat", "type", "pile", "pile", "scope", "player",
                "card_kind", "has_type", "ref_club", "has_club", "ref_trait", "has_trait");
            DocRow(t, "AddStatTotalCount", "stat", "stat_type", "total", "total_type");
            DocRow(t, "CopyStat", "ref_trait", "trait", "ref_club", "require_club(선택)", "flag", "only_if_missing");
            DocRow(t, "CycleStat", "ref_trait", "trait");
            DocRow(t, "Damage", "damage_kind", "damage_type", "value_kind", "value_type", "bonus_trait", "bonus_damage");
            DocRow(t, "DamageRatio", "bonus_trait", "bonus_damage", "value", "ratio(만분율)");
            DocRow(t, "DamageCount", "damage_kind", "damage_type", "bonus_trait", "bonus_damage",
                "pile", "pile", "scope", "player", "card_kind", "has_type", "ref_club", "has_club", "ref_trait", "has_trait");
            DocRow(t, "Heal", "value_kind", "heal_type", "bonus_trait", "bonus_heal");
            DocRow(t, "Draw");
            DocRow(t, "Discard");
            DocRow(t, "Shuffle");
            DocRow(t, "Create", "pile", "create_pile", "scope", "create_opponent");
            DocRow(t, "CreateCard", "weighted_cards", "create_card(card=weight;...)", "pile", "create_pile",
                "flag", "is_same_possibility", "scope", "create_opponent");
            DocRow(t, "SendPile", "pile", "pile", "pile_pos", "insert(Top/Bottom)");
            DocRow(t, "MovePileTopToBottom", "pile", "pile");
            DocRow(t, "ClearTemp");
            DocRow(t, "Play");
            DocRow(t, "PlayCard", "ref_card", "play_card");
            DocRow(t, "UseCard", "ref_card", "use", "scope", "use_opponent");
            DocRow(t, "Transform", "ref_card", "transform_to");
            DocRow(t, "SummonSlot", "x", "position.x", "y", "position.y", "dx", "direction.x", "dy", "direction.y");
            DocRow(t, "MoveUnit", "last_type", "target_type");
            DocRow(t, "Knockback");
            DocRow(t, "Attack", "last_type", "attacker_type");
            DocRow(t, "AttackRedirect", "last_type", "attacker_type");
            DocRow(t, "AddAbility", "ref_ability", "gain_ability");
            DocRow(t, "RemoveAbility", "ref_ability", "remove_ability");
            DocRow(t, "AddTrait", "ref_trait", "trait");
            DocRow(t, "RemoveTrait", "ref_trait", "trait");
            DocRow(t, "AddClub", "ref_club", "club");
            DocRow(t, "ClearStatus", "ref_status", "status(빈 칸이면 status_kind로 일괄 제거)", "status_kind", "status_type");
            DocRow(t, "AttachCard", "ref_card", "attach");
            DocRow(t, "ChangeWeapon", "ref_weapon", "weapon");
            DocRow(t, "ChangeOwner", "scope", "owner_opponent");
            DocRow(t, "Destroy");
            DocRow(t, "DestroyEquip");
            DocRow(t, "Exhaust", "flag", "exhausted");
            DocRow(t, "Mana");
            DocRow(t, "Roll", "value", "dice");
            DocRow(t, "BossGauge", "stat", "gauge(Boss*)", "flag", "set_to_value", "value", "delta 또는 set_value(flag에 따름)");
            DocRow(t, "StoreCount", "pile", "pile", "scope", "player",
                "card_kind", "has_type", "ref_club", "has_club", "ref_trait", "has_trait");
            DocRow(t, "SetClubCardUI");
            DocRow(t, "Conditional", "ref_condition", "condition", "effects_true", "effects_true", "effects_false", "effects_false");
            DocRow(t, "ConditionalCaster", "ref_condition", "condition", "effects_true", "effects_true", "effects_false", "effects_false");

            var idUsed = new HashSet<string>(StringComparer.Ordinal);
            foreach (var a in LoadSorted<EffectData>())
            {
                var row = new Dictionary<string, object> { ["type"] = MemberName(a.GetType(), "Effect") };
                FillEffectRow(a, row);
                row["id"] = MakeUniqueId(Sanitize(a.name), idUsed);
                t.rows.Add(row);
            }
            return t;
        }

        // EffectPlayerType {All, Player, Opponent} -> ConditionPlayerType names.
        static string ScopeOf(EffectPlayerType p)
            => p == EffectPlayerType.All ? "Both" : (p == EffectPlayerType.Player ? "Self" : "Opponent");

        // "apply to opponent?" bools -> ConditionPlayerType names.
        static string OppScope(bool opponent) => opponent ? "Opponent" : "Self";

        void FillEffectRow(EffectData a, IDictionary<string, object> row)
        {
            switch (a)
            {
                case EffectSetStat e: row["stat"] = e.type.ToString(); break;
                case EffectAddStatCount e:
                    row["stat"] = e.type.ToString(); row["pile"] = e.pile.ToString(); row["scope"] = ScopeOf(e.player);
                    row["card_kind"] = CardKind(e.has_type); row["ref_club"] = RefName(e.has_club); row["ref_trait"] = RefName(e.has_trait); break;
                case EffectAddStatRoll e: row["stat"] = e.type.ToString(); break;
                case EffectAddStatTotalCount e: row["stat"] = e.stat_type.ToString(); row["total"] = e.total_type.ToString(); break;
                case EffectAddStat e: row["stat"] = e.type.ToString(); row["flag"] = e.use_stored_value ? 1 : 0; break;
                case EffectSetStatCustom e: row["ref_trait"] = RefName(e.trait); break;
                case EffectCopyStat e: row["ref_trait"] = RefName(e.trait); row["ref_club"] = RefName(e.require_club); row["flag"] = e.only_if_missing ? 1 : 0; break;
                case EffectCycleStat e: row["ref_trait"] = RefName(e.trait); break;
                case EffectDamageCount e:
                    row["damage_kind"] = e.damage_type.ToString(); row["bonus_trait"] = RefName(e.bonus_damage);
                    row["pile"] = e.pile.ToString(); row["scope"] = ScopeOf(e.player);
                    row["card_kind"] = CardKind(e.has_type); row["ref_club"] = RefName(e.has_club); row["ref_trait"] = RefName(e.has_trait); break;
                case EffectDamageRatio e: row["bonus_trait"] = RefName(e.bonus_damage); row["value"] = (int)Math.Round(e.ratio * 10000.0); break;
                case EffectDamage e: row["damage_kind"] = e.damage_type.ToString(); row["value_kind"] = e.value_type.ToString(); row["bonus_trait"] = RefName(e.bonus_damage); break;
                case EffectHeal e: row["value_kind"] = e.heal_type.ToString(); row["bonus_trait"] = RefName(e.bonus_heal); break;
                case EffectCreateCard e:
                    row["weighted_cards"] = JoinWeighted(e.create_card); row["pile"] = e.create_pile.ToString();
                    row["flag"] = e.is_same_possibility ? 1 : 0; row["scope"] = OppScope(e.create_opponent); break;
                case EffectCreate e: row["pile"] = e.create_pile.ToString(); row["scope"] = OppScope(e.create_opponent); break;
                case EffectSendPile e: row["pile"] = e.pile.ToString(); row["pile_pos"] = e.insert == DeckInsert.Top ? "Top" : "Bottom"; break;
                case EffectMovePileTopToBottom e: row["pile"] = e.pile.ToString(); break;
                case EffectPlayCard e: row["ref_card"] = RefName(e.play_card); break;
                case EffectUseCard e: row["ref_card"] = RefName(e.use); row["scope"] = OppScope(e.use_opponent); break;
                case EffectTransform e: row["ref_card"] = RefName(e.transform_to); break;
                case EffectSummonSlot e:
                    row["x"] = e.position.x; row["y"] = e.position.y;
                    row["dx"] = e.direction.x; row["dy"] = e.direction.y; break;
                case EffectMoveUnit e: row["last_type"] = e.target_type.ToString(); break;
                case EffectAttackRedirect e: row["last_type"] = e.attacker_type.ToString(); break;
                case EffectAttack e: row["last_type"] = e.attacker_type.ToString(); break;
                case EffectAddAbility e: row["ref_ability"] = RefName(e.gain_ability); break;
                case EffectRemoveAbility e: row["ref_ability"] = RefName(e.remove_ability); break;
                case EffectAddTrait e: row["ref_trait"] = RefName(e.trait); break;
                case EffectRemoveTrait e: row["ref_trait"] = RefName(e.trait); break;
                case EffectAddClub e: row["ref_club"] = RefName(e.club); break;
                case EffectClearStatus e: row["ref_status"] = RefName(e.status); row["status_kind"] = e.status_type.ToString(); break;
                case EffectAttachCard e: row["ref_card"] = RefName(e.attach); break;
                case EffectChangeWeapon e: row["ref_weapon"] = RefName(e.weapon); break;
                case EffectChangeOwner e: row["scope"] = OppScope(e.owner_opponent); break;
                case EffectExhaust e: row["flag"] = e.exhausted ? 1 : 0; break;
                case EffectRoll e: row["value"] = e.dice; break;
                case EffectBossGauge e:
                    // gauge -> extended ConditionStatType member; only the int
                    // the flag selects is meaningful at runtime.
                    row["stat"] = "Boss" + e.gauge; row["flag"] = e.set_to_value ? 1 : 0;
                    row["value"] = e.set_to_value ? e.set_value : e.delta; break;
                case EffectStoreCount e:
                    row["pile"] = e.pile.ToString(); row["scope"] = ScopeOf(e.player);
                    row["card_kind"] = CardKind(e.has_type); row["ref_club"] = RefName(e.has_club); row["ref_trait"] = RefName(e.has_trait); break;
                case EffectConditionalCaster e:
                    row["ref_condition"] = RefName(e.condition);
                    row["effects_true"] = JoinRefs(e.effects_true); row["effects_false"] = JoinRefs(e.effects_false); break;
                case EffectConditional e:
                    row["ref_condition"] = RefName(e.condition);
                    row["effects_true"] = JoinRefs(e.effects_true); row["effects_false"] = JoinRefs(e.effects_false); break;
                case EffectResetStat _: case EffectDraw _: case EffectDiscard _: case EffectShuffle _:
                case EffectClearTemp _: case EffectPlay _: case EffectKnockback _: case EffectDestroy _:
                case EffectDestroyEquip _: case EffectMana _: case EffectSetClubCardUI _:
                    break;  // no serialized fields
                default: Debug.LogWarning("[DataTableExporter] Unmapped effect: " + a.GetType().Name); break;
            }
        }

        // ==================================================================
        // Condition-family semantic tables (v2)
        //   Condition / WideAreaRange / Filter / Sort / Repeat.
        //
        // Hand-mapped, MEANING-named columns sharing ONE column vocabulary
        // across all 5 sheets (see docs/condition-family-datatable-schema.md).
        //   - id   : plain sanitized asset name on EVERY sheet, so Ability's
        //            FK cells (RefName) resolve without a prefix scheme.
        //   - oper : ConditionOperatorBool/Int merged -> ConditionOperator.
        //   - value: shared int (threshold/index/amount/probability x10000).
        //   - range: shared neighbor radius (distance/range).
        //   - flag : bool 0/1 (compare_to_max/diagonals/rest/descending) or
        //            bitmask 1|2|4 (SlotPid side / SlotLocate zone).
        //   - scope: ConditionPlayerType (Self/Opponent/Both) on every sheet.
        //   - stat : ConditionStatType extended with BossSkill/BossAtg/BossGroggy
        //            (table-level; replaces the old `gauge` column).
        //   - refs : ONE value per cell (card_kind/ref_card/ref_club/ref_trait)
        //            when the match is positive — OR over several values is
        //            structural (synthetic leaf rows composed by CompositeOr,
        //            see "OR decomposition" below). Exception: a NEGATED match
        //            (oper=NotEqual) keeps its ';' list in-cell, because
        //            NOT(any) = AND of negations ("none of these").
        //            `sub_conditions` (AND for Count, OR for CompositeOr) and
        //            WideAreaRange dx/dy are list cells by design.
        // ==================================================================

        // Build a table with an explicit (col, token) schema and a doc header
        // ("type" row-label + every column except `id` and `type`).
        static DDTable NewDDTable(string sheet, params string[] colTok)
        {
            var t = new DDTable { sheet = sheet };
            for (int i = 0; i + 1 < colTok.Length; i += 2) { t.cols.Add(colTok[i]); t.toks.Add(colTok[i + 1]); }
            var head = new List<object> { "type" };
            for (int i = 2; i < t.cols.Count; i++) head.Add(t.cols[i]);  // skip id(0) + type(1)
            t.doc.Add(head);
            return t;
        }

        // One [type별 슬롯 의미] row: maps the columns this type uses to the
        // original field name. Unused columns render as "-".
        static void DocRow(DDTable t, string type, params string[] colDesc)
        {
            var map = new Dictionary<string, string>();
            for (int i = 0; i + 1 < colDesc.Length; i += 2) map[colDesc[i]] = colDesc[i + 1];
            var r = new List<object> { type };
            for (int i = 2; i < t.cols.Count; i++) { map.TryGetValue(t.cols[i], out var d); r.Add(string.IsNullOrEmpty(d) ? "-" : d); }
            t.doc.Add(r);
        }

        static string MemberName(Type t, string prefix)
            => t.Name.StartsWith(prefix) ? t.Name.Substring(prefix.Length) : t.Name;

        // ConditionOperatorInt member names already equal ConditionOperator's.
        static string Oper(ConditionOperatorInt o) => o.ToString();
        static string Oper(ConditionOperatorBool o) => o == ConditionOperatorBool.IsTrue ? "Equal" : "NotEqual";

        // Single CardType field -> `card_kind` cell. None means
        // "no type filter" in code, encoded as an empty cell.
        static string CardKind(CardType kind)
            => kind == CardType.None ? string.Empty : kind.ToString();

        // Self-ref id for CompositeOr.any — must match the Condition id scheme
        // (sanitized asset name; type now lives in the `condition` column, not the id).
        // Dedup suffixes are not reflected (asset names are unique, so collisions don't occur).
        static string ConditionRefId(ConditionData c)
            => c == null ? string.Empty : Sanitize(c.name);

        static string JoinConditionRefs(System.Collections.IEnumerable items)
        {
            if (items == null) return string.Empty;
            var parts = new List<string>();
            foreach (var it in items) if (it is ConditionData c) parts.Add(ConditionRefId(c));
            return string.Join(";", parts);
        }

        static string MakeUniqueId(string baseId, HashSet<string> used)
        {
            string id = baseId; int k = 2;
            while (!used.Add(id)) id = baseId + "_" + (k++);
            return id;
        }

        static T[] LoadSorted<T>(Func<Type, bool> exclude = null) where T : ScriptableObject
            => Resources.LoadAll<T>("")
                .Where(a => a != null && (exclude == null || !exclude(a.GetType())))
                .OrderBy(a => a.name, StringComparer.Ordinal).ToArray();

        // ------------------------------------------------------------------
        // Condition (36 types; WideAreaRange excluded -> own table)
        // ------------------------------------------------------------------
        DDTable BuildConditionTable()
        {
            // `type`(ConditionType) is the dispatch enum column — the type is
            // not encoded in `id` (id = asset name, matching Ability's FK).
            //
            // v2.1: every ref/enum-match column holds a SINGLE value. OR over
            // multiple values is expressed structurally: the exporter splits a
            // multi-value asset into synthetic single-value leaf rows composed
            // by a CompositeOr row that keeps the original asset id (so the
            // Ability FK still resolves). Count's per-card filter groups move
            // into `sub_conditions` (AND-combined). See EmitCardType/
            // EmitCardData/EmitCount below.
            var t = NewDDTable("Condition",
                "id", "!Id", "type", "ConditionType", "oper", "ConditionOperator",
                "value", "!Int", "range", "!Int", "flag", "!Int",
                "scope", "ConditionPlayerType", "stat", "ConditionStatType",
                "pile", "PileType", "pile_pos", "PilePosType",
                "last_type", "ConditionLastType", "target_kind", "ConditionTargetType",
                "side", "SlotSide", "zone", "SlotZone",
                "card_kind", "CardType",
                "ref_card", "_ID_CardData", "ref_club", "_ID_Club",
                "ref_trait", "_ID_Trait", "ref_status", "_ID_Status",
                "sub_conditions", "_ID_Condition");
            foreach (var d in new[] { "Effect.xlsx", "CardData.xlsx", "Club.xlsx", "Trait.xlsx", "Status.xlsx" }) t.refDeps.Add(d);

            // [type별 슬롯 의미] — col -> original field name
            DocRow(t, "Stat", "stat", "type", "oper", "oper", "value", "value");
            DocRow(t, "StatCustom", "ref_trait", "trait", "oper", "oper", "value", "value");
            DocRow(t, "PlayerStat", "stat", "type", "oper", "oper", "value", "value");
            DocRow(t, "ClubStatMatch", "ref_club", "club", "ref_trait", "trait", "oper", "oper");
            DocRow(t, "BossGauge", "stat", "gauge(Boss*)", "oper", "oper", "value", "value", "flag", "compare_to_max");
            DocRow(t, "CardType", "card_kind", "has_type(다중 OR은 CompositeOr 분해, NotEqual은 ;목록)", "ref_club", "has_club(동일)", "ref_trait", "has_trait(동일)", "oper", "oper");
            DocRow(t, "CardData", "ref_card", "card_types(다중 OR은 CompositeOr 분해, NotEqual은 ;목록)", "oper", "oper");
            DocRow(t, "Status", "ref_status", "has_status", "value", "value(최소스택)", "oper", "oper");
            DocRow(t, "Damaged", "oper", "oper");
            DocRow(t, "Exhaust", "oper", "oper");
            DocRow(t, "Equipped", "oper", "oper");
            DocRow(t, "Deckbuilding", "oper", "oper");
            DocRow(t, "Owner", "oper", "oper");
            DocRow(t, "OwnerAI", "oper", "oper(AI 전용)");
            DocRow(t, "Self", "oper", "oper");
            DocRow(t, "Target", "target_kind", "type", "oper", "oper");
            DocRow(t, "Triggered", "oper", "is_oper");
            DocRow(t, "Count", "scope", "target", "pile", "pile", "oper", "oper", "value", "value",
                "sub_conditions", "카드 필터 조건 목록(모두 충족한 카드만 셈; has_type/club/trait/card에서 분해)");
            DocRow(t, "CardPile", "pile", "type", "oper", "oper");
            DocRow(t, "PilePosition", "pile", "pile", "pile_pos", "mode", "value", "index", "oper", "oper");
            DocRow(t, "CanPlace", "last_type", "last_type", "ref_card", "place_card", "scope", "card_owner", "oper", "oper");
            DocRow(t, "SlotDist", "range", "distance", "flag", "diagonals");
            DocRow(t, "SlotRange", "oper", "oper");
            DocRow(t, "SlotNeighbor", "range", "range");
            DocRow(t, "SlotPid", "side", "player/opponent/neutral(단일; 다중은 CompositeOr로 분해)");
            DocRow(t, "SlotLocate", "zone", "Inside/Outside/Neutral(단일; 다중은 CompositeOr로 분해)");
            DocRow(t, "SlotAttachmentEmpty", "oper", "oper");
            DocRow(t, "SlotUnitEmpty", "oper", "oper");
            DocRow(t, "Turn", "oper", "oper");
            DocRow(t, "Rolled", "oper", "oper", "value", "value");
            DocRow(t, "Once");
            DocRow(t, "Possibility", "value", "possibility(만분율)");
            DocRow(t, "None");
            DocRow(t, "LastTypeExist", "last_type", "type", "oper", "oper");
            DocRow(t, "LastTypeRange", "last_type", "type", "range", "range", "oper", "oper");
            DocRow(t, "CompositeOr", "sub_conditions", "any");

            var idUsed = new HashSet<string>(StringComparer.Ordinal);
            var synth = new Dictionary<string, string>(StringComparer.Ordinal);  // content signature -> synthetic row id
            var assets = LoadSorted<ConditionData>(tp => tp == typeof(ConditionWideAreaRange));

            // Reserve every real asset id FIRST, so a synthetic leaf/or row can
            // never steal an id that an Ability FK cell points to.
            var realId = new Dictionary<ConditionData, string>();
            foreach (var a in assets)
                realId[a] = MakeUniqueId(Sanitize(a.name), idUsed);

            foreach (var a in assets)
            {
                switch (a)
                {
                    case ConditionCardType c: EmitCardType(t, c, realId[a], idUsed, synth); break;
                    case ConditionCardData c: EmitCardData(t, c, realId[a], idUsed, synth); break;
                    case ConditionCount c: EmitCount(t, c, realId[a], idUsed, synth); break;
                    case ConditionSlotPid c: EmitSlotPid(t, c, realId[a], idUsed, synth); break;
                    case ConditionSlotLocate c: EmitSlotLocate(t, c, realId[a], idUsed, synth); break;
                    default:
                    {
                        var row = new Dictionary<string, object> { ["type"] = MemberName(a.GetType(), "Condition") };
                        FillConditionRow(a, row);
                        row["id"] = realId[a];
                        t.rows.Add(row);
                        break;
                    }
                }
            }
            return t;
        }

        // ------------------------------------------------------------------
        // OR decomposition (v2.1)
        //
        // oper=Equal (positive) + multi-value: OR must be structural — the
        // asset is split into single-value LEAF rows composed by a CompositeOr
        // row; the composite keeps the original asset id so Ability FKs
        // resolve unchanged. Leaf / or-group rows are synthetic (no backing
        // asset), deduped by content signature, with readable content-derived
        // ids (is_kind_X / is_club_X / or_kind_X_Y / ...).
        //
        // oper=NotEqual (negated) + multi-value: NOT(a∨b) = (¬a)∧(¬b) — an
        // AND, so the ';' list STAYS in the cell (user decision: AND semantics
        // may remain list-encoded). A NotEqual row negates its whole match
        // expression: "none of the listed values match".
        // ------------------------------------------------------------------

        // Adds a synthetic row once per content signature; returns its id.
        string AddSynthRow(DDTable t, HashSet<string> idUsed, Dictionary<string, string> synth,
                           string sig, string baseId, Func<Dictionary<string, object>> build)
        {
            string id;
            if (synth.TryGetValue(sig, out id)) return id;
            id = MakeUniqueId(baseId, idUsed);
            var row = build();
            row["id"] = id;
            t.rows.Add(row);
            synth[sig] = id;
            return id;
        }

        // Single-value CardType leaf: AND of whichever of kind/club/trait is set.
        string AddCardTypeLeaf(DDTable t, HashSet<string> idUsed, Dictionary<string, string> synth,
                               CardType kind, ClubData club, TraitData trait)
        {
            string k = CardKind(kind);
            string cl = RefName(club);
            string tr = RefName(trait);

            var parts = new List<string>();
            if (k.Length > 0) parts.Add("kind_" + k);
            if (cl.Length > 0) parts.Add("club_" + Sanitize(cl));
            if (tr.Length > 0) parts.Add("trait_" + Sanitize(tr));
            string baseId = "is_" + (parts.Count > 0 ? string.Join("_", parts) : "any");

            string sig = "CardType|" + k + "|" + cl + "|" + tr;
            return AddSynthRow(t, idUsed, synth, sig, baseId, () => new Dictionary<string, object>
            {
                ["type"] = "CardType", ["oper"] = "Equal",
                ["card_kind"] = k, ["ref_club"] = cl, ["ref_trait"] = tr,
            });
        }

        // Single-value CardData leaf: target is this exact card.
        string AddCardDataLeaf(DDTable t, HashSet<string> idUsed, Dictionary<string, string> synth, CardData card)
        {
            string cn = RefName(card);
            string sig = "CardData|" + cn;
            return AddSynthRow(t, idUsed, synth, sig, "is_card_" + Sanitize(cn), () => new Dictionary<string, object>
            {
                ["type"] = "CardData", ["oper"] = "Equal", ["ref_card"] = cn,
            });
        }

        // Wraps leaf ids into ONE referencable condition: a single leaf is
        // returned as-is, several become a (content-deduped) CompositeOr row.
        string AddOrGroup(DDTable t, HashSet<string> idUsed, Dictionary<string, string> synth,
                          string baseId, List<string> leafIds)
        {
            leafIds = leafIds.Distinct().ToList();
            if (leafIds.Count == 1) return leafIds[0];
            string sig = "Or|" + string.Join(";", leafIds);
            return AddSynthRow(t, idUsed, synth, sig, baseId, () => new Dictionary<string, object>
            {
                ["type"] = "CompositeOr", ["sub_conditions"] = string.Join(";", leafIds),
            });
        }

        // CardType: (type∈T)∧(club∈C)∧(trait∈R) with each group OR-combined.
        // Positive multi-value -> DNF: CompositeOr over the cartesian product
        // of leaves. Negated (IsFalse) -> one row, lists kept in-cell.
        void EmitCardType(DDTable t, ConditionCardType c, string id,
                          HashSet<string> idUsed, Dictionary<string, string> synth)
        {
            var kinds = (c.has_type ?? new List<CardType>()).Where(k => k != CardType.None).Distinct().ToList();
            var clubs = (c.has_club ?? new List<ClubData>()).Where(x => x != null).ToList();
            var traits = (c.has_trait ?? new List<TraitData>()).Where(x => x != null).ToList();

            int combos = Math.Max(1, kinds.Count) * Math.Max(1, clubs.Count) * Math.Max(1, traits.Count);
            if (combos <= 1 || c.oper == ConditionOperatorBool.IsFalse)
            {
                // Single-value, or negated match (NOT(any) = AND -> lists stay).
                t.rows.Add(new Dictionary<string, object>
                {
                    ["id"] = id, ["type"] = "CardType", ["oper"] = Oper(c.oper),
                    ["card_kind"] = string.Join(";", kinds),
                    ["ref_club"] = JoinRefs(clubs),
                    ["ref_trait"] = JoinRefs(traits),
                });
                return;
            }

            var leafIds = new List<string>();
            foreach (var k in kinds.DefaultIfEmpty(CardType.None))
                foreach (var cl in clubs.DefaultIfEmpty())
                    foreach (var tr in traits.DefaultIfEmpty())
                        leafIds.Add(AddCardTypeLeaf(t, idUsed, synth, k, cl, tr));

            t.rows.Add(new Dictionary<string, object>
            {
                ["id"] = id, ["type"] = "CompositeOr",
                ["sub_conditions"] = string.Join(";", leafIds.Distinct()),
            });
        }

        // CardData: card ∈ list. Positive multi-value -> CompositeOr of
        // CardData leaves. Negated (IsFalse) -> one row, list kept in-cell.
        void EmitCardData(DDTable t, ConditionCardData c, string id,
                          HashSet<string> idUsed, Dictionary<string, string> synth)
        {
            var cards = (c.card_types ?? new List<CardData>()).Where(x => x != null).ToList();
            if (cards.Count <= 1 || c.oper == ConditionOperatorBool.IsFalse)
            {
                // Single-value, or negated match (NOT(any) = AND -> list stays).
                t.rows.Add(new Dictionary<string, object>
                {
                    ["id"] = id, ["type"] = "CardData", ["oper"] = Oper(c.oper),
                    ["ref_card"] = JoinRefs(cards),
                });
                return;
            }

            var leafIds = cards.Select(x => AddCardDataLeaf(t, idUsed, synth, x)).Distinct().ToList();
            t.rows.Add(new Dictionary<string, object>
            {
                ["id"] = id, ["type"] = "CompositeOr",
                ["sub_conditions"] = string.Join(";", leafIds),
            });
        }

        // Single-value enum leaf (SlotPid `side` / SlotLocate `zone`).
        string AddEnumLeaf(DDTable t, HashSet<string> idUsed, Dictionary<string, string> synth,
                           string type, string col, string member)
        {
            string sig = type + "|" + col + "|" + member;
            return AddSynthRow(t, idUsed, synth, sig, "is_" + col + "_" + member,
                () => new Dictionary<string, object> { ["type"] = type, [col] = member });
        }

        // SlotPid / SlotLocate: the three checkbox bools are OR-combined ->
        // one enum member each; multi-checked assets become a CompositeOr of
        // single-member leaves (is_side_Player / is_zone_Inside / ...).
        void EmitSlotPid(DDTable t, ConditionSlotPid c, string id,
                         HashSet<string> idUsed, Dictionary<string, string> synth)
        {
            var members = new List<string>();
            if (c.player) members.Add(SlotSideMask.Player.ToString());
            if (c.opponent) members.Add(SlotSideMask.Opponent.ToString());
            if (c.neutral) members.Add(SlotSideMask.Neutral.ToString());
            EmitEnumOr(t, id, "SlotPid", "side", members, idUsed, synth);
        }

        void EmitSlotLocate(DDTable t, ConditionSlotLocate c, string id,
                            HashSet<string> idUsed, Dictionary<string, string> synth)
        {
            var members = new List<string>();
            if (c.Inside) members.Add(SlotZoneMask.Inside.ToString());
            if (c.Outside) members.Add(SlotZoneMask.Outside.ToString());
            if (c.Neutral) members.Add(SlotZoneMask.Neutral.ToString());
            EmitEnumOr(t, id, "SlotLocate", "zone", members, idUsed, synth);
        }

        void EmitEnumOr(DDTable t, string id, string type, string col, List<string> members,
                        HashSet<string> idUsed, Dictionary<string, string> synth)
        {
            if (members.Count <= 1)
            {
                // Zero members = always false at runtime; kept as an empty cell.
                t.rows.Add(new Dictionary<string, object>
                {
                    ["id"] = id, ["type"] = type,
                    [col] = members.Count > 0 ? members[0] : string.Empty,
                });
                return;
            }

            var leafIds = members.Select(m => AddEnumLeaf(t, idUsed, synth, type, col, m)).ToList();
            t.rows.Add(new Dictionary<string, object>
            {
                ["id"] = id, ["type"] = "CompositeOr",
                ["sub_conditions"] = string.Join(";", leafIds),
            });
        }

        // Count: the per-card filter groups (has_type/club/trait/card) become
        // sub_conditions entries — AND-combined per card, each group itself a
        // single leaf or a CompositeOr of leaves. scope/pile/oper/value stay
        // on the Count row.
        void EmitCount(DDTable t, ConditionCount c, string id,
                       HashSet<string> idUsed, Dictionary<string, string> synth)
        {
            var subs = new List<string>();

            var kinds = (c.has_type ?? new List<CardType>()).Where(k => k != CardType.None).Distinct().ToList();
            if (kinds.Count > 0)
                subs.Add(AddOrGroup(t, idUsed, synth, id + "_kinds",
                    kinds.Select(k => AddCardTypeLeaf(t, idUsed, synth, k, null, null)).ToList()));

            var clubs = (c.has_club ?? new List<ClubData>()).Where(x => x != null).ToList();
            if (clubs.Count > 0)
                subs.Add(AddOrGroup(t, idUsed, synth, id + "_clubs",
                    clubs.Select(x => AddCardTypeLeaf(t, idUsed, synth, CardType.None, x, null)).ToList()));

            var traits = (c.has_trait ?? new List<TraitData>()).Where(x => x != null).ToList();
            if (traits.Count > 0)
                subs.Add(AddOrGroup(t, idUsed, synth, id + "_traits",
                    traits.Select(x => AddCardTypeLeaf(t, idUsed, synth, CardType.None, null, x)).ToList()));

            var cards = (c.has_card ?? new List<CardData>()).Where(x => x != null).ToList();
            if (cards.Count > 0)
                subs.Add(AddOrGroup(t, idUsed, synth, id + "_cards",
                    cards.Select(x => AddCardDataLeaf(t, idUsed, synth, x)).ToList()));

            t.rows.Add(new Dictionary<string, object>
            {
                ["id"] = id, ["type"] = "Count",
                ["scope"] = c.target.ToString(), ["pile"] = c.pile.ToString(),
                ["oper"] = Oper(c.oper), ["value"] = c.value,
                ["sub_conditions"] = string.Join(";", subs),
            });
        }

        void FillConditionRow(ConditionData a, IDictionary<string, object> row)
        {
            switch (a)
            {
                case ConditionBossGauge c:
                    // gauge -> extended ConditionStatType member (BossSkill/BossAtg/BossGroggy)
                    row["stat"] = "Boss" + c.gauge; row["oper"] = Oper(c.oper); row["value"] = c.value; row["flag"] = c.compare_to_max ? 1 : 0; break;
                // ConditionCardType / ConditionCardData / ConditionCount are
                // handled by EmitCardType/EmitCardData/EmitCount (OR-decomposed).
                case ConditionCanPlace c:
                    row["last_type"] = c.last_type.ToString(); row["ref_card"] = RefName(c.place_card); row["scope"] = c.card_owner.ToString(); row["oper"] = Oper(c.oper); break;
                case ConditionCardPile c:
                    row["pile"] = c.type.ToString(); row["oper"] = Oper(c.oper); break;
                case ConditionClubStatMatch c:
                    row["ref_club"] = RefName(c.club); row["ref_trait"] = RefName(c.trait); row["oper"] = Oper(c.oper); break;
                case ConditionCompositeOr c:
                    row["sub_conditions"] = JoinConditionRefs(c.any); break;
                case ConditionDamaged c: row["oper"] = Oper(c.oper); break;
                case ConditionDeckbuilding c: row["oper"] = Oper(c.oper); break;
                case ConditionEquipped c: row["oper"] = Oper(c.oper); break;
                case ConditionExhaust c: row["oper"] = Oper(c.oper); break;
                case ConditionLastTypeExist c: row["last_type"] = c.type.ToString(); row["oper"] = Oper(c.oper); break;
                case ConditionLastTypeRange c: row["last_type"] = c.type.ToString(); row["range"] = c.range; row["oper"] = Oper(c.oper); break;
                case ConditionOwnerAI c: row["oper"] = Oper(c.oper); break;   // AI-only check; same data as Owner
                case ConditionOwner c: row["oper"] = Oper(c.oper); break;
                case ConditionPilePosition c: row["pile"] = c.pile.ToString(); row["pile_pos"] = c.mode.ToString(); row["value"] = c.index; row["oper"] = Oper(c.oper); break;
                case ConditionPlayerStat c: row["stat"] = c.type.ToString(); row["oper"] = Oper(c.oper); row["value"] = c.value; break;
                case ConditionPossibility c: row["value"] = (int)Math.Round(c.possibility * 10000.0); break;
                case ConditionRolled c: row["oper"] = Oper(c.oper); row["value"] = c.value; break;
                case ConditionSelf c: row["oper"] = Oper(c.oper); break;
                // ConditionSlotPid / ConditionSlotLocate are handled by
                // EmitSlotPid/EmitSlotLocate (single `side`/`zone` + OR-decomposed).
                case ConditionSlotAttachmentEmpty c: row["oper"] = Oper(c.oper); break;
                case ConditionSlotDist c: row["range"] = c.distance; row["flag"] = c.diagonals ? 1 : 0; break;
                case ConditionSlotNeighbor c: row["range"] = c.range; break;
                case ConditionSlotRange c: row["oper"] = Oper(c.oper); break;
                case ConditionSlotUnitEmpty c: row["oper"] = Oper(c.oper); break;
                case ConditionStat c: row["stat"] = c.type.ToString(); row["oper"] = Oper(c.oper); row["value"] = c.value; break;
                case ConditionStatCustom c: row["ref_trait"] = RefName(c.trait); row["oper"] = Oper(c.oper); row["value"] = c.value; break;
                case ConditionStatus c: row["ref_status"] = StatusId(c.has_status); row["value"] = c.value; row["oper"] = Oper(c.oper); break;
                case ConditionTarget c: row["target_kind"] = c.type.ToString(); row["oper"] = Oper(c.oper); break;
                case ConditionTriggered c: row["oper"] = Oper(c.is_oper); break;
                case ConditionTurn c: row["oper"] = Oper(c.oper); break;
                case ConditionNone: break;
                case ConditionOnce: break;
                default: Debug.LogWarning("[DataTableExporter] Unmapped condition: " + a.GetType().Name); break;
            }
        }

        // ------------------------------------------------------------------
        // Filter (7 types)
        // ------------------------------------------------------------------
        DDTable BuildFilterTable()
        {
            // amount -> shared `value`, rest -> shared `flag`. `scope` reuses
            // ConditionPlayerType (FilterPlayerType has the same member names);
            // FilterRandomCount's bool player_self maps to Self/Opponent.
            var t = NewDDTable("Filter",
                "id", "!Id", "type", "FilterType", "value", "!Int", "range", "!Int", "flag", "!Int",
                "scope", "ConditionPlayerType", "stat", "ConditionStatType", "pile", "PileType",
                "card_kind", "CardType", "ref_club", "_ID_Club", "ref_trait", "_ID_Trait");
            foreach (var d in new[] { "Effect.xlsx", "CardData.xlsx", "Club.xlsx", "Trait.xlsx" }) t.refDeps.Add(d);

            DocRow(t, "First", "value", "amount");
            DocRow(t, "Random", "value", "amount", "flag", "rest");
            DocRow(t, "RandomCount", "pile", "pile", "scope", "player_self", "card_kind", "has_type", "ref_club", "has_club", "ref_trait", "has_trait");
            DocRow(t, "HighestStat", "stat", "stat");
            DocRow(t, "LowestStat", "stat", "stat");
            DocRow(t, "MostUnitSlot", "range", "distance", "scope", "player_type");
            DocRow(t, "MostWoundedSlot", "range", "distance", "scope", "player_type");

            var idUsed = new HashSet<string>(StringComparer.Ordinal);
            foreach (var a in LoadSorted<FilterData>())
            {
                string type = MemberName(a.GetType(), "Filter");
                var row = new Dictionary<string, object> { ["type"] = type };
                switch (a)
                {
                    case FilterFirst f: row["value"] = f.amount; break;
                    case FilterRandom f: row["value"] = f.amount; row["flag"] = f.rest ? 1 : 0; break;
                    case FilterRandomCount f:
                        row["pile"] = f.pile.ToString(); row["scope"] = (f.player_self ? ConditionPlayerType.Self : ConditionPlayerType.Opponent).ToString();
                        row["card_kind"] = CardKind(f.has_type); row["ref_club"] = RefName(f.has_club); row["ref_trait"] = RefName(f.has_trait); break;
                    case FilterHighestStat f: row["stat"] = f.stat.ToString(); break;
                    case FilterLowestStat f: row["stat"] = f.stat.ToString(); break;
                    case FilterMostUnitSlot f: row["range"] = f.distance; row["scope"] = f.player_type.ToString(); break;
                    case FilterMostWoundedSlot f: row["range"] = f.distance; row["scope"] = f.player_type.ToString(); break;
                    default: Debug.LogWarning("[DataTableExporter] Unmapped filter: " + a.GetType().Name); break;
                }
                row["id"] = MakeUniqueId(Sanitize(a.name), idUsed);
                t.rows.Add(row);
            }
            return t;
        }

        // ------------------------------------------------------------------
        // Sort (1 type) — descending is on the SortData base.
        // ------------------------------------------------------------------
        DDTable BuildSortTable()
        {
            // descending (SortData base field) -> shared `flag` (0=asc, 1=desc).
            var t = NewDDTable("Sort", "id", "!Id", "type", "SortType", "flag", "!Int", "ref_trait", "_ID_Trait");
            t.refDeps.Add("Trait.xlsx");
            DocRow(t, "Trait", "flag", "descending", "ref_trait", "trait");

            var idUsed = new HashSet<string>(StringComparer.Ordinal);
            foreach (var a in LoadSorted<SortData>())
            {
                string type = MemberName(a.GetType(), "Sort");
                var row = new Dictionary<string, object> { ["type"] = type, ["flag"] = a.descending ? 1 : 0 };
                switch (a)
                {
                    case SortTrait s: row["ref_trait"] = RefName(s.trait); break;
                    default: Debug.LogWarning("[DataTableExporter] Unmapped sort: " + a.GetType().Name); break;
                }
                row["id"] = MakeUniqueId(Sanitize(a.name), idUsed);
                t.rows.Add(row);
            }
            return t;
        }

        // ------------------------------------------------------------------
        // Repeat (2 types)
        // ------------------------------------------------------------------
        DDTable BuildRepeatTable()
        {
            var t = NewDDTable("Repeat",
                "id", "!Id", "type", "RepeatType", "value", "!Int", "scope", "ConditionPlayerType",
                "pile", "PileType", "card_kind", "CardType", "ref_club", "_ID_Club", "ref_trait", "_ID_Trait");
            foreach (var d in new[] { "Effect.xlsx", "CardData.xlsx", "Club.xlsx", "Trait.xlsx" }) t.refDeps.Add(d);

            DocRow(t, "StaticValue", "value", "value");
            DocRow(t, "CountType", "pile", "pile", "scope", "player", "card_kind", "has_type", "ref_club", "has_club", "ref_trait", "has_trait");

            var idUsed = new HashSet<string>(StringComparer.Ordinal);
            foreach (var a in LoadSorted<RepeatConditionData>())
            {
                string type = MemberName(a.GetType(), "Repeat");
                var row = new Dictionary<string, object> { ["type"] = type };
                switch (a)
                {
                    case RepeatStaticValue r: row["value"] = r.value; break;
                    case RepeatCountType r:
                        row["pile"] = r.pile.ToString(); row["scope"] = r.player.ToString();
                        row["card_kind"] = CardKind(r.has_type); row["ref_club"] = RefName(r.has_club); row["ref_trait"] = RefName(r.has_trait); break;
                    default: Debug.LogWarning("[DataTableExporter] Unmapped repeat: " + a.GetType().Name); break;
                }
                row["id"] = MakeUniqueId(Sanitize(a.name), idUsed);
                t.rows.Add(row);
            }
            return t;
        }

        // ------------------------------------------------------------------
        // WideAreaRange — direction offsets + preview sprite path.
        // directions -> two ';'-joined int columns dx / dy (index-aligned;
        // both cells must have the same element count). The player-1 mirroring
        // (dx/dy sign flip) is a RULE, not data — it stays in code.
        // id = asset name (matches Ability.condition_wide_range FK via RefName).
        // ------------------------------------------------------------------
        DDTable BuildWideAreaRangeTable()
        {
            var t = NewDDTable("WideAreaRange", "id", "!Id", "dx", "!Int", "dy", "!Int", "thumbnail", "!String");
            t.doc.Clear();  // single type, no per-type breakdown

            var idUsed = new HashSet<string>(StringComparer.Ordinal);
            foreach (var a in LoadSorted<ConditionWideAreaRange>())
            {
                var dirs = a.directions ?? new List<Direction>();
                var row = new Dictionary<string, object>
                {
                    ["dx"] = string.Join(";", dirs.Where(d => d != null).Select(d => d.dx)),
                    ["dy"] = string.Join(";", dirs.Where(d => d != null).Select(d => d.dy)),
                    ["thumbnail"] = RefName(a.thumnail),
                };
                row["id"] = MakeUniqueId(Sanitize(a.name), idUsed);
                t.rows.Add(row);
            }
            return t;
        }

        // StatusType -> the matching StatusData id (the table is keyed by asset
        // name; StatusData.effect carries the StatusType). Falls back to the
        // enum name when no asset maps to that value.
        static Dictionary<StatusType, string> _statusIdByType;
        static string StatusId(StatusType st)
        {
            if (_statusIdByType == null)
            {
                _statusIdByType = new Dictionary<StatusType, string>();
                foreach (var sd in Resources.LoadAll<StatusData>(""))
                {
                    if (sd == null || _statusIdByType.ContainsKey(sd.effect)) continue;
                    _statusIdByType[sd.effect] = RefName(sd);
                }
            }
            string id;
            return _statusIdByType.TryGetValue(st, out id) ? id : st.ToString();
        }

        static string Sanitize(string s)
        {
            if (string.IsNullOrEmpty(s)) return "x";
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (char ch in s) sb.Append(char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_');
            return sb.ToString();
        }

        string JoinWeighted(List<EffectCreateCard.WeightedCard> wlist)
        {
            if (wlist == null) return string.Empty;
            var parts = new List<string>(wlist.Count);
            foreach (var w in wlist)
            {
                string cn = RefName(w.card);
                if (!string.IsNullOrEmpty(cn)) parts.Add(cn + "=" + w.weight);
            }
            return string.Join(";", parts);
        }

        // ------------------------------------------------------------------
        // Workbook writer: bundles one or more data-driven tables into one
        // .xlsx (Reference, Enum, _컬럼 설명, then one data sheet per table).
        // ------------------------------------------------------------------
        int ExportWorkbook(string outDir, string fileBase, params DDTable[] tables)
        {
            tables = tables.Where(x => x != null).ToArray();
            if (tables.Length == 0) return 0;

            var own = new HashSet<string>(tables.Select(x => x.sheet + ".xlsx"), StringComparer.Ordinal);
            var deps = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var tb in tables) foreach (var d in tb.refDeps) if (!own.Contains(d)) deps.Add(d);

            var sheets = new List<SimpleXlsx.Sheet>();
            sheets.Add(BuildReferencesSheetFrom(deps));
            sheets.Add(BuildEnumsSheet(fileBase));
            sheets.Add(BuildDataDrivenDescSheet(tables));

            int total = 0;
            foreach (var tb in tables)
            {
                sheets.Add(BuildDataSheet(tb.sheet, tb.cols, tb.toks, tb.rows));
                total += tb.rows.Count;
            }

            string outPath = Path.Combine(outDir, fileBase + ".xlsx");
            SimpleXlsx.Write(outPath, sheets.ToArray());
            Debug.Log(string.Format("[DataTableExporter] {0,-10} {1} tables, {2,4} rows -> {3}",
                                    fileBase, tables.Length, total, outPath));
            return total;
        }

        SimpleXlsx.Sheet BuildReferencesSheetFrom(IEnumerable<string> deps)
        {
            var rows = new List<IList<object>>();
            rows.Add(new List<object> { null, null, null });
            foreach (var r in deps)
                rows.Add(new List<object> { null, ReferenceMarker, r });
            return new SimpleXlsx.Sheet { Name = "Reference", Rows = rows, FreezeRows = 0 };
        }

        // _컬럼 설명 for data-driven tables: per table a column dictionary
        // (filled with generic descriptions) + a [type별 슬롯 의미] table that
        // says, for every dispatch type, which original field each slot holds.
        SimpleXlsx.Sheet BuildDataDrivenDescSheet(DDTable[] tables)
        {
            const int margin = 1;
            var rows = new List<IList<object>>();
            rows.Add(new List<object>());  // spacer

            foreach (var tb in tables)
            {
                rows.Add(Indent(margin, "TableName", tb.sheet));
                // column dictionary — header row colored
                rows.Add(HeaderRow(margin, "No", "Name", "Type", "설명", EndField));
                for (int i = 0; i < tb.cols.Count; i++)
                    rows.Add(Indent(margin, i + 1, tb.cols[i], tb.toks[i], SlotColDesc(tb.cols[i])));

                rows.Add(new List<object>());
                rows.Add(Indent(margin, "[type별 슬롯 의미]"));

                // append a trailing "검사 내용" column only when this table has
                // per-type behavior text (Condition/Filter/Sort/Repeat; not Effect).
                bool hasBehavior = false;
                for (int di = 1; di < tb.doc.Count; di++)
                    if (!string.IsNullOrEmpty(TypeBehaviorKo(tb.sheet, tb.doc[di].Count > 0 ? tb.doc[di][0] as string : null)))
                    { hasBehavior = true; break; }

                for (int di = 0; di < tb.doc.Count; di++)
                {
                    var cells = new List<object>(tb.doc[di]);
                    if (hasBehavior)
                        cells.Add(di == 0 ? "검사 내용 (무엇을·어떻게)"
                                          : TypeBehaviorKo(tb.sheet, cells.Count > 0 ? cells[0] as string : null));
                    rows.Add(di == 0 ? HeaderRow(margin, cells) : Indent(margin, cells));
                }
                rows.Add(new List<object>());  // spacer between tables
            }
            return new SimpleXlsx.Sheet { Name = "_컬럼 설명", Rows = rows, FreezeRows = 0 };
        }

        // 컬럼별 한국어 설명 — "이 칸에 어떤 값이 들어가는지". 의미 기반으로 명명한
        // Condition/Filter/Sort/Repeat/WideAreaRange 컬럼을 모두 덮는다. 타입별 세부
        // 의미는 같은 시트의 [type별 슬롯 의미] 표에 별도 기재된다.
        static readonly Dictionary<string, string> COL_DESC_KO = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "id",             "고유 식별자. 에셋 이름이며 능력이 이 값으로 참조" },
            { "type",           "종류. 어떤 판정/동작을 하는지 가르는 분기 값" },
            { "oper",           "비교 연산자. 같음·다름·이상·이하·초과·미만 (참거짓 조건은 같음=참, 다름=거짓)" },
            { "value",          "기준 정수값. 스탯 임계값·인덱스·최소 스택·선택 개수·반복 횟수 등 (확률은 만분율)" },
            { "range",          "이웃 슬롯 반경. 거리" },
            { "flag",           "참거짓. 0=거짓, 1=참 (대각선 포함·최대치 비교·나머지 선택·내림차순 등)" },
            { "scope",          "대상 플레이어. 자신·상대·양쪽" },
            { "stat",           "스탯 종류. 공격·체력·마나·사거리 또는 보스 게이지(스킬·ATG·그로기)" },
            { "total",          "누적 카운트 종류. 누적 회복량 등" },
            { "pile",           "카드 더미. 덱·손·필드·장착·무덤·비밀·임시 등" },
            { "pile_pos",       "더미 내 위치. 맨위·맨아래·지정 인덱스" },
            { "last_type",      "행동 주체/기록. 자신·발동자 또는 마지막 선택·지정·소환·공격·파괴·플레이" },
            { "target_kind",    "대상 분류. 카드·플레이어·슬롯" },
            { "side",           "슬롯 진영 (단일). 아군·적군·중립. 여러 진영의 OR은 CompositeOr로 표현" },
            { "zone",           "슬롯 구역 (단일). 안쪽·바깥·중립. 여러 구역의 OR은 CompositeOr로 표현" },
            { "damage_kind",    "피해 대상 방식. 카드 또는 슬롯" },
            { "value_kind",     "수치 출처. 지정값·공격력·체력" },
            { "status_kind",    "상태이상 분류. 나쁜 것·좋은 것·모두" },
            { "card_kind",      "카드 종류. 빈 칸=필터 없음. 다중 OR은 CompositeOr로 분해, oper=다름이면 ;목록 허용(모두 아님)" },
            { "ref_card",       "참조 카드 식별자. 다중 OR은 CompositeOr로 분해, oper=다름이면 ;목록 허용(모두 아님)" },
            { "ref_club",       "참조 클럽 식별자. 다중 OR은 CompositeOr로 분해, oper=다름이면 ;목록 허용(모두 아님)" },
            { "ref_trait",      "참조 특성 식별자. 다중 OR은 CompositeOr로 분해, oper=다름이면 ;목록 허용(모두 아님)" },
            { "ref_status",     "참조 상태이상 (단일). 상태 식별자" },
            { "bonus_trait",    "보정 특성 (단일). 이 특성값만큼 피해/회복에 가산" },
            { "ref_ability",    "참조 능력 (단일). 능력 식별자" },
            { "ref_weapon",     "참조 무기 (단일). 무기 식별자" },
            { "ref_condition",  "참조 조건 (단일). 조건 식별자" },
            { "sub_conditions", "하위 조건 목록. 조건 식별자, 세미콜론 구분 — CompositeOr는 하나라도 충족(OR), Count는 카드 필터로 모두 충족(AND)" },
            { "effects_true",   "조건 충족 시 실행할 효과 목록. 세미콜론 구분, 순서대로 전부 실행" },
            { "effects_false",  "조건 불충족 시 실행할 효과 목록. 세미콜론 구분, 순서대로 전부 실행" },
            { "weighted_cards", "가중치 카드 목록: 카드식별자=가중치;... (가중 랜덤 1장 선택)" },
            { "x",              "보드 좌표 x (대상 소유자 기준)" },
            { "y",              "보드 좌표 y (대상 소유자 기준)" },
            { "dx",             "가로 오프셋. WideAreaRange는 세미콜론 목록(세로와 순서 정렬), Effect는 단일 대체 이동 방향" },
            { "dy",             "세로 오프셋. WideAreaRange는 세미콜론 목록(가로와 순서 정렬), Effect는 단일 대체 이동 방향" },
            { "thumbnail",      "범위 미리보기 스프라이트. 에셋 경로 문자열" },
        };

        static string SlotColDesc(string col)
        {
            if (COL_DESC_KO.TryGetValue(col, out var ko)) return ko;
            return "[type별 슬롯 의미] 참조";
        }

        // 각 dispatch 타입이 "무엇을 어떻게 검사/동작" 하는지 한국어 한 줄.
        // _컬럼 설명 시트의 [type별 슬롯 의미] 표 마지막 열에 출력된다.
        // 키 = "{시트}.{타입}".
        static readonly Dictionary<string, string> TYPE_BEHAVIOR_KO = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // --- Condition ---
            { "Condition.Stat",          "타깃(카드/플레이어)의 스탯(공격·체력·마나)을 value와 oper로 비교" },
            { "Condition.StatCustom",    "타깃의 특정 특성(trait) 값을 value와 oper로 비교" },
            { "Condition.PlayerStat",    "타깃 플레이어의 스탯(체력·마나)을 value와 oper로 비교 (카드면 소유 플레이어로 환산)" },
            { "Condition.ClubStatMatch", "시전자의 특성값과 소유 클럽 카드의 같은 특성값을 oper로 비교" },
            { "Condition.BossGauge",     "보스 게이지 현재값을 value(또는 최대치)와 oper로 비교. 보스 없으면 거짓" },
            { "Condition.CardType",      "타깃 카드가 지정 종류·클럽·특성에 모두 해당하는지 검사. 다중 OR은 CompositeOr 행으로 분해, oper=다름 + ;목록은 '모두 아님'" },
            { "Condition.CardData",      "타깃 카드가 지정 카드인지 검사. 다중 OR은 CompositeOr 행으로 분해, oper=다름 + ;목록은 '모두 아님'" },
            { "Condition.Status",        "타깃이 해당 상태이상을 value 스택 이상 보유했는지 검사" },
            { "Condition.Damaged",       "타깃이 피해를 입었는지 검사 (카드 damage>0, 플레이어 hp<최대)" },
            { "Condition.Exhaust",       "타깃 카드가 탈진 상태인지 검사" },
            { "Condition.Equipped",      "타깃 카드에 장비가 장착됐는지 검사" },
            { "Condition.Deckbuilding",  "카드가 덱빌딩 가능 카드인지(소환 토큰 아님) 검사" },
            { "Condition.Owner",         "타깃의 소유자가 시전자와 같은지 검사" },
            { "Condition.OwnerAI",       "AI일 때만 타깃 소유자가 시전자와 같은지 검사 (사람은 항상 통과)" },
            { "Condition.Self",          "타깃이 시전자 자신인지 검사" },
            { "Condition.Target",        "현재 타깃의 분류(카드·플레이어·슬롯)가 지정 종류와 일치하는지 검사" },
            { "Condition.Triggered",     "타깃이 이번 능력을 발동시킨 주체인지 검사" },
            { "Condition.Count",         "지정 플레이어·더미에서 sub_conditions(카드 필터, 모두 충족)를 만족하는 카드 수를 value와 oper로 비교" },
            { "Condition.CardPile",      "타깃 카드가 지정 더미(덱·손·필드 등)에 있는지 검사" },
            { "Condition.PilePosition",  "타깃 카드가 더미의 지정 위치(맨위·맨아래·인덱스)에 있는지 검사" },
            { "Condition.CanPlace",      "지정 카드를 타깃 슬롯에 배치할 수 있는지 검사" },
            { "Condition.SlotDist",      "타깃 슬롯이 시전자로부터 range 이동거리 내인지 검사 (대각선 포함=flag)" },
            { "Condition.SlotRange",     "타깃 슬롯이 시전자 사거리 내 이웃인지 검사" },
            { "Condition.SlotNeighbor",  "타깃 슬롯이 시전자 기준 range 이웃 범위인지 검사" },
            { "Condition.SlotPid",       "타깃 슬롯이 지정 진영(side, 단일)인지 검사. 여러 진영의 OR은 CompositeOr 행으로 분해됨" },
            { "Condition.SlotLocate",    "타깃 슬롯이 지정 구역(zone, 단일)인지 검사. 여러 구역의 OR은 CompositeOr 행으로 분해됨" },
            { "Condition.SlotAttachmentEmpty", "타깃 슬롯의 부착 칸이 비었는지 검사" },
            { "Condition.SlotUnitEmpty", "타깃 슬롯에 유닛이 없는지 검사" },
            { "Condition.Turn",          "현재 시전자의 턴인지 검사" },
            { "Condition.Rolled",        "주사위 결과값을 value와 oper로 비교" },
            { "Condition.Once",          "이번 능력이 이 턴에 아직 발동되지 않았는지 검사" },
            { "Condition.Possibility",   "value(만분율) 확률로 통과" },
            { "Condition.None",          "항상 참 (조건 없음)" },
            { "Condition.LastTypeExist", "마지막 행동 기록(예: 마지막 선택)이 존재하는지 검사" },
            { "Condition.LastTypeRange", "타깃이 마지막 행동(공격·지정·소환·파괴·플레이) 위치 기준 range 내인지 검사" },
            { "Condition.CompositeOr",   "하위 조건 중 하나라도 충족하면 참 (OR 합성)" },
            // --- Effect ---
            { "Effect.SetStat",          "대상의 스탯을 능력 value로 설정" },
            { "Effect.AddStat",          "대상의 스탯에 능력 value를 가산 (flag=1이면 저장된 값 사용)" },
            { "Effect.ResetStat",        "대상의 스탯을 원본 카드 수치로 되돌림" },
            { "Effect.SetStatCustom",    "대상의 커스텀 스탯(특성)을 능력 value로 설정" },
            { "Effect.AddStatRoll",      "대상의 스탯에 주사위 결과값을 가산" },
            { "Effect.AddStatCount",     "지정 더미의 (조건 일치) 카드 수 × 능력 value를 스탯에 가산" },
            { "Effect.AddStatTotalCount","누적 카운트(누적 회복량 등)를 스탯에 가산" },
            { "Effect.CopyStat",         "시전자의 특성값을 대상에 복사 (flag=1이면 대상에 특성이 없을 때만)" },
            { "Effect.CycleStat",        "특성값을 순환 카운터로 1 증가" },
            { "Effect.Damage",           "대상에 피해 (수치 출처=value_kind, 보정 특성 가산)" },
            { "Effect.DamageRatio",      "대상 체력의 value(만분율) 비율만큼 피해" },
            { "Effect.DamageCount",      "지정 더미의 (조건 일치) 카드 수 × 능력 value만큼 피해" },
            { "Effect.Heal",             "대상 회복 (수치 출처=value_kind, 보정 특성 가산)" },
            { "Effect.Draw",             "능력 value장 드로우" },
            { "Effect.Discard",          "대상 카드를 버림" },
            { "Effect.Shuffle",          "덱을 섞음" },
            { "Effect.Create",           "대상 카드데이터로 새 카드를 지정 더미에 생성 (scope=받는 쪽)" },
            { "Effect.CreateCard",       "가중치 목록에서 뽑은 카드를 지정 더미에 생성 (flag=1이면 균등 확률)" },
            { "Effect.SendPile",         "대상 카드를 지정 더미로 이동 (pile_pos=맨위/맨아래)" },
            { "Effect.MovePileTopToBottom", "지정 더미 맨위 카드를 맨아래로" },
            { "Effect.ClearTemp",        "임시 더미를 비움" },
            { "Effect.Play",             "대상 카드를 플레이" },
            { "Effect.PlayCard",         "지정 카드를 플레이" },
            { "Effect.UseCard",          "지정 카드를 사용 (scope=사용 주체)" },
            { "Effect.Transform",        "대상 카드를 지정 카드로 변신" },
            { "Effect.SummonSlot",       "지정 좌표(x,y)에 소환, 차 있으면 (dx,dy) 방향으로 밀며 시도" },
            { "Effect.MoveUnit",         "지정 주체(last_type)를 대상 슬롯으로 이동" },
            { "Effect.Knockback",        "대상을 밀쳐냄" },
            { "Effect.Attack",           "지정 주체(last_type)가 대상을 공격" },
            { "Effect.AttackRedirect",   "지정 주체(last_type)의 공격을 대상으로 돌림" },
            { "Effect.AddAbility",       "대상에 능력 부여" },
            { "Effect.RemoveAbility",    "대상의 능력 제거" },
            { "Effect.AddTrait",         "대상에 특성 부여 (값=능력 value)" },
            { "Effect.RemoveTrait",      "대상의 특성 제거" },
            { "Effect.AddClub",          "대상에 클럽 부여" },
            { "Effect.ClearStatus",      "대상의 상태이상 제거 (ref_status 지정 시 그것만, 아니면 status_kind 일괄)" },
            { "Effect.AttachCard",       "지정 카드를 대상 슬롯에 부착" },
            { "Effect.ChangeWeapon",     "대상의 무기를 교체" },
            { "Effect.ChangeOwner",      "대상 카드의 소유자를 변경 (scope=새 소유자)" },
            { "Effect.Destroy",          "대상 파괴" },
            { "Effect.DestroyEquip",     "대상의 장비 파괴" },
            { "Effect.Exhaust",          "대상의 탈진 상태를 flag 값으로 설정" },
            { "Effect.Mana",             "마나를 능력 value만큼 증감" },
            { "Effect.Roll",             "value면 주사위를 굴림" },
            { "Effect.BossGauge",        "보스 게이지를 value만큼 증감 (flag=1이면 value로 설정)" },
            { "Effect.StoreCount",       "지정 더미의 (조건 일치) 카드 수를 저장 (이후 use_stored_value로 사용)" },
            { "Effect.SetClubCardUI",    "클럽 카드 UI 상태 갱신" },
            { "Effect.Conditional",      "대상이 조건 충족 시 effects_true, 아니면 effects_false 실행" },
            { "Effect.ConditionalCaster","시전자가 조건 충족 시 effects_true, 아니면 effects_false 실행" },
            // --- Filter ---
            { "Filter.First",            "앞에서부터 value개 선택" },
            { "Filter.Random",           "무작위 value개 선택 (flag=1이면 선택분을 제외한 나머지를 선택)" },
            { "Filter.RandomCount",      "지정 더미의 (조건 일치) 카드 수만큼 무작위 선택" },
            { "Filter.HighestStat",      "지정 스탯이 가장 높은 대상만 선택" },
            { "Filter.LowestStat",       "지정 스탯이 가장 낮은 대상만 선택" },
            { "Filter.MostUnitSlot",     "range 내 유닛이 가장 많은 슬롯 1개 선택" },
            { "Filter.MostWoundedSlot",  "range 내 부상 유닛이 가장 많은 슬롯 1개 선택" },
            // --- Sort ---
            { "Sort.Trait",              "지정 특성값 기준 정렬 (flag=1이면 내림차순)" },
            // --- Repeat ---
            { "Repeat.StaticValue",      "고정 value회 반복" },
            { "Repeat.CountType",        "지정 플레이어·더미의 (조건 일치) 카드 수만큼 반복" },
        };

        static string TypeBehaviorKo(string sheet, string type)
            => type != null && TYPE_BEHAVIOR_KO.TryGetValue(sheet + "." + type, out var d) ? d : "";

        // ------------------------------------------------------------------
        // CardData / AbilityData / WeaponData: explicit columns for ordering
        // ------------------------------------------------------------------
        static readonly string[] CardColumns =
        {
            "id", "title", "type", "mana", "attack", "hp",
            "clubs", "weapon", "traits", "stats", "abilities",
            "text", "text_format", "text_values", "desc",
            "deckbuilding", "cost", "packs",
            "art_full", "art_board",
            "spawn_fx", "death_fx", "attack_fx", "damage_fx", "idle_fx",
            "spawn_audio", "death_audio", "attack_audio", "damage_audio",
        };

        IDictionary<string, object> BuildCardRow(CardData c)
        {
            return new Dictionary<string, object>
            {
                { "id", c.id },
                { "title", c.title },
                { "type", c.type.ToString() },
                { "mana", c.mana },
                { "attack", c.attack },
                { "hp", c.hp },
                { "clubs", JoinRefs(c.clubs) },
                { "weapon", RefName(c.weapon) },
                { "traits", JoinRefs(c.traits) },
                { "stats", JoinTraitStats(c.stats) },
                { "abilities", JoinRefs(c.abilities) },
                { "text", c.text },
                { "text_format", c.text_format },
                { "text_values", JoinRefs(c.text_values) },
                { "desc", c.desc },
                { "deckbuilding", c.deckbuilding },
                { "cost", c.cost },
                { "packs", JoinRefs(c.packs) },
                { "art_full", RefName(c.art_full) },
                { "art_board", RefName(c.art_board) },
                { "spawn_fx", RefName(c.spawn_fx) },
                { "death_fx", RefName(c.death_fx) },
                { "attack_fx", RefName(c.attack_fx) },
                { "damage_fx", RefName(c.damage_fx) },
                { "idle_fx", RefName(c.idle_fx) },
                { "spawn_audio", RefName(c.spawn_audio) },
                { "death_audio", RefName(c.death_audio) },
                { "attack_audio", RefName(c.attack_audio) },
                { "damage_audio", RefName(c.damage_audio) },
            };
        }

        static readonly string[] AbilityColumns =
        {
            "id", "title", "desc", "selector_desc",
            "trigger", "criteria_target",
            "conditions_trigger", "conditions_criteria_target",
            "condition_wide_range", "condition_target", "filters_target", "sort_target",
            "condition_repeat",
            "effects", "status",
            "value", "duration", "can_cancel",
            "chain_abilities", "mana_cost", "exhaust", "charge_target",
            "show_card_fx", "board_fx", "caster_fx", "target_fx",
            "cast_audio", "target_audio",
        };

        IDictionary<string, object> BuildAbilityRow(AbilityData a)
        {
            return new Dictionary<string, object>
            {
                { "id", a.id },
                { "title", a.title },
                { "desc", a.desc },
                { "selector_desc", a.selector_desc },
                { "trigger", a.trigger.ToString() },
                { "criteria_target", a.criteria_target.ToString() },
                { "conditions_trigger", JoinRefs(a.conditions_trigger) },
                { "conditions_criteria_target", JoinRefs(a.conditions_criteria_target) },
                { "condition_wide_range", RefName(a.condition_wide_range) },
                { "condition_target", JoinRefs(a.condition_target) },
                { "filters_target", JoinRefs(a.filters_target) },
                { "sort_target", RefName(a.sort_target) },
                { "condition_repeat", RefName(a.condition_repeat) },
                { "effects", JoinRefs(a.effects) },
                { "status", JoinRefs(a.status) },
                { "value", a.value },
                { "duration", a.duration },
                { "can_cancel", a.can_cancel },
                { "chain_abilities", JoinRefs(a.chain_abilities) },
                { "mana_cost", a.mana_cost },
                { "exhaust", a.exhaust },
                { "charge_target", a.charge_target },
                { "show_card_fx", a.show_card_fx },
                { "board_fx", RefName(a.board_fx) },
                { "caster_fx", RefName(a.caster_fx) },
                { "target_fx", RefName(a.target_fx) },
                { "cast_audio", RefName(a.cast_audio) },
                { "target_audio", RefName(a.target_audio) },
            };
        }

        static readonly string[] WeaponColumns = { "id", "weapon_class", "type", "range" };

        IDictionary<string, object> BuildWeaponRow(WeaponData w)
        {
            return new Dictionary<string, object>
            {
                { "id", w.GetWeaponID() },
                { "weapon_class", w.GetType().Name },
                { "type", w.GetWeaponType().ToString() },
                { "range", w.GetDefaultRange() },
            };
        }

        // ------------------------------------------------------------------
        // Reflection helpers
        // ------------------------------------------------------------------
        static readonly HashSet<string> SKIP_FIELDS = new HashSet<string>
        {
            "card_list", "card_dict", "ability_list", "ability_dict",
            "academy_list", "club_list", "trait_list", "status_list",
            "weapon_list", "deck_list", "level_list", "pack_list",
            "variant_list", "cardback_list", "avatar_list", "rarity_list",
            "effect_list", "condition_list", "filter_list",
        };

        void DumpAllFields(object obj, IDictionary<string, object> row)
        {
            if (obj == null) return;
            for (Type t = obj.GetType(); t != null && t != typeof(UnityEngine.Object); t = t.BaseType)
            {
                var fields = t.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                foreach (var f in fields)
                {
                    if (f.IsStatic) continue;
                    if (f.IsNotSerialized) continue;
                    if (SKIP_FIELDS.Contains(f.Name)) continue;
                    if (row.ContainsKey(f.Name)) continue;

                    object val;
                    try { val = f.GetValue(obj); }
                    catch { continue; }
                    row[f.Name] = ConvertValue(val);
                }
            }
        }

        object ConvertValue(object val)
        {
            if (val == null) return null;
            Type t = val.GetType();

            if (t.IsEnum) return val.ToString();
            if (val is bool b) return b;
            if (val is string s) return s;
            // float/double -> scaled int (10000 = 1.0); the spec has no float type.
            if (val is float fv) return (int)System.Math.Round(fv * 10000.0);
            if (val is double dv) return (int)System.Math.Round(dv * 10000.0);
            if (t.IsPrimitive) return val;

            if (val is UnityEngine.Object uo)
                return RefName(uo);

            if (val is Color col)
                return string.Format(CultureInfo.InvariantCulture, "r={0},g={1},b={2},a={3}",
                                     col.r, col.g, col.b, col.a);
            if (val is Color32 col32)
                return string.Format(CultureInfo.InvariantCulture, "r={0},g={1},b={2},a={3}",
                                     col32.r, col32.g, col32.b, col32.a);

            if (val is System.Collections.IEnumerable enumerable)
            {
                var parts = new List<string>();
                foreach (var item in enumerable)
                {
                    object cv = ConvertValue(item);
                    string str = cv == null ? string.Empty : cv.ToString();
                    if (!string.IsNullOrEmpty(str)) parts.Add(str);
                }
                return string.Join(";", parts);
            }

            if (t.IsValueType)
                return StructToString(val);

            return val.ToString();
        }

        string StructToString(object val)
        {
            Type t = val.GetType();
            var fields = t.GetFields(BindingFlags.Public | BindingFlags.Instance);
            var parts = new List<string>(fields.Length);
            foreach (var f in fields)
            {
                object v = ConvertValue(f.GetValue(val));
                parts.Add(f.Name + "=" + (v ?? string.Empty));
            }
            return string.Join(",", parts);
        }

        // ------------------------------------------------------------------
        // Reference resolution
        //   - WeaponData      -> GetWeaponID()  (the runtime id, "FRONT" etc.)
        //   - ScriptableObject -> the `id` field if any, else .name
        //   - External asset   -> AssetDatabase asset path
        // ------------------------------------------------------------------
        public static string RefName(UnityEngine.Object obj)
        {
            if (obj == null) return string.Empty;

            if (obj is WeaponData w) return w.GetWeaponID();

            if (obj is ScriptableObject)
            {
                var idField = obj.GetType().GetField("id", BindingFlags.Public | BindingFlags.Instance);
                if (idField != null && idField.FieldType == typeof(string))
                {
                    var id = idField.GetValue(obj) as string;
                    if (!string.IsNullOrEmpty(id)) return id;
                }
                return obj.name;
            }

#if UNITY_EDITOR
            string path = AssetDatabase.GetAssetPath(obj);
            if (!string.IsNullOrEmpty(path)) return path;
#endif
            return obj.name;
        }

        static string JoinRefs(System.Collections.IEnumerable items)
        {
            if (items == null) return string.Empty;
            var parts = new List<string>();
            foreach (var item in items)
            {
                if (item is UnityEngine.Object uo)
                {
                    string n = RefName(uo);
                    if (!string.IsNullOrEmpty(n)) parts.Add(n);
                }
            }
            return string.Join(";", parts);
        }

        static string JoinTraitStats(TraitStat[] stats)
        {
            if (stats == null) return string.Empty;
            var parts = new List<string>(stats.Length);
            foreach (var s in stats)
            {
                if (s.trait == null) continue;
                parts.Add(RefName(s.trait) + "=" + s.value);
            }
            return string.Join(";", parts);
        }
    }
}
