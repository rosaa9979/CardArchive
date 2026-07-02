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
    //     vocabulary (value/range/flag/scope/stat/pile/card_kinds/ref_*) —
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
                total += ExportWorkbook(outDir, "Effect",
                    BuildDDTable<EffectData>("Effect", "Effect", null));
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
            { "Effect",    new[] {
                typeof(EffectType),
                typeof(EffectStatType), typeof(EffectTotalCountType),
                typeof(EffectDamageType), typeof(EffectValueType),
                typeof(PileType), typeof(EffectStatusType),
                typeof(EffectActionType), typeof(EffectPlayerType),
                typeof(DeckInsert), typeof(BossGaugeType),
            } },
            { "Weapon",    new[] { typeof(WeaponType) } },
            { "Status",    new[] { typeof(StatusType) } },
            // Condition.xlsx bundles Condition/Filter/Sort/Repeat/WideAreaRange
            // sheets, so it owns all of their enums. FilterPlayerType and
            // PosMode are gone from the table: filters share ConditionPlayerType
            // (same member names), PosMode is exported as virtual PilePosType.
            { "Condition", new[] {
                typeof(ConditionType), typeof(FilterType), typeof(SortType), typeof(RepeatType),
                typeof(ConditionOperator),
                typeof(ConditionStatType), typeof(ConditionPlayerType),
                typeof(ConditionLastType), typeof(ConditionTargetType),
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
            // TODO-2: ConditionStatType extended with the boss gauges so the
            // Condition/Filter `stat` column replaces the old `gauge` column.
            { "ConditionStatType", new[] {
                new EnumMember("None", 0), new EnumMember("Attack", 10),
                new EnumMember("HP", 20), new EnumMember("Mana", 30),
                new EnumMember("BossSkill", 40), new EnumMember("BossAtg", 41),
                new EnumMember("BossGroggy", 42),
            } },
            // TODO-3: ConditionPilePosition.PosMode promoted to top-level PilePosType.
            { "PilePosType", new[] {
                new EnumMember("Top", 0), new EnumMember("Bottom", 1), new EnumMember("Index", 2),
            } },
        };

        static readonly Dictionary<string, string[]> ENUM_MEMBER_EXCLUDE = new Dictionary<string, string[]>
        {
            // TODO-4: WideAreaRange has its own sheet — not a Condition dispatch value.
            { "ConditionType", new[] { "WideAreaRange" } },
        };

        static readonly Dictionary<string, string[]> ENUM_VIRTUAL = new Dictionary<string, string[]>
        {
            { "Condition", new[] { "PilePosType" } },
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
        // Data-driven tables (Effect / Condition family)
        //
        // One row per asset; columns are compressed BY DATA TYPE, not by
        // meaning. Each subclass's fields pack into generic typed slots:
        //   - one column per enum type   (e.g. `pile`, `oper`)
        //   - `ref1..refN`               : table refs (any `_ID_*`; just the id)
        //   - `int1..intM`               : int / bool(0/1) / float(x10000) / SlotXY(x,y)
        //   - `create_card`, `directions`: special list encodings
        // The `id` column is `{Type}_{name}` (dispatch type == prefix before
        // the first `_`), so there is no separate `type` column. Every slot's
        // per-type meaning is written to `_컬럼 설명` (BuildDataDrivenDescSheet).
        // ==================================================================
        // Enum     = a "common" enum (whitelisted) kept as a real typed column.
        // EnumStr  = a non-common single enum -> member-name string (`str` pool).
        // EnumList = a non-common enum list   -> joined member names (`str` pool).
        enum DDCat { Enum, EnumStr, EnumList, Ref, RefList, StatusRef, Int, Slot, Weighted, Direction, Skip }

        class DDSlot { public FieldInfo f; public DDCat c; public string col; public string col2; }

        class DDTable
        {
            public string sheet;
            public List<string> cols = new List<string>();
            public List<string> toks = new List<string>();
            public List<IDictionary<string, object>> rows = new List<IDictionary<string, object>>();
            public List<IList<object>> doc = new List<IList<object>>();   // per-type slot meaning (row 0 = header)
            public SortedSet<string> refDeps = new SortedSet<string>(StringComparer.Ordinal);
        }

        // ConditionOperatorBool/Int unify into one ConditionOperator.
        static Type NormEnum(Type t)
            => (t == typeof(ConditionOperatorBool) || t == typeof(ConditionOperatorInt))
               ? typeof(ConditionOperator) : t;

        // "Common" enums (used across many subtypes) keep a real typed column;
        // every other enum becomes a member-name string. Extend as needed.
        static readonly Dictionary<Type, string> COMMON_ENUM_COL = new Dictionary<Type, string>
        {
            { typeof(ConditionOperator), "oper" },
        };
        static bool IsCommonEnum(Type e) => COMMON_ENUM_COL.ContainsKey(e);
        static string CommonEnumColName(Type e) => COMMON_ENUM_COL.TryGetValue(e, out var n) ? n : e.Name;

        static IEnumerable<FieldInfo> DDFields(Type t)
        {
            var seen = new HashSet<string>();
            for (; t != null && t != typeof(ScriptableObject) && t != typeof(UnityEngine.Object); t = t.BaseType)
                foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                    if (!f.IsStatic && !f.IsNotSerialized && seen.Add(f.Name))
                        yield return f;
        }

        static DDCat DDCategorize(FieldInfo f, out Type core)
        {
            Type ft = f.FieldType; bool isList = false; core = ft;
            if (ft.IsArray) { core = ft.GetElementType(); isList = true; }
            else if (ft.IsGenericType && ft.GetGenericTypeDefinition() == typeof(List<>))
            { core = ft.GetGenericArguments()[0]; isList = true; }

            if (ft == typeof(SlotXY)) return DDCat.Slot;
            if (isList && core == typeof(EffectCreateCard.WeightedCard)) return DDCat.Weighted;
            if (isList && core == typeof(Direction)) return DDCat.Direction;
            // StatusType identifies a specific status -> reference the Status
            // table by id (resolved via StatusData.effect) instead of the enum.
            if (!isList && core == typeof(StatusType)) return DDCat.StatusRef;
            if (core.IsEnum)
            {
                if (isList) return DDCat.EnumList;
                return IsCommonEnum(NormEnum(core)) ? DDCat.Enum : DDCat.EnumStr;
            }
            if (typeof(ScriptableObject).IsAssignableFrom(core)) return isList ? DDCat.RefList : DDCat.Ref;
            if (core == typeof(int) || core == typeof(bool) || core == typeof(float) || core == typeof(double)
                || core == typeof(short) || core == typeof(byte) || core == typeof(long)
                || core == typeof(uint) || core == typeof(ushort) || core == typeof(sbyte) || core == typeof(ulong))
                return DDCat.Int;
            return DDCat.Skip;  // Sprite / GameObject / AudioClip / string (editor-only, dropped)
        }

        DDTable BuildDDTable<T>(string sheet, string classPrefix, Func<Type, bool> exclude)
            where T : ScriptableObject
        {
            var assets = Resources.LoadAll<T>("")
                .Where(a => a != null && (exclude == null || !exclude(a.GetType())))
                .OrderBy(a => a.name, StringComparer.Ordinal).ToArray();
            if (assets.Length == 0)
            {
                Debug.LogWarning(string.Format("[DataTableExporter] {0}: no assets found.", sheet));
                return null;
            }

            var subclasses = assets.Select(a => a.GetType()).Distinct()
                                   .OrderBy(s => s.Name, StringComparer.Ordinal).ToList();

            var t = new DDTable { sheet = sheet };
            var fieldsBySub = new Dictionary<Type, List<DDSlot>>();
            var commonEnumTypes = new List<Type>();   // dedicated typed enum columns (whitelisted)
            var refToks = new SortedSet<string>(StringComparer.Ordinal);
            int maxStr = 0, maxRef = 0, maxInt = 0;
            bool hasWeighted = false, hasDirection = false;

            // pass 1: analyze each subclass, accumulate max slot counts.
            foreach (var sub in subclasses)
            {
                var list = new List<DDSlot>();
                int sc = 0, rc = 0, ic = 0;
                foreach (var f in DDFields(sub))
                {
                    Type core; var c = DDCategorize(f, out core);
                    if (c == DDCat.Skip) continue;
                    list.Add(new DDSlot { f = f, c = c });
                    switch (c)
                    {
                        case DDCat.Enum: { Type e = NormEnum(f.FieldType); if (!commonEnumTypes.Contains(e)) commonEnumTypes.Add(e); } break;
                        case DDCat.EnumStr: sc++; break;
                        case DDCat.EnumList: sc++; break;
                        case DDCat.Ref: rc++; { string tn = TableNameOf(core); refToks.Add("_ID_" + tn); t.refDeps.Add(tn + ".xlsx"); } break;
                        case DDCat.RefList: rc++; { string tn = TableNameOf(core); refToks.Add("_ID_" + tn + "[]"); t.refDeps.Add(tn + ".xlsx"); } break;
                        case DDCat.StatusRef: rc++; refToks.Add("_ID_Status"); t.refDeps.Add("Status.xlsx"); break;
                        case DDCat.Int: ic++; break;
                        case DDCat.Slot: ic += 2; break;
                        case DDCat.Weighted: hasWeighted = true; t.refDeps.Add("CardData.xlsx"); break;
                        case DDCat.Direction: hasDirection = true; break;
                    }
                }
                fieldsBySub[sub] = list;
                if (sc > maxStr) maxStr = sc;
                if (rc > maxRef) maxRef = rc;
                if (ic > maxInt) maxInt = ic;
            }

            // build the uniform column set + type tokens. Common enums get a real
            // typed column; every other enum is a member-name string (`str` pool);
            // refs/ints pool the same way.
            Action<string, string> add = (col, tok) => { t.cols.Add(col); t.toks.Add(tok); };

            add("id", "!Id");
            foreach (var e in commonEnumTypes) add(CommonEnumColName(e), e.Name);
            for (int i = 1; i <= maxStr; i++) add("str" + i, "!String");
            string refTok = refToks.Count > 0 ? string.Join(";", refToks) : "_ID_Ref";
            for (int i = 1; i <= maxRef; i++) add("ref" + i, refTok);
            if (hasWeighted) add("create_card", "WeightedCard[]");
            if (hasDirection) add("directions", "Direction[]");
            for (int i = 1; i <= maxInt; i++) add("int" + i, "!Int");

            // pass 2: per-subclass field -> column assignment.
            foreach (var sub in subclasses)
            {
                int s = 0, r = 0, ii = 0;
                foreach (var sl in fieldsBySub[sub])
                {
                    switch (sl.c)
                    {
                        case DDCat.Enum: sl.col = CommonEnumColName(NormEnum(sl.f.FieldType)); break;
                        case DDCat.EnumStr: case DDCat.EnumList: sl.col = "str" + (++s); break;
                        case DDCat.Ref: case DDCat.RefList: case DDCat.StatusRef: sl.col = "ref" + (++r); break;
                        case DDCat.Int: sl.col = "int" + (++ii); break;
                        case DDCat.Slot: sl.col = "int" + (++ii); sl.col2 = "int" + (++ii); break;
                        case DDCat.Weighted: sl.col = "create_card"; break;
                        case DDCat.Direction: sl.col = "directions"; break;
                    }
                }
            }

            // data rows.
            var idUsed = new HashSet<string>(StringComparer.Ordinal);
            foreach (var a in assets)
            {
                Type sub = a.GetType();
                string member = sub.Name.StartsWith(classPrefix) ? sub.Name.Substring(classPrefix.Length) : sub.Name;
                string id = member + "_" + Sanitize(a.name);
                string baseId = id; int k = 2; while (!idUsed.Add(id)) id = baseId + "_" + (k++);

                var row = new Dictionary<string, object>(); row["id"] = id;
                foreach (var sl in fieldsBySub[sub])
                {
                    object val; try { val = sl.f.GetValue(a); } catch { continue; }
                    switch (sl.c)
                    {
                        case DDCat.Enum: row[sl.col] = RemapEnum(sl.f.FieldType, val); break;
                        case DDCat.EnumStr: row[sl.col] = val == null ? null : val.ToString(); break;
                        case DDCat.EnumList: row[sl.col] = ConvertValue(val); break;
                        case DDCat.Ref: row[sl.col] = RefName(val as UnityEngine.Object); break;
                        case DDCat.RefList: row[sl.col] = JoinRefs(val as System.Collections.IEnumerable); break;
                        case DDCat.StatusRef: row[sl.col] = StatusId((StatusType)val); break;
                        case DDCat.Int: row[sl.col] = ScaleInt(val); break;
                        case DDCat.Slot: { var s = (SlotXY)val; row[sl.col] = s.x; row[sl.col2] = s.y; } break;
                        case DDCat.Weighted: row[sl.col] = JoinWeighted(val); break;
                        case DDCat.Direction: row[sl.col] = JoinDirections(val); break;
                    }
                }
                t.rows.Add(row);
            }

            // per-type slot-meaning table.
            var head = new List<object> { "type" };
            for (int i = 1; i < t.cols.Count; i++) head.Add(t.cols[i]);
            t.doc.Add(head);
            foreach (var sub in subclasses)
            {
                string member = sub.Name.StartsWith(classPrefix) ? sub.Name.Substring(classPrefix.Length) : sub.Name;
                var byCol = new Dictionary<string, string>();
                foreach (var sl in fieldsBySub[sub])
                {
                    if (sl.c == DDCat.Slot) { byCol[sl.col] = sl.f.Name + ".x"; byCol[sl.col2] = sl.f.Name + ".y"; }
                    else byCol[sl.col] = DescribeSlot(sl);
                }
                var dr = new List<object> { member };
                for (int i = 1; i < t.cols.Count; i++) { string d; byCol.TryGetValue(t.cols[i], out d); dr.Add(string.IsNullOrEmpty(d) ? "-" : d); }
                t.doc.Add(dr);
            }

            return t;
        }

        // ==================================================================
        // Condition-family semantic tables (v2)
        //   Condition / WideAreaRange / Filter / Sort / Repeat.
        //
        // Unlike the generic BuildDDTable pooling (str1/ref1/int1...), these use
        // hand-mapped, MEANING-named columns sharing ONE column vocabulary
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
        //   - single-vs-list refs unified into ';'-joined ref columns.
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

        // Single CardType field -> `card_kinds` list cell. None means
        // "no type filter" in code, which a list encodes as an empty cell.
        static string CardKind(CardType kind)
            => kind == CardType.None ? string.Empty : kind.ToString();

        static string JoinEnums(System.Collections.IEnumerable items)
        {
            if (items == null) return string.Empty;
            var parts = new List<string>();
            foreach (var it in items) if (it != null) parts.Add(it.ToString());
            return string.Join(";", parts);
        }

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
            var t = NewDDTable("Condition",
                "id", "!Id", "type", "ConditionType", "oper", "ConditionOperator",
                "value", "!Int", "range", "!Int", "flag", "!Int",
                "scope", "ConditionPlayerType", "stat", "ConditionStatType",
                "pile", "PileType", "pile_pos", "PilePosType",
                "last_type", "ConditionLastType", "target_kind", "ConditionTargetType",
                "card_kinds", "CardType",
                "ref_cards", "_ID_CardData", "ref_clubs", "_ID_Club",
                "ref_traits", "_ID_Trait", "ref_status", "_ID_Status",
                "sub_conditions", "_ID_Condition");
            foreach (var d in new[] { "Effect.xlsx", "CardData.xlsx", "Club.xlsx", "Trait.xlsx", "Status.xlsx" }) t.refDeps.Add(d);

            // [type별 슬롯 의미] — col -> original field name
            DocRow(t, "Stat", "stat", "type", "oper", "oper", "value", "value");
            DocRow(t, "StatCustom", "ref_traits", "trait", "oper", "oper", "value", "value");
            DocRow(t, "PlayerStat", "stat", "type", "oper", "oper", "value", "value");
            DocRow(t, "ClubStatMatch", "ref_clubs", "club", "ref_traits", "trait", "oper", "oper");
            DocRow(t, "BossGauge", "stat", "gauge(Boss*)", "oper", "oper", "value", "value", "flag", "compare_to_max");
            DocRow(t, "CardType", "card_kinds", "has_type", "ref_clubs", "has_club", "ref_traits", "has_trait", "oper", "oper");
            DocRow(t, "CardData", "ref_cards", "card_types", "oper", "oper");
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
                "card_kinds", "has_type", "ref_clubs", "has_club", "ref_traits", "has_trait", "ref_cards", "has_card");
            DocRow(t, "CardPile", "pile", "type", "oper", "oper");
            DocRow(t, "PilePosition", "pile", "pile", "pile_pos", "mode", "value", "index", "oper", "oper");
            DocRow(t, "CanPlace", "last_type", "last_type", "ref_cards", "place_card", "scope", "card_owner", "oper", "oper");
            DocRow(t, "SlotDist", "range", "distance", "flag", "diagonals");
            DocRow(t, "SlotRange", "oper", "oper");
            DocRow(t, "SlotNeighbor", "range", "range");
            DocRow(t, "SlotPid", "flag", "아군(1)+적군(2)+중립(4) 비트합 (SlotSideMask)");
            DocRow(t, "SlotLocate", "flag", "안쪽(1)+바깥(2)+중립(4) 비트합 (SlotZoneMask)");
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
            foreach (var a in LoadSorted<ConditionData>(tp => tp == typeof(ConditionWideAreaRange)))
            {
                string type = MemberName(a.GetType(), "Condition");
                var row = new Dictionary<string, object> { ["type"] = type };
                FillConditionRow(a, row);
                row["id"] = MakeUniqueId(Sanitize(a.name), idUsed);
                t.rows.Add(row);
            }
            return t;
        }

        void FillConditionRow(ConditionData a, IDictionary<string, object> row)
        {
            switch (a)
            {
                case ConditionBossGauge c:
                    // gauge -> extended ConditionStatType member (BossSkill/BossAtg/BossGroggy)
                    row["stat"] = "Boss" + c.gauge; row["oper"] = Oper(c.oper); row["value"] = c.value; row["flag"] = c.compare_to_max ? 1 : 0; break;
                case ConditionCanPlace c:
                    row["last_type"] = c.last_type.ToString(); row["ref_cards"] = RefName(c.place_card); row["scope"] = c.card_owner.ToString(); row["oper"] = Oper(c.oper); break;
                case ConditionCardData c:
                    row["ref_cards"] = JoinRefs(c.card_types); row["oper"] = Oper(c.oper); break;
                case ConditionCardPile c:
                    row["pile"] = c.type.ToString(); row["oper"] = Oper(c.oper); break;
                case ConditionCardType c:
                    row["card_kinds"] = JoinEnums(c.has_type); row["ref_clubs"] = JoinRefs(c.has_club); row["ref_traits"] = JoinRefs(c.has_trait); row["oper"] = Oper(c.oper); break;
                case ConditionClubStatMatch c:
                    row["ref_clubs"] = RefName(c.club); row["ref_traits"] = RefName(c.trait); row["oper"] = Oper(c.oper); break;
                case ConditionCompositeOr c:
                    row["sub_conditions"] = JoinConditionRefs(c.any); break;
                case ConditionCount c:
                    row["scope"] = c.target.ToString(); row["pile"] = c.pile.ToString(); row["oper"] = Oper(c.oper); row["value"] = c.value;
                    row["card_kinds"] = JoinEnums(c.has_type); row["ref_clubs"] = JoinRefs(c.has_club); row["ref_traits"] = JoinRefs(c.has_trait); row["ref_cards"] = JoinRefs(c.has_card); break;
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
                case ConditionSlotAttachmentEmpty c: row["oper"] = Oper(c.oper); break;
                case ConditionSlotDist c: row["range"] = c.distance; row["flag"] = c.diagonals ? 1 : 0; break;
                case ConditionSlotLocate c:
                {
                    SlotZoneMask m = SlotZoneMask.None;
                    if (c.Inside) m |= SlotZoneMask.Inside;
                    if (c.Outside) m |= SlotZoneMask.Outside;
                    if (c.Neutral) m |= SlotZoneMask.Neutral;
                    row["flag"] = (int)m;
                    break;
                }
                case ConditionSlotNeighbor c: row["range"] = c.range; break;
                case ConditionSlotPid c:
                {
                    SlotSideMask m = SlotSideMask.None;
                    if (c.player) m |= SlotSideMask.Player;
                    if (c.opponent) m |= SlotSideMask.Opponent;
                    if (c.neutral) m |= SlotSideMask.Neutral;
                    row["flag"] = (int)m;
                    break;
                }
                case ConditionSlotRange c: row["oper"] = Oper(c.oper); break;
                case ConditionSlotUnitEmpty c: row["oper"] = Oper(c.oper); break;
                case ConditionStat c: row["stat"] = c.type.ToString(); row["oper"] = Oper(c.oper); row["value"] = c.value; break;
                case ConditionStatCustom c: row["ref_traits"] = RefName(c.trait); row["oper"] = Oper(c.oper); row["value"] = c.value; break;
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
                "card_kinds", "CardType", "ref_clubs", "_ID_Club", "ref_traits", "_ID_Trait");
            foreach (var d in new[] { "Effect.xlsx", "CardData.xlsx", "Club.xlsx", "Trait.xlsx" }) t.refDeps.Add(d);

            DocRow(t, "First", "value", "amount");
            DocRow(t, "Random", "value", "amount", "flag", "rest");
            DocRow(t, "RandomCount", "pile", "pile", "scope", "player_self", "card_kinds", "has_type", "ref_clubs", "has_club", "ref_traits", "has_trait");
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
                        row["card_kinds"] = CardKind(f.has_type); row["ref_clubs"] = RefName(f.has_club); row["ref_traits"] = RefName(f.has_trait); break;
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
            var t = NewDDTable("Sort", "id", "!Id", "type", "SortType", "flag", "!Int", "ref_traits", "_ID_Trait");
            t.refDeps.Add("Trait.xlsx");
            DocRow(t, "Trait", "flag", "descending", "ref_traits", "trait");

            var idUsed = new HashSet<string>(StringComparer.Ordinal);
            foreach (var a in LoadSorted<SortData>())
            {
                string type = MemberName(a.GetType(), "Sort");
                var row = new Dictionary<string, object> { ["type"] = type, ["flag"] = a.descending ? 1 : 0 };
                switch (a)
                {
                    case SortTrait s: row["ref_traits"] = RefName(s.trait); break;
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
                "pile", "PileType", "card_kinds", "CardType", "ref_clubs", "_ID_Club", "ref_traits", "_ID_Trait");
            foreach (var d in new[] { "Effect.xlsx", "CardData.xlsx", "Club.xlsx", "Trait.xlsx" }) t.refDeps.Add(d);

            DocRow(t, "StaticValue", "value", "value");
            DocRow(t, "CountType", "pile", "pile", "scope", "player", "card_kinds", "has_type", "ref_clubs", "has_club", "ref_traits", "has_trait");

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
                        row["card_kinds"] = CardKind(r.has_type); row["ref_clubs"] = RefName(r.has_club); row["ref_traits"] = RefName(r.has_trait); break;
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

        static string DescribeSlot(DDSlot sl)
        {
            Type core; DDCategorize(sl.f, out core);
            if (sl.c == DDCat.Enum) return sl.f.Name + " (" + NormEnum(sl.f.FieldType).Name + ", enum)";
            if (sl.c == DDCat.EnumStr) return sl.f.Name + " (" + NormEnum(sl.f.FieldType).Name + ", 이름)";
            if (sl.c == DDCat.EnumList) return sl.f.Name + " (" + core.Name + "[], 이름)";
            if (sl.c == DDCat.StatusRef) return sl.f.Name + " (Status)";
            if (sl.c == DDCat.Ref) return sl.f.Name + " (" + TableNameOf(core) + ")";
            if (sl.c == DDCat.RefList) return sl.f.Name + " (" + TableNameOf(core) + "[])";
            return sl.f.Name;
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

        static object RemapEnum(Type ft, object val)
        {
            if (val == null) return null;
            if (ft == typeof(ConditionOperatorBool))
                return ((ConditionOperatorBool)val) == ConditionOperatorBool.IsTrue ? "Equal" : "NotEqual";
            return val.ToString();
        }

        static object ScaleInt(object val)
        {
            if (val is float fv) return (int)System.Math.Round(fv * 10000.0);
            if (val is double dv) return (int)System.Math.Round(dv * 10000.0);
            return val;   // int / bool (bool -> 0/1 in BuildDataSheet)
        }

        static string Sanitize(string s)
        {
            if (string.IsNullOrEmpty(s)) return "x";
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (char ch in s) sb.Append(char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_');
            return sb.ToString();
        }

        string JoinWeighted(object val)
        {
            var wlist = val as List<EffectCreateCard.WeightedCard>;
            if (wlist == null) return string.Empty;
            var parts = new List<string>(wlist.Count);
            foreach (var w in wlist)
            {
                string cn = RefName(w.card);
                if (!string.IsNullOrEmpty(cn)) parts.Add(cn + "=" + w.weight);
            }
            return string.Join(";", parts);
        }

        string JoinDirections(object val)
        {
            var dirs = val as List<Direction>;
            if (dirs == null) return string.Empty;
            return string.Join(";", dirs.Where(d => d != null).Select(StructToString));
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
            { "flag",           "참거짓(0/1) 또는 비트합(1·2·4). 대각선 포함·최대치 비교·나머지 선택·내림차순·진영/구역 마스크" },
            { "scope",          "대상 플레이어. 자신·상대·양쪽" },
            { "stat",           "스탯 종류. 공격·체력·마나 또는 보스 게이지(스킬·ATG·그로기)" },
            { "pile",           "카드 더미. 덱·손·필드·장착·무덤·비밀·임시 등" },
            { "pile_pos",       "더미 내 위치. 맨위·맨아래·지정 인덱스" },
            { "last_type",      "마지막 행동 종류. 마지막 선택·지정·소환·공격·파괴·플레이" },
            { "target_kind",    "대상 분류. 카드·플레이어·슬롯" },
            { "card_kinds",     "카드 종류 목록. 세미콜론 구분, 하나라도 일치하면 매칭 (빈 칸=필터 없음)" },
            { "ref_cards",      "참조 카드 목록. 카드 식별자, 세미콜론 구분" },
            { "ref_clubs",      "참조 클럽 목록. 클럽 식별자, 세미콜론 구분" },
            { "ref_traits",     "참조 특성 목록. 특성 식별자, 세미콜론 구분" },
            { "ref_status",     "참조 상태이상. 상태 식별자" },
            { "sub_conditions", "하위 조건 목록. 조건 식별자, 세미콜론 구분 (하나라도 충족 시 참)" },
            { "dx",             "가로 오프셋 목록. 세미콜론 구분, 세로와 순서 정렬" },
            { "dy",             "세로 오프셋 목록. 세미콜론 구분, 가로와 순서 정렬" },
            { "thumbnail",      "범위 미리보기 스프라이트. 에셋 경로 문자열" },
        };

        static string SlotColDesc(string col)
        {
            if (COL_DESC_KO.TryGetValue(col, out var ko)) return ko;
            // Effect 테이블의 풀링 컬럼(str/ref/int)과 특수 인코딩 컬럼.
            if (col == "create_card") return "가중치 카드 목록: cardId=weight;...";
            if (col == "directions") return "방향 목록: dx=..,dy=..;...";
            if (col.StartsWith("str")) return "enum 멤버명 문자열 슬롯 — 타입별로 다른 enum ([type별 슬롯 의미] 참조)";
            if (col.StartsWith("ref")) return "참조 슬롯 — 타입별로 가리키는 테이블이 다름 ([type별 슬롯 의미] 참조)";
            if (col.StartsWith("int")) return "정수 슬롯 — 타입별 의미 다름. float은 ×10000, bool은 0/1 ([type별 슬롯 의미] 참조)";
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
            { "Condition.CardType",      "타깃 카드가 지정 타입·클럽·특성 중 하나라도 해당하는지 검사 (각 목록은 OR)" },
            { "Condition.CardData",      "타깃 카드가 지정 카드 목록에 속하는지(card_id 일치) 검사" },
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
            { "Condition.Count",         "지정 플레이어·더미의 (조건 일치) 카드 수를 세어 value와 oper로 비교" },
            { "Condition.CardPile",      "타깃 카드가 지정 더미(덱·손·필드 등)에 있는지 검사" },
            { "Condition.PilePosition",  "타깃 카드가 더미의 지정 위치(맨위·맨아래·인덱스)에 있는지 검사" },
            { "Condition.CanPlace",      "지정 카드를 타깃 슬롯에 배치할 수 있는지 검사" },
            { "Condition.SlotDist",      "타깃 슬롯이 시전자로부터 range 이동거리 내인지 검사 (대각선 포함=flag)" },
            { "Condition.SlotRange",     "타깃 슬롯이 시전자 사거리 내 이웃인지 검사" },
            { "Condition.SlotNeighbor",  "타깃 슬롯이 시전자 기준 range 이웃 범위인지 검사" },
            { "Condition.SlotPid",       "타깃 슬롯의 진영이 flag 비트합(1=아군·2=적군·4=중립)에 포함되는지 검사" },
            { "Condition.SlotLocate",    "타깃 슬롯의 구역이 flag 비트합(1=안쪽·2=바깥·4=중립)에 포함되는지 검사" },
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
            "text", "desc",
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
