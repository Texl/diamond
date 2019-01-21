using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Core;
using static PlayStudios.Functional.Alg;

namespace PlayStudios.Functional
{

    public sealed class Map1<TKey, TValue> : IComparable
    {
        private Map1(FSharpMap<TKey, TValue> map)
        {
            mUnderlyingData = map;
        }

        public static Option<Map1<TKey, TValue>> Contingent(IEnumerable<Tuple<TKey, TValue>> elements)
        {
            var r = MapModule.OfSeq(elements);
            return When(! r.IsEmpty, () => new Map1<TKey, TValue>(r));
        }

        public static Map1<TKey, TValue> Build(TKey initialKey, TValue initialValue, FSharpMap<TKey, TValue> rest)
        {
            return new Map1<TKey, TValue>(rest.Add(initialKey, initialValue));
        }

        public Map1<TKey, TValue> Add(TKey key, TValue value)
        {
            return new Map1<TKey, TValue>(mUnderlyingData.Add(key, value));
        }

        public FSharpMap<TKey, TValue> GetMap()
        {
            return mUnderlyingData;
        }

        public Option<Map1<TKey, TValue>> MapWhere(Func<KeyValuePair<TKey, TValue>, bool> filter) =>
            MapWhereFolding(UnitValue, kv => (filter(kv), Func((Unit x) => x))).Item1;

        public (Option<Map1<TKey, TValue>> possibleResultMap, TFolded folded) MapWhereFolding<TFolded>(TFolded original, Func<KeyValuePair<TKey, TValue>, (bool, Func<TFolded, TFolded>)> filterAndFold)
        {
            var current = mUnderlyingData;
            var cf = original;
            foreach (var kv in mUnderlyingData)
            {
                var fr = filterAndFold(kv);
                if (!fr.Item1)
                {
                    current = current.Remove(kv.Key);
                }

                cf = fr.Item2(cf);

                if (!current.Any())
                {
                    return (None<Map1<TKey, TValue>>(), cf);
                }
            }

            return (Some(new Map1<TKey, TValue>(current)), cf);
        }


        public Enumerable1<TKey> GetKeys() => Enumerable1.InsistWithSkip1(mUnderlyingData.Select(x => x.Key));
        public Enumerable1<TValue> GetValues() => Enumerable1.InsistWithSkip1(mUnderlyingData.Select(x => x.Value));

        public IEnumerable<Tuple<TKey, TValue>> GetKeyValuePairs()
        {
            return MapModule.ToSeq(mUnderlyingData);
        }

        public Array1<Tuple<TKey, TValue>> GetKeyValuePairsA1()
        {
            return Array1.Insist(GetKeyValuePairs());
        }

        public Option<TValue> GetValueIfPresent(TKey key) => mUnderlyingData.GetValueIfPresent(key);

        public TValue GetValue(TKey key) => mUnderlyingData.GetValue(key);
        public TValue GetValue(TKey key, Func<TKey, string> augmentErrorMessage) => mUnderlyingData.GetValue(key, augmentErrorMessage);

        public Option<Map1<TKey, TValue>> Remove(TKey key)
        {
            var withRemoval = mUnderlyingData.Remove(key);
            return When(! withRemoval.IsEmpty, () => new Map1<TKey, TValue>(withRemoval));
        }

        public bool ContainsKey(TKey key) => mUnderlyingData.ContainsKey(key);

        public IDictionary<TKey, TValue> AsDictionary => mUnderlyingData.ToDictionary(x => x.Key, x => x.Value);

        private static FSharpMap<TKey, TValue> CopiedDictionary(IDictionary<TKey, TValue> d)
        {
            var r = MapModule.OfSeq(d.Select(x => Tuple.Create(x.Key, x.Value)));
            if (r.IsEmpty)
            {
                throw new Exception("Passed in an empty dictionary - inappropriate for Map1");
            }
            return r;
        }

        public int Count => mUnderlyingData.Count;

