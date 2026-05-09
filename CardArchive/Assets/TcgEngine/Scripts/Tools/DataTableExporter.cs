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
    // Plays the scene -> writes one .xlsx per ScriptableObject type
    // (CardData, AbilityData, EffectData, etc.) under <project>/Tools/DataExport/.
    //
    // Each .xlsx contains 3 sheets:
    //   1. References — list of other .xlsx files this table refers to
    //   2. Enums      — enum definitions (each block: `_ID_Name` header, then
    //                   value/int rows, separated by blank rows). Each enum is
    //                   declared in exactly one "owner" table.
    //   3. {Table}    — actual data:
    //                     row 1: column names (id always first)
    //                     row 2: type row (`_ID_<Sheet>` for id, `_ID_<Other>`
    //                            for table refs, `_ID_<Enum>` for enums,
    //                            `int`/`string`/`bool`/`float` for primitives,
    //                            `path` for external assets)
    //                     row 3+: data
    //
    //   `!EndTable` markers at top-right (row 1, col N+1) and bottom-left
    //   (row M+1, col 1) of every sheet — so parsers can detect end-of-cols
    //   and end-of-rows.
    [DisallowMultipleComponent]
    public class DataTableExporter : MonoBehaviour
    {
        const string EndTable = "!EndTable";

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
                total += Export<EffectData>(outDir, "Effect",   BuildSparseRow,  EffectFixedColumns);
                total += Export<WeaponData>(outDir, "Weapon",   BuildWeaponRow,  WeaponColumns);
                total += Export<TraitData>(outDir, "Trait",     BuildSimpleRow,  null);
                total += Export<StatusData>(outDir, "Status",   BuildSimpleRow,  null);
                total += Export<ConditionData>(outDir, "Condition", BuildSparseRow, ConditionFixedColumns);
                total += Export<PackData>(outDir, "Pack",       BuildSimpleRow,  null);
                total += Export<RarityData>(outDir, "Rarity",   BuildSimpleRow,  null);
                total += Export<VariantData>(outDir, "Variant", BuildSimpleRow,  null);
                total += Export<CardbackData>(outDir, "Cardback", BuildSimpleRow, null);
                total += Export<AvatarData>(outDir, "Avatar",   BuildSimpleRow,  null);
                total += Export<DeckData>(outDir, "Deck",       BuildSimpleRow,  null);
                total += Export<LevelData>(outDir, "Level",     BuildSimpleRow,  null);
            }
            catch (Exception e)
            {
                Debug.LogError("[DataTableExporter] FAILED: " + e);
                return;
            }

            float dt = Time.realtimeSinceStartup - t0;
            Debug.Log(string.Format(
                "<b>[DataTableExporter] Wrote {0} rows across 16 files in {1:F2}s.</b>\nOutput: {2}",
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
            var dataSheet = BuildDataSheet(fileBase, cols, typeRow, dataRows);

            string outPath = Path.Combine(outDir, fileBase + ".xlsx");
            SimpleXlsx.Write(outPath, new[] { refSheet, enumSheet, dataSheet });
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
            { "Effect",    new[] { "Trait.xlsx", "Club.xlsx", "Ability.xlsx", "CardData.xlsx", "Weapon.xlsx", "Status.xlsx" } },
            { "Weapon",    new string[0] },
            { "Trait",     new string[0] },
            { "Status",    new string[0] },
            { "Condition", new[] { "Trait.xlsx", "CardData.xlsx", "Club.xlsx", "Status.xlsx" } },
            { "Pack",      new[] { "Rarity.xlsx", "Variant.xlsx" } },
            { "Rarity",    new string[0] },
            { "Variant",   new string[0] },
            { "Cardback",  new string[0] },
            { "Avatar",    new string[0] },
            { "Deck",      new[] { "CardData.xlsx" } },
            { "Level",     new[] { "Deck.xlsx", "Pack.xlsx", "CardData.xlsx" } },
        };

        SimpleXlsx.Sheet BuildReferencesSheet(string table)
        {
            var rows = new List<IList<object>>();
            // Row 1: column header + top-right end marker
            rows.Add(new List<object> { "File", EndTable });
            // Row 2: type row
            rows.Add(new List<object> { "string" });

            string[] refs;
            if (REFERENCES.TryGetValue(table, out refs))
            {
                foreach (var r in refs)
                    rows.Add(new List<object> { r });
            }
            // Bottom-left end marker
            rows.Add(new List<object> { EndTable });

            return new SimpleXlsx.Sheet { Name = "References", Rows = rows, FreezeRows = 2 };
        }

        // ------------------------------------------------------------------
        // Sheet 2: Enums (deduped — each enum has exactly one owner table)
        // ------------------------------------------------------------------
        static readonly Dictionary<string, Type[]> ENUM_OWNER = new Dictionary<string, Type[]>
        {
            { "CardData",  new[] { typeof(CardType) } },
            { "Ability",   new[] { typeof(AbilityTrigger), typeof(AbilityTarget) } },
            { "Effect",    new[] {
                typeof(EffectStatType), typeof(EffectTotalCountType),
                typeof(EffectDamageType), typeof(EffectValueType),
                typeof(PileType), typeof(EffectStatusType),
                typeof(EffectActionType), typeof(EffectPlayerType),
            } },
            { "Weapon",    new[] { typeof(WeaponType) } },
            { "Status",    new[] { typeof(StatusType) } },
            { "Condition", new[] {
                typeof(ConditionOperatorBool), typeof(ConditionOperatorInt),
                typeof(ConditionStatType), typeof(ConditionPlayerType),
                typeof(ConditionLastType), typeof(ConditionTargetType),
                typeof(FilterPlayerType),
            } },
            { "Pack",      new[] { typeof(PackType) } },
            { "Level",     new[] { typeof(LevelFirst) } },
        };

        SimpleXlsx.Sheet BuildEnumsSheet(string table)
        {
            var rows = new List<IList<object>>();
            Type[] enumTypes;
            bool first = true;
            bool topMarkerWritten = false;

            if (ENUM_OWNER.TryGetValue(table, out enumTypes))
            {
                foreach (var t in enumTypes)
                {
                    if (!first) rows.Add(new List<object>());  // blank separator row
                    first = false;

                    // First row of each block: `_ID_<EnumName>`.  The very
                    // first block also gets the top-right `!EndTable` marker.
                    var headerRow = new List<object> { "_ID_" + t.Name };
                    if (!topMarkerWritten)
                    {
                        headerRow.Add(null);     // value column placeholder
                        headerRow.Add(EndTable); // top-right marker
                        topMarkerWritten = true;
                    }
                    rows.Add(headerRow);

                    foreach (var v in Enum.GetValues(t))
                    {
                        rows.Add(new List<object> { v.ToString(), Convert.ToInt32(v) });
                    }
                }
            }

            // Bottom-left end-of-rows marker
            rows.Add(new List<object> { EndTable });

            return new SimpleXlsx.Sheet { Name = "Enums", Rows = rows, FreezeRows = 0 };
        }

        // ------------------------------------------------------------------
        // Sheet 3: Data
        //   row 1: column names + top-right `!EndTable`
        //   row 2: types        (`_ID_<Sheet>` for id, etc.)
        //   row 3+: data
        //   row M+1: bottom-left `!EndTable`
        // ------------------------------------------------------------------
        SimpleXlsx.Sheet BuildDataSheet(string fileBase, List<string> cols, List<string> typeRow,
                                        List<IDictionary<string, object>> dataRows)
        {
            var rows = new List<IList<object>>(dataRows.Count + 4);

            // Row 1: header + top-right marker
            var header = new List<object>(cols.Count + 1);
            foreach (var c in cols) header.Add(c);
            header.Add(EndTable);
            rows.Add(header);

            // Row 2: type row (no marker)
            var types = new List<object>(typeRow.Count);
            foreach (var t in typeRow) types.Add(t);
            rows.Add(types);

            // Data rows
            foreach (var dr in dataRows)
            {
                var row = new List<object>(cols.Count);
                foreach (var c in cols)
                {
                    object v;
                    dr.TryGetValue(c, out v);
                    row.Add(v);
                }
                rows.Add(row);
            }

            // Bottom-left end-of-rows marker
            rows.Add(new List<object> { EndTable });

            return new SimpleXlsx.Sheet { Name = fileBase, Rows = rows, FreezeRows = 2 };
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
            { typeof(ConditionData), "Condition" },
            { typeof(FilterData),   "Condition" },
            { typeof(RepeatConditionData), "Condition" },
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
            if (column == "id") return "_ID_" + sheetName;
            if (column == "effect_class" || column == "condition_class") return "string";

            FieldInfo f;
            if (!fieldByCol.TryGetValue(column, out f)) return "string";
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
            if (t.IsEnum) s = "_ID_" + t.Name;
            else if (t == typeof(string)) s = "string";
            else if (t == typeof(int) || t == typeof(long) || t == typeof(short) || t == typeof(byte)
                     || t == typeof(uint) || t == typeof(ulong) || t == typeof(ushort) || t == typeof(sbyte))
                s = "int";
            else if (t == typeof(float) || t == typeof(double)) s = "float";
            else if (t == typeof(bool)) s = "bool";
            else if (typeof(ScriptableObject).IsAssignableFrom(t)) s = "_ID_" + TableNameOf(t);
            else if (typeof(UnityEngine.Object).IsAssignableFrom(t)) s = "path";
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

        IDictionary<string, object> BuildSparseRow<T>(T asset) where T : ScriptableObject
        {
            var row = new Dictionary<string, object>();
            row["id"] = asset.name;
            if (asset is EffectData) row["effect_class"] = asset.GetType().Name;
            else if (asset is ConditionData) row["condition_class"] = asset.GetType().Name;
            DumpAllFields(asset, row);
            return row;
        }

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
            "condition_wide_range", "condition_target", "filters_target",
            "condition_repeat",
            "effects", "status",
            "value", "duration", "can_cancel",
            "chain_abilities", "mana_cost", "exhaust", "charge_target",
            "board_fx", "caster_fx", "target_fx",
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

        static readonly string[] EffectFixedColumns    = { "id", "effect_class" };
        static readonly string[] ConditionFixedColumns = { "id", "condition_class" };

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
