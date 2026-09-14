using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace TcgEngine.Replay
{
    // Schema-directed public-field codec: no runtime type names, Unity assets, delegates,
    // private caches or BinaryFormatter. Lists retain order; dictionaries have stable keys.
    public static class ReplayStateCodec
    {
        static readonly Dictionary<Type, FieldInfo[]> fields = new Dictionary<Type, FieldInfo[]>();
        static FieldInfo[] Fields(Type type)
        {
            if (!fields.TryGetValue(type, out var result))
                fields[type] = result = type.GetFields(BindingFlags.Public | BindingFlags.Instance)
                    .Where(f => !f.IsNotSerialized && !typeof(UnityEngine.Object).IsAssignableFrom(f.FieldType)
                        && !typeof(Delegate).IsAssignableFrom(f.FieldType))
                    .OrderBy(f => f.Name, StringComparer.Ordinal).ToArray();
            return result;
        }
        static bool Scalar(Type t) => t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(decimal);
        public static ReplayNode Capture(Game game) => Write(game, "root");
        static ReplayNode Write(object obj, string key)
        {
            var n = new ReplayNode { key = key, isNull = obj == null };
            if (obj == null) return n;
            Type t = obj.GetType();
            if (Scalar(t)) n.value = t.IsEnum ? Convert.ToInt64(obj).ToString(CultureInfo.InvariantCulture)
                : obj is float f ? f.ToString("R", CultureInfo.InvariantCulture)
                : obj is double d ? d.ToString("R", CultureInfo.InvariantCulture)
                : Convert.ToString(obj, CultureInfo.InvariantCulture);
            else if (obj is IDictionary dict)
            {
                foreach (object k in dict.Keys.Cast<object>().OrderBy(k => Convert.ToString(k, CultureInfo.InvariantCulture), StringComparer.Ordinal))
                    n.children.Add(Write(dict[k], Convert.ToString(k, CultureInfo.InvariantCulture)));
            }
            else if (obj is IEnumerable list)
            {
                var values = list.Cast<object>();
                if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(HashSet<>))
                    values = values.OrderBy(v => Convert.ToString(v, CultureInfo.InvariantCulture), StringComparer.Ordinal);
                int i = 0;
                foreach (var v in values) n.children.Add(Write(v, (i++).ToString()));
            }
            else foreach (var f in Fields(t)) n.children.Add(Write(f.GetValue(obj), f.Name));
            return n;
        }
        public static Game Restore(ReplayNode node)
        {
            return (Game)Read(node, typeof(Game), new Dictionary<string, Card>());
        }
        static object Parse(string value, Type t) => t == typeof(string) ? value
            : t.IsEnum ? Enum.Parse(t, value)
            : Convert.ChangeType(value, t, CultureInfo.InvariantCulture);
        static object Read(ReplayNode n, Type t, Dictionary<string, Card> cards)
        {
            if (n.isNull) return null;
            if (Scalar(t)) return Parse(n.value, t);
            if (t.IsArray)
            {
                var a = Array.CreateInstance(t.GetElementType(), n.children.Count);
                for (int i = 0; i < a.Length; i++) a.SetValue(Read(n.children[i], t.GetElementType(), cards), i);
                return a;
            }
            if (typeof(IDictionary).IsAssignableFrom(t))
            {
                var d = (IDictionary)Activator.CreateInstance(t);
                var args = t.GetGenericArguments();
                foreach (var c in n.children) d.Add(Parse(c.key, args[0]), Read(c, args[1], cards));
                return d;
            }
            if (t.IsGenericType && (t.GetGenericTypeDefinition() == typeof(List<>) || t.GetGenericTypeDefinition() == typeof(HashSet<>)))
            {
                var list = Activator.CreateInstance(t);
                var add = t.GetMethod("Add");
                foreach (var c in n.children) add.Invoke(list, new[] { Read(c, t.GetGenericArguments()[0], cards) });
                return list;
            }
            object obj;
            if (t == typeof(Card))
            {
                string uid = n.children.First(c => c.key == "uid").value;
                if (cards.TryGetValue(uid, out var card)) return card;
                obj = new Card("", uid, 0);
                cards.Add(uid, (Card)obj);
            }
            else if (t == typeof(Player)) obj = new Player(0);
            else obj = t.IsValueType || t.GetConstructor(Type.EmptyTypes) != null
                ? Activator.CreateInstance(t) : System.Runtime.Serialization.FormatterServices.GetUninitializedObject(t);
            foreach (var f in Fields(t))
            {
                var c = n.children.Find(v => v.key == f.Name);
                if (c != null) f.SetValue(obj, Read(c, f.FieldType, cards));
            }
            return obj;
        }
        public static List<ReplayChange> Diff(ReplayNode before, ReplayNode after)
        {
            var changes = new List<ReplayChange>();
            Diff(before, after, new List<string>(), changes);
            return changes;
        }
        static void Diff(ReplayNode a, ReplayNode b, List<string> path, List<ReplayChange> changes)
        {
            if (a.isNull != b.isNull || a.value != b.value || a.children.Count != b.children.Count
                || !a.children.Select(c => c.key).SequenceEqual(b.children.Select(c => c.key)))
            {
                changes.Add(new ReplayChange { path = path.ToArray(), value = b });
                return;
            }
            for (int i = 0; i < a.children.Count; i++)
            {
                path.Add(b.children[i].key);
                Diff(a.children[i], b.children[i], path, changes);
                path.RemoveAt(path.Count - 1);
            }
        }
        public static ReplayNode Apply(ReplayNode root, IEnumerable<ReplayChange> changes)
        {
            foreach (var c in changes)
            {
                var value = Copy(c.value); // Never mutate the stored recording on restart.
                if (c.path.Length == 0) { root = value; continue; }
                var parent = root;
                for (int i = 0; i < c.path.Length - 1; i++)
                    parent = parent.children.Find(n => n.key == c.path[i]) ?? throw new InvalidOperationException("Invalid replay path");
                int index = parent.children.FindIndex(n => n.key == c.path[c.path.Length - 1]);
                if (index < 0) throw new InvalidOperationException("Invalid replay field");
                parent.children[index] = value;
            }
            return root;
        }
        public static ReplayNode Copy(ReplayNode n) => JsonUtility.FromJson<ReplayNode>(JsonUtility.ToJson(n));
        public static string Hash(ReplayNode n)
        {
            using (var sha = SHA256.Create())
                return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(JsonUtility.ToJson(n))));
        }
    }
}
