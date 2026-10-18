using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace TcgEngine.Replay
{
    // Fixed-depth entity schema: Game/Player/Card -> scalar, ID, or a list of leaf values.
    // No object-tree walk, recursive calls, nested entities, or Unity asset serialization.
    // Binary entity bodies preserve nulls and readonly context fields that JsonUtility cannot.
    // The outer snapshot/delta remains shallow JSON for transport and replay storage.
    public static class ReplayStateCodec
    {
        public const int FormatVersion = 3; //3: Card/Player marker lists, CardTrait without value
        const int MaxCollectionCount = 1000000;
        static readonly Dictionary<Type, FieldInfo[]> fields = new Dictionary<Type, FieldInfo[]>();
        static readonly HashSet<Type> leaves = new HashSet<Type> {
            typeof(GameSettings), typeof(BossState), typeof(DeathEventContext),
            typeof(CardTrait), typeof(CardMarker), typeof(CardStatus), typeof(CardClub), typeof(CardWeapon), typeof(ActionHistory)
        };

        sealed class References
        {
            public readonly Dictionary<string, Card> cards = new Dictionary<string, Card>(StringComparer.Ordinal);
            public readonly Dictionary<int, Player> players = new Dictionary<int, Player>();
        }

        static FieldInfo[] Fields(Type type)
        {
            if (!fields.TryGetValue(type, out var result))
                fields[type] = result = type.GetFields(BindingFlags.Public | BindingFlags.Instance)
                    .Where(f => !f.IsNotSerialized).OrderBy(f => f.Name, StringComparer.Ordinal).ToArray();
            return result;
        }

        static bool Atom(Type t) => t.IsPrimitive || t.IsEnum || t == typeof(string)
            || t == typeof(decimal) || t == typeof(Slot);
        static bool Collection(Type t) => t.IsArray || t.IsGenericType
            && (t.GetGenericTypeDefinition() == typeof(List<>) || t.GetGenericTypeDefinition() == typeof(HashSet<>));
        static Type Element(Type t) => t.IsArray ? t.GetElementType() : t.GetGenericArguments()[0];
        static string Number(object value) => Convert.ToString(value, CultureInfo.InvariantCulture);
        static InvalidDataException Invalid(string message) => new InvalidDataException("Replay state: " + message);

        public static ReplayState Capture(Game game)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            var refs = new References();
            var state = new ReplayState();
            state.entities.Add(Encode(ReplayEntityKind.Game, "game", game, typeof(Game), refs));
            // Serializing the root registers players and attack-list cards; player records
            // register every remaining card. No registration traverses an entity's fields.
            foreach (var pair in refs.players.OrderBy(p => p.Key))
                state.entities.Add(Encode(ReplayEntityKind.Player, Number(pair.Key), pair.Value, typeof(Player), refs));
            foreach (var pair in refs.cards.OrderBy(p => p.Key, StringComparer.Ordinal))
                state.entities.Add(Encode(ReplayEntityKind.Card, pair.Key, pair.Value, typeof(Card), refs));
            state.entities = Ordered(state.entities);
            return state;
        }

        static ReplayEntity Encode(ReplayEntityKind kind, string id, object value, Type type, References refs)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(FormatVersion);
                var schema = Fields(type);
                writer.Write(schema.Length);
                foreach (var field in schema)
                {
                    writer.Write(field.Name);
                    WriteValue(writer, field.GetValue(value), field.FieldType, refs);
                }
                writer.Flush();
                return new ReplayEntity { kind = kind, id = id, data = Convert.ToBase64String(stream.ToArray()) };
            }
        }

        static void WriteText(BinaryWriter writer, string text)
        {
            writer.Write(text != null);
            if (text != null) writer.Write(text);
        }
        static string ReadText(BinaryReader reader) => reader.ReadBoolean() ? reader.ReadString() : null;

        static void WriteAtom(BinaryWriter writer, object value, Type type)
        {
            if (type == typeof(Slot))
            {
                var slot = (Slot)value;
                writer.Write(slot.x); writer.Write(slot.y); writer.Write(slot.p);
            }
            else if (type == typeof(string)) WriteText(writer, (string)value);
            else writer.Write(type.IsEnum ? Convert.ToInt64(value).ToString(CultureInfo.InvariantCulture)
                : value is float f ? f.ToString("R", CultureInfo.InvariantCulture)
                : value is double d ? d.ToString("R", CultureInfo.InvariantCulture) : Number(value));
        }

        static object ReadAtom(BinaryReader reader, Type type)
        {
            if (type == typeof(Slot)) return new Slot { x = reader.ReadInt32(), y = reader.ReadInt32(), p = reader.ReadInt32() };
            if (type == typeof(string)) return ReadText(reader);
            string value = reader.ReadString();
            return type.IsEnum ? Enum.Parse(type, value) : Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
        }

        static void WriteCard(BinaryWriter writer, Card card, References refs)
        {
            WriteText(writer, card?.uid);
            if (card == null) return;
            if (string.IsNullOrEmpty(card.uid)) throw Invalid("card UID is missing");
            if (refs.cards.TryGetValue(card.uid, out var previous) && !ReferenceEquals(previous, card))
                throw Invalid("different card instances share UID " + card.uid);
            refs.cards[card.uid] = card;
        }
        static Card ReadCard(BinaryReader reader, References refs)
        {
            string uid = ReadText(reader);
            if (uid == null) return null;
            return refs.cards.TryGetValue(uid, out var card) ? card : throw Invalid("missing card " + uid);
        }

        static void WriteValue(BinaryWriter writer, object value, Type type, References refs)
        {
            if (Atom(type)) { WriteAtom(writer, value, type); return; }
            if (type == typeof(Card)) { WriteCard(writer, (Card)value, refs); return; }
            if (type == typeof(AbilityEventContext))
            {
                if (value is AbilityExecutionContext execution)
                {
                    writer.Write((byte)2);
                    foreach (var field in Fields(typeof(AbilityExecutionContext)))
                    {
                        if (field.Name == nameof(AbilityExecutionContext.source_event))
                            WriteLeaf(writer, execution.source_event, typeof(DeathEventContext));
                        else
                            WriteValue(writer, field.GetValue(execution), field.FieldType, refs);
                    }
                }
                else
                {
                    if (value != null && !(value is DeathEventContext)) throw Invalid("unregistered ability context");
                    WriteLeaf(writer, value, typeof(DeathEventContext));
                }
                return;
            }
            if (type == typeof(Dictionary<string, Card>))
            {
                writer.Write(value != null);
                if (value == null) return;
                var dictionary = (Dictionary<string, Card>)value;
                writer.Write(dictionary.Count);
                foreach (var pair in dictionary.OrderBy(p => p.Key, StringComparer.Ordinal))
                {
                    writer.Write(pair.Key);
                    WriteCard(writer, pair.Value, refs);
                }
                return;
            }
            if (Collection(type))
            {
                writer.Write(value != null);
                if (value == null) return;
                var items = ((IEnumerable)value).Cast<object>();
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(HashSet<>))
                    items = items.OrderBy(item => item == null ? 0 : 1).ThenBy(Number, StringComparer.Ordinal);
                var list = items.ToArray();
                writer.Write(list.Length);
                Type element = Element(type);
                foreach (object item in list)
                {
                    if (element == typeof(Card)) WriteCard(writer, (Card)item, refs);
                    else if (element == typeof(Player))
                    {
                        var player = (Player)item;
                        writer.Write(player != null);
                        if (player == null) continue;
                        if (refs.players.TryGetValue(player.player_id, out var previous) && !ReferenceEquals(previous, player))
                            throw Invalid("duplicate player ID");
                        refs.players[player.player_id] = player;
                        writer.Write(player.player_id);
                    }
                    else if (Atom(element)) WriteAtom(writer, item, element);
                    else WriteLeaf(writer, item, element);
                }
                return;
            }
            WriteLeaf(writer, value, type);
        }

        // Leaves are explicitly registered and may contain only scalar/Slot fields.
        // An added nested object fails here instead of silently introducing recursion.
        static void WriteLeaf(BinaryWriter writer, object value, Type type)
        {
            if (!leaves.Contains(type)) throw Invalid("unregistered leaf " + type.Name);
            writer.Write(value != null);
            if (value == null) return;
            foreach (var field in Fields(type))
            {
                if (!Atom(field.FieldType)) throw Invalid("non-scalar leaf field " + type.Name + "." + field.Name);
                WriteAtom(writer, field.GetValue(value), field.FieldType);
            }
        }

        static object ReadLeaf(BinaryReader reader, Type type)
        {
            if (!leaves.Contains(type)) throw Invalid("unregistered leaf " + type.Name);
            if (!reader.ReadBoolean()) return null;
            // Some immutable data wrappers lack a default constructor; restoring their public
            // fields must not load assets or run gameplay code.
            object value = type.GetConstructor(Type.EmptyTypes) != null ? Activator.CreateInstance(type)
                : System.Runtime.Serialization.FormatterServices.GetUninitializedObject(type);
            foreach (var field in Fields(type))
            {
                if (!Atom(field.FieldType)) throw Invalid("non-scalar leaf field " + type.Name + "." + field.Name);
                field.SetValue(value, ReadAtom(reader, field.FieldType));
            }
            return value;
        }

        static int ReadCount(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            if (count < 0 || count > MaxCollectionCount) throw Invalid("invalid collection count");
            return count;
        }

        static object ReadValue(BinaryReader reader, Type type, References refs)
        {
            if (Atom(type)) return ReadAtom(reader, type);
            if (type == typeof(Card)) return ReadCard(reader, refs);
            if (type == typeof(AbilityEventContext))
            {
                byte kind = reader.ReadByte();
                if (kind == 0) return null;
                Type contextType = kind == 1 ? typeof(DeathEventContext)
                    : kind == 2 ? typeof(AbilityExecutionContext) : throw Invalid("unknown ability context");
                object context = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(contextType);
                foreach (var field in Fields(contextType))
                {
                    object fieldValue = field.Name == nameof(AbilityExecutionContext.source_event)
                        ? ReadLeaf(reader, typeof(DeathEventContext)) : ReadValue(reader, field.FieldType, refs);
                    field.SetValue(context, fieldValue);
                }
                return context;
            }
            if (type == typeof(Dictionary<string, Card>))
            {
                if (!reader.ReadBoolean()) return null;
                int count = ReadCount(reader);
                var dictionary = new Dictionary<string, Card>(StringComparer.Ordinal);
                for (int i = 0; i < count; i++) dictionary.Add(reader.ReadString(), ReadCard(reader, refs));
                return dictionary;
            }
            if (Collection(type))
            {
                if (!reader.ReadBoolean()) return null;
                int count = ReadCount(reader);
                Type element = Element(type);
                object collection = type.IsArray ? Array.CreateInstance(element, count) : Activator.CreateInstance(type);
                MethodInfo add = type.IsArray ? null : type.GetMethod("Add");
                for (int i = 0; i < count; i++)
                {
                    object item;
                    if (element == typeof(Card)) item = ReadCard(reader, refs);
                    else if (element == typeof(Player))
                    {
                        item = null;
                        if (reader.ReadBoolean())
                        {
                            int id = reader.ReadInt32();
                            item = refs.players.TryGetValue(id, out var player) ? player : throw Invalid("missing player " + id);
                        }
                    }
                    else item = Atom(element) ? ReadAtom(reader, element) : ReadLeaf(reader, element);
                    if (type.IsArray) ((Array)collection).SetValue(item, i);
                    else add.Invoke(collection, new[] { item });
                }
                return collection;
            }
            return ReadLeaf(reader, type);
        }

        public static Game Restore(ReplayState state)
        {
            var index = Index(state);
            var refs = new References();
            var game = new Game();
            // Allocate all entities first, then connect their IDs. This preserves identity
            // inside this model without sharing any mutable instances with another model.
            foreach (var entity in index.Values)
            {
                if (entity.kind == ReplayEntityKind.Card) refs.cards.Add(entity.id, new Card("", entity.id, 0));
                else if (entity.kind == ReplayEntityKind.Player)
                {
                    int id = int.Parse(entity.id, CultureInfo.InvariantCulture);
                    refs.players.Add(id, new Player(id));
                }
            }
            foreach (var entity in index.Values)
            {
                object target = entity.kind == ReplayEntityKind.Game ? (object)game
                    : entity.kind == ReplayEntityKind.Player ? refs.players[int.Parse(entity.id, CultureInfo.InvariantCulture)] : refs.cards[entity.id];
                Decode(entity, target, refs);
                if (target is Card card && card.uid != entity.id) throw Invalid("card ID mismatch");
                if (target is Player player && Number(player.player_id) != entity.id) throw Invalid("player ID mismatch");
            }
            return game;
        }

        static void Decode(ReplayEntity entity, object target, References refs)
        {
            using (var stream = new MemoryStream(Convert.FromBase64String(entity.data)))
            using (var reader = new BinaryReader(stream, Encoding.UTF8))
            {
                if (reader.ReadInt32() != FormatVersion) throw Invalid("unsupported entity format");
                var schema = Fields(target.GetType());
                if (ReadCount(reader) != schema.Length) throw Invalid("entity schema differs from this build");
                foreach (var field in schema)
                {
                    if (reader.ReadString() != field.Name) throw Invalid("entity field differs from this build");
                    field.SetValue(target, ReadValue(reader, field.FieldType, refs));
                }
                if (stream.Position != stream.Length) throw Invalid("unexpected entity data");
            }
        }

        static string Key(ReplayEntity entity)
        {
            if (entity == null || string.IsNullOrEmpty(entity.id) || !Enum.IsDefined(typeof(ReplayEntityKind), entity.kind))
                throw Invalid("invalid entity key");
            if (entity.kind == ReplayEntityKind.Game && entity.id != "game") throw Invalid("invalid game key");
            return ((int)entity.kind).ToString(CultureInfo.InvariantCulture) + ":" + entity.id;
        }
        static List<ReplayEntity> Ordered(IEnumerable<ReplayEntity> entities) => entities.OrderBy(e => e.kind)
            .ThenBy(e => e.id, StringComparer.Ordinal).ToList();
        static ReplayEntity CopyEntity(ReplayEntity entity) => new ReplayEntity { kind = entity.kind, id = entity.id, data = entity.data };
        static Dictionary<string, ReplayEntity> Index(ReplayState state)
        {
            if (state == null || state.formatVersion != FormatVersion || state.entities == null) throw Invalid("unsupported state format");
            var index = new Dictionary<string, ReplayEntity>(StringComparer.Ordinal);
            foreach (var entity in state.entities)
            {
                if (entity == null || entity.data == null) throw Invalid("missing entity body");
                string key = Key(entity);
                if (index.ContainsKey(key)) throw Invalid("duplicate entity " + key);
                index.Add(key, entity);
            }
            if (!index.ContainsKey("0:game")) throw Invalid("missing game record");
            return index;
        }

        public static List<ReplayChange> Diff(ReplayState before, ReplayState after)
        {
            var old = Index(before);
            var next = Index(after);
            var changes = new List<ReplayChange>();
            foreach (var entity in Ordered(next.Values))
                if (!old.TryGetValue(Key(entity), out var previous) || previous.data != entity.data)
                    changes.Add(new ReplayChange { entity = CopyEntity(entity) });
            foreach (var entity in Ordered(old.Values))
                if (!next.ContainsKey(Key(entity))) changes.Add(new ReplayChange {
                    removed = true, entity = new ReplayEntity { kind = entity.kind, id = entity.id } });
            return changes;
        }

        public static ReplayState Apply(ReplayState state, IEnumerable<ReplayChange> changes)
        {
            var index = Index(state);
            foreach (var change in changes)
            {
                string key = Key(change.entity);
                if (change.removed)
                {
                    if (!index.Remove(key)) throw Invalid("removing missing entity " + key);
                }
                else
                {
                    if (change.entity.data == null) throw Invalid("missing change body");
                    index[key] = CopyEntity(change.entity);
                }
            }
            if (!index.ContainsKey("0:game")) throw Invalid("removing game record");
            state.entities = Ordered(index.Values);
            return state;
        }

        public static ReplayState Copy(ReplayState state)
        {
            if (state == null) return null;
            return new ReplayState { entities = Ordered(Index(state).Values).Select(CopyEntity).ToList() };
        }

        public static string Hash(ReplayState state)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            using (var sha = SHA256.Create())
            {
                writer.Write(FormatVersion);
                foreach (var entity in Ordered(Index(state).Values))
                {
                    writer.Write((int)entity.kind); writer.Write(entity.id); writer.Write(entity.data);
                }
                writer.Flush();
                stream.Position = 0;
                return Convert.ToBase64String(sha.ComputeHash(stream));
            }
        }
    }
}
