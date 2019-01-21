using System.Linq;
using System;
using System.Collections.Generic;

namespace PlayStudios.Functional
{
    public static class ExtensionsContainers
    {
        private static string GetGetValueErrorMessage<TKey, TValue>(IEnumerable<KeyValuePair<TKey, TValue>> dictionary, TKey key)
        {
            const int lengthCap = 500;
            var keysCharList = Alg.Intersperse(",", dictionary.Select(x => x.Key.ToString())).SelectMany(x => x.ToCharArray());

            var errorMessage =
                "Failed to find key " + key.ToString() + " in dictionary containing keys [" +
                Alg.MergedChars(keysCharList.Take(lengthCap)) + (keysCharList.Skip(lengthCap).Any() ? " ...<truncated for length>" : "") + "]";
            return errorMessage;

        }

        public static Value GetValue<Key, Value>(this IDictionary<Key, Value> dictionary, Key key) => dictionary.GetValue(key, _ => "");

        public static Value GetValue<Key, Value>(this IDictionary<Key, Value> dictionary, Key key, Func<Key, string> augmentErrorMessage)
        {
            if (dictionary.TryGetValue(key, out Value r))
            {
                return r;
            }
            else
            {
                var augment = augmentErrorMessage(key);
                var errorMessage = GetGetValueErrorMessage(dictionary, key) + (augment.Any() ? ("  -  " + augment) : "");
                throw new Exception(errorMessage);
            }
        }


        public static Value GetValue<Key, Value>(this IReadOnlyDictionary<Key, Value> dictionary, Key key)
        {
            if (dictionary.TryGetValue(key, out var r))
            {
                return r;
            }
            else
            {
                var errorMessage = GetGetValueErrorMessage(dictionary, key);
                throw new Exception(errorMessage);
            }
        }

        public static Value GetValue<Key, Value>(this Dictionary<Key, Value> dictionary, Key key)
        {
            if (dictionary.TryGetValue(key, out var r))
            {
                return r;
            }
            else
            {
                var errorMessage = GetGetValueErrorMessage(dictionary, key);
                throw new Exception(errorMessage);
            }
        }
        public static T GetArrayValue<T>(this T[] array, int index)
        {
            if (index >= array.Length)
            {
                throw new Exception("Index " + index + " out of range of " + array.Length);
            }
            return array[index];
        }


        /// <summary>
        /// Exists solely to factor out dictionary.TryGetValue or associated if-storms
        /// Having the result of a dictionary lookup be in "list" (of length 0 or 1) form has some very useful properties, especially
        /// when mixed with .SelectMany - a lot of "if" calculations can be removed and this lets you stay "in-expression" when building
        /// more complex logic that happens to involve the result of an optional search.
        /// </summary>
        /// <typeparam name="Key"></typeparam>
        /// <typeparam name="Value"></typeparam>
        /// <param name="dictionary"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public static Option<Value> GetValueIfPresent<Key, Value>(this IDictionary<Key, Value> dictionary, Key key)
        {
            return (dictionary.TryGetValue(key, out var r))
            ? Alg.Some(r)
            : Alg.None<Value>();
        }

        public static Option<Value> GetValueIfPresent<Key, Value>(this IReadOnlyDictionary<Key, Value> dictionary, Key key)
        {
            return (dictionary.TryGetValue(key, out var r))
            ? Alg.Some(r)
            : Alg.None<Value>();
        }

        public static Option<Value> GetValueIfPresent<Key, Value>(this Dictionary<Key, Value> dictionary, Key key)
        {
            return (dictionary.TryGetValue(key, out var r))
            ? Alg.Some(r)
            : Alg.None<Value>();
        }


        public static Value GetValueAssuringPresent<Key, Value>(this IDictionary<Key, Value> dictionary, Key key, Func<Value> ifNotPresent)
        {
            if (dictionary.ContainsKey(key))
            {
                return dictionary[key];
            }
            Value val = ifNotPresent();
            dictionary.Add(key, val);
            return val;
        }

        public static Dictionary<K, U> MapValue<K, V, U>(this Dictionary<K, V> d, Func<V, U> f) => d.ToDictionary(kv => kv.Key, kv => f(kv.Value));

        // this one kind of questionable now as it assumes end-user wants physical hashtable dictionary. Almost wants another name, or convert the other users to more solid containers.
        // Problem almost that it becomes viral due to returning IDictionary anyway.
        // May change it to returning solid Dict first, modifying code downchain, then reassessing.
        public static IDictionary<K, U> MapValue<K, V, U>(this IDictionary<K, V> d, Func<V, U> f) => d.ToDictionary(kv => kv.Key, kv => f(kv.Value));

        // The mapvalues could theoretically be sped up someday through more structural control



        // High utility - saves a local cast or rebuild
        public static IDictionary<K, V> AsIDictionary<K, V>(this IDictionary<K, V> d) => d;
 
        // Regular dictionary doesn't serialize - some utility here. Any mismatch between key-uniqueness-ability, (which in this direction by definition shouldn't exist for map/ordering compliant keys)
        // ToDictionary crashes.

        public static IDictionary<K, U> MapValue<K, V, U>(this IDictionary<K, V> d, Func<K, V, U> f) => d.ToDictionary(kv => kv.Key, kv => f(kv.Key, kv.Value));

        public static Tuple<TKey, TValue> ToTuple<TKey, TValue>(this KeyValuePair<TKey, TValue> kvp) => Tuple.Create(kvp.Key, kvp.Value);
        public static Tuple<TKey, TValue> ToTuple<TKey, TValue>(this (TKey, TValue) kvp) => Tuple.Create(kvp.Item1, kvp.Item2);
    }
}
