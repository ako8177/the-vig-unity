// A canonical text form of sim data: JSON with keys sorted and nulls left out, numbers printed as JS
// prints them. The TS sim's twin (tools/golden/canon.ts) writes the same text from the same state, so
// the golden test can compare the two sims tick by tick; Game.Hash uses it too.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Vig.Sim
{
    public static class Canon
    {
        /// <summary>The key a field goes by when camelCase can't spell it (won$).</summary>
        [AttributeUsage(AttributeTargets.Field)]
        public sealed class NameAttribute : Attribute
        {
            public readonly string Key;
            public NameAttribute(string key) { Key = key; }
        }

        static readonly Dictionary<Type, (string key, FieldInfo f)[]> fields = new Dictionary<Type, (string, FieldInfo)[]>();

        static (string key, FieldInfo f)[] FieldsOf(Type t)
        {
            lock (fields)
            {
                if (fields.TryGetValue(t, out var fs)) return fs;
                fs = t.GetFields(BindingFlags.Public | BindingFlags.Instance)
                    .Select(f =>
                    {
                        var n = f.GetCustomAttribute<NameAttribute>();
                        string key = n != null ? n.Key : char.ToLowerInvariant(f.Name[0]) + f.Name.Substring(1);
                        return (key, f);
                    })
                    .OrderBy(x => x.key, StringComparer.Ordinal)
                    .ToArray();
                fields[t] = fs;
                return fs;
            }
        }

        public static string Of(object o)
        {
            var sb = new StringBuilder();
            Write(sb, o);
            return sb.ToString();
        }

        static void Write(StringBuilder sb, object o)
        {
            switch (o)
            {
                case null: sb.Append("null"); return;
                case string s: Str(sb, s); return;
                case bool b: sb.Append(b ? "true" : "false"); return;
                case int i: sb.Append(Js.Num(i)); return;
                case uint u: sb.Append(Js.Num(u)); return;
                case long l: sb.Append(Js.Num(l)); return;
                case double d: sb.Append(Js.Num(d)); return;
                case float f: sb.Append(Js.Num(f)); return;
                case IDictionary dict:
                {
                    var keys = dict.Keys.Cast<object>().Select(k => k.ToString()).OrderBy(k => k, StringComparer.Ordinal).ToList();
                    sb.Append('{');
                    bool first = true;
                    foreach (var k in keys)
                    {
                        var v = dict[k];
                        if (v == null) continue;
                        if (!first) sb.Append(',');
                        first = false;
                        Str(sb, k); sb.Append(':'); Write(sb, v);
                    }
                    sb.Append('}');
                    return;
                }
                case IEnumerable list:
                {
                    sb.Append('[');
                    bool first = true;
                    foreach (var v in list)
                    {
                        if (!first) sb.Append(',');
                        first = false;
                        Write(sb, v);
                    }
                    sb.Append(']');
                    return;
                }
                default:
                {
                    sb.Append('{');
                    bool first = true;
                    foreach (var (key, f) in FieldsOf(o.GetType()))
                    {
                        var v = f.GetValue(o);
                        if (v == null) continue;
                        if (!first) sb.Append(',');
                        first = false;
                        Str(sb, key); sb.Append(':'); Write(sb, v);
                    }
                    sb.Append('}');
                    return;
                }
            }
        }

        /// <summary>A string as JSON.stringify writes it.</summary>
        static void Str(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }
    }
}
