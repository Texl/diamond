using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static PlayStudios.Functional.Alg;

namespace PlayStudios.Functional
{
    // Useful bits of code for generating test data code fragments from data - generally to be used for temporary scraps
    // Of code to dig out the strings.
    public static class CodeFragments
    {
        private static string PolicyDirectTypeName(Type type) => type.Name; // deliberately taking the short, easier for a resharper unit to add than subtract.

        public static string GetTypeLiteral(Type type) =>
            PolicyDirectTypeName(type).Let(
                rawName =>
                    type.IsGenericType
                        ? rawName.Substring(0, rawName.IndexOf('`')) + Braced("<", ">")(MergedStrings(Intersperse(",", type.GetGenericArguments().Select(GetTypeLiteral))))
                        : new Dictionary<string, string>
                        {
                            { "String", "string" }
                        }.Let(d =>
                            d.GetValueIfPresent(rawName).Match(x => x, () => rawName)));

        public static string GetLiteralForDictionary<K, V>(IDictionary<K, V> d, Func<K, string> fk, Func<V, string> fv) =>
            "new Dictionary<" + GetTypeLiteral(typeof(K)) + "," + GetTypeLiteral(typeof(V)) + "> {"
            + MergedStrings(Intersperse(",", d.Select(kv => "{" + fk(kv.Key) + "," + fv(kv.Value) + "}")))
            + "}";

        public static string GetLiteralForList<T>(IEnumerable<T> l, Func<T, string> fv) =>
            "new " /* + GetTypeLiteral(typeof(T)) */ + "[] {" + MergedStrings(Intersperse(",", l.Select(fv))) + "}";

        public static Func<string, string> Braced(string left, string right) => inner => left + inner + right;


        public static string Quoted(string str) => Braced("\"", "\"")(str.Replace("\"", "\\\""));

    }
}
