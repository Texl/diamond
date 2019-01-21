using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.FSharp.Collections;

namespace PlayStudios.Functional
{
    public static class ExtensionsFSharpMap
    {

        // IDictionary/IReadonlyDictionary provide new ambiguity
        public static Value GetValue<Key, Value>(this FSharpMap<Key, Value> dictionary, Key key) =>
            ((IDictionary<Key, Value>)dictionary).GetValue(key);

        // Needed because of new ambiguity over FSharpMap getting IReadonlyDictionary
        public static Option<Value> GetValueIfPresent<Key, Value>(this FSharpMap<Key, Value> dictionary, Key key) =>
            ((IDictionary<Key, Value>)dictionary).GetValueIfPresent(key);


        public static FSharpMap<TKey, TValue> WithGuaranteedEntry<TKey, TValue>(this FSharpMap<TKey, TValue> map, TKey key, Func<TValue> toAddIfNotPresent)
        {
            if (map.ContainsKey(key))
            {
                return map;
            }
            else
            {
                return map.Add(key, toAddIfNotPresent());
            }
        }

        // The mapvalues could theoretically be sped up someday through more structural control
        public static FSharpMap<K, U> MapValue<K, V, U>(this FSharpMap<K, V> d, Func<V, U> f) => MapModule.OfSeq(d.Select(kv => Tuple.Create(kv.Key, f(kv.Value))));
        public static FSharpMap<K, U> MapValue<K, V, U>(this FSharpMap<K, V> d, Func<K, V, U> f) => MapModule.OfSeq(d.Select(kv => Tuple.Create(kv.Key, f(kv.Key, kv.Value))));

        public static FSharpMap<U, V> MapKey<K, V, U>(this FSharpMap<K, V> d, Func<K, U> f) => MapModule.OfSeq(d.Select(kv => Tuple.Create(f(kv.Key), kv.Value)));


        // Perhaps optimal for understanding, or speed, when the removal is small
        public static FSharpMap<K, V> WhereK<K, V>(this FSharpMap<K, V> d, Func<K, bool> keep) => d.Where(kv => !keep(kv.Key)).Aggregate(d, (s, n) => s.Remove(n.Key));
        public static FSharpMap<K, V> WhereV<K, V>(this FSharpMap<K, V> d, Func<V, bool> keep) => d.Where(kv => !keep(kv.Value)).Aggregate(d, (s, n) => s.Remove(n.Key));
        public static FSharpMap<K, V> WhereKV<K, V>(this FSharpMap<K, V> d, Func<KeyValuePair<K, V>, bool> keep) => d.Where(kv => !keep(kv)).Aggregate(d, (s, n) => s.Remove(n.Key));

        public static FSharpMap<K, U> SelectManyK<K, V, U>(this IDictionary<K, V> d, Func<K, Option<U>> f) => Alg.MapOfSeq(d.SelectMany(kv => f(kv.Key).Select(x => (kv.Key, x))));
        public static FSharpMap<K, U> SelectManyV<K, V, U>(this IDictionary<K, V> d, Func<V, Option<U>> f) => Alg.MapOfSeq(d.SelectMany(kv => f(kv.Value).Select(x => (kv.Key, x))));
        public static FSharpMap<K, U> SelectManyKV<K, V, U>(this IDictionary<K, V> d, Func<KeyValuePair<K, V>, Option<U>> f) => Alg.MapOfSeq(d.SelectMany(kv => f(kv).Select(x => (kv.Key, x))));


        public static Dictionary<K, V> ToDictionary<K, V>(this FSharpMap<K, V> d) => d.ToDictionary(kv => kv.Key, kv => kv.Value);



        // Private ones here solely for local utility, not to be confused with originals
        private static Tuple<TKey, TValue> ToTuple<TKey, TValue>(this KeyValuePair<TKey, TValue> kvp) => Tuple.Create(kvp.Key, kvp.Value);
        private static Tuple<TKey, TValue> ToTuple<TKey, TValue>(this (TKey, TValue) kvp) => Tuple.Create(kvp.Item1, kvp.Item2);

        public static FSharpMap<TKey, TValue> ToFSharpMap<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>> kvps) => MapModule.OfSeq(kvps.Select(ToTuple));
        public static FSharpMap<TKey, TValue> ToFSharpMap<TKey, TValue>(this IEnumerable<(TKey, TValue)> kvps) => MapModule.OfSeq(kvps.Select(ToTuple));
        public static FSharpMap<TKey, TValue> ToFSharpMap<TKey, TValue>(this IEnumerable<Tuple<TKey, TValue>> kvps) => MapModule.OfSeq(kvps);

    }
}