        public Map1<TKey, TValueU> MapValue<TValueU>(Func<TValue, TValueU> f) => new Map1<TKey, TValueU>(MapModule.OfSeq(mUnderlyingData.Select(kv => Tuple.Create(kv.Key, f(kv.Value)))));
        public Map1<TKey, TValueU> MapValueFS<TValueU>(FSharpFunc<TValue, TValueU> f) => new Map1<TKey, TValueU>(MapModule.OfSeq(mUnderlyingData.Select(kv => Tuple.Create(kv.Key, f.Invoke(kv.Value)))));

        public Map1<TKey, TValueU> MapValue<TValueU>(Func<TKey, TValue, TValueU> f) => new Map1<TKey, TValueU>(MapModule.OfSeq(mUnderlyingData.Select(kv => Tuple.Create(kv.Key, f(kv.Key, kv.Value)))));
        public Map1<TKey, TValueU> MapValueFS<TValueU>(FSharpFunc<TKey, FSharpFunc<TValue, TValueU>> f) => new Map1<TKey, TValueU>(MapModule.OfSeq(mUnderlyingData.Select(kv => Tuple.Create(kv.Key, f.Invoke(kv.Key).Invoke(kv.Value)))));


        public Map1<TKeyU, TValueU> MapInsistKeyUniquenessHolds<TKeyU, TValueU>(Func<TKey, TKeyU> fKey, Func<TValue, TValueU> fValue) =>
            new Map1<TKeyU, TValueU>(MapModule.OfSeq(mUnderlyingData.Select(kv => Tuple.Create(fKey(kv.Key), fValue(kv.Value))).GroupBy(x => x.Item1).Select(g => Tuple.Create(g.Key, g.Single().Item2))));

        private readonly FSharpMap<TKey, TValue> mUnderlyingData;




        public int CompareTo(object other)
        {
            var theOther = other as Map1<TKey, TValue>;
            if (theOther != null)
            {
                return ((IComparable) mUnderlyingData).CompareTo((IComparable) theOther.mUnderlyingData);
            }
            throw new Exception("Bad attempt to compare Map1");
        }

        public override string ToString() => mUnderlyingData.ToString();
        public override bool Equals(object obj) => (obj is Map1<TKey, TValue>) && (this.Equals((Map1<TKey, TValue>)obj));
        public override int GetHashCode() => mUnderlyingData.GetHashCode();

        public bool Equals(Map1<TKey, TValue> other) => mUnderlyingData.Equals(other.mUnderlyingData);
    }

    public static class Map1
    {
        public static Map1<TKey, TValue> Sole<TKey, TValue>(TKey key, TValue value) =>
            Map1<TKey, TValue>.Build(key, value, MapModule.Empty<TKey, TValue>());

        public static Map1<TKey, TValue> Build<TKey, TValue>(Array1<Tuple<TKey, TValue>> elements) =>
            Map1<TKey, TValue>.Build(elements.First.Item1, elements.First.Item2, MapModule.OfSeq(elements.Rest));

        public static Map1<TKey, TValue> Build<TKey, TValue>(Enumerable1<Tuple<TKey, TValue>> elements) =>
            Map1<TKey, TValue>.Build(elements.First.Item1, elements.First.Item2, MapModule.OfSeq(elements.Rest));

        public static Map1<TKey, TValue> Insist<TKey, TValue>(IEnumerable<Tuple<TKey, TValue>> elements) =>
            Map1<TKey, TValue>.Contingent(elements).Match(
                x => x, () =>
                {
                    throw new Exception("Tried to 'Insist' a map1 off an empty list!");
                });

        public static Map1<TKey, TValue> Insist<TKey, TValue>(IDictionary<TKey, TValue> dictionary) =>
            Map1<TKey, TValue>.Contingent(dictionary.Select(x => Tuple.Create(x.Key, x.Value))).Match(
                x => x, () =>
                {
                    throw new Exception("Tried to 'Insist' a map1 off an empty list!");
                });

        public static Option<Map1<TKey, TValue>> Contingent<TKey, TValue>(IEnumerable<Tuple<TKey, TValue>> elements) =>
            Map1<TKey, TValue>.Contingent(elements);
    }
}
