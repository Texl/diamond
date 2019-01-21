using System;
using System.Collections.Generic;
using System.Linq;

namespace PlayStudios.Functional
{
    public sealed class TwinMap1<TPrimaryKey, TSecondary, TValue>
    {
        private TwinMap1(Map1<TPrimaryKey, Tuple<TSecondary, TValue>> primaryMap, Map1<TSecondary, TPrimaryKey> secondaryMap)
        {
            PrimaryMap = primaryMap;
            SecondaryMap = secondaryMap;
        }

        public static Option<TwinMap1<TPrimaryKey, TSecondary, TValue>> Contingent(IEnumerable<Tuple<TPrimaryKey, TSecondary, TValue>> entries) =>
            Map1.Contingent(entries.Select(x => Tuple.Create(x.Item1, Tuple.Create(x.Item2, x.Item3)))).Select(
                p =>
                    new TwinMap1<TPrimaryKey, TSecondary, TValue>(
                        p,
                        Map1.Insist(p.GetKeyValuePairs().Select(x => Tuple.Create(x.Item2.Item1, x.Item1)))));

        public static TwinMap1<TPrimaryKey, TSecondary, TValue> Build(Array1<Tuple<TPrimaryKey, TSecondary, TValue>> entries) =>
            Map1.Build(entries.Select(x => Tuple.Create(x.Item1, Tuple.Create(x.Item2, x.Item3)))).Let(
                p =>
                    new TwinMap1<TPrimaryKey, TSecondary, TValue>(
                        p,
                        Map1.Insist(p.GetKeyValuePairs().Select(x => Tuple.Create(x.Item2.Item1, x.Item1)))));


        public TwinMap1<TPrimaryKey, TSecondary, TValueU> MapValue<TValueU>(Func<TValue, TValueU> f) => new TwinMap1<TPrimaryKey, TSecondary, TValueU>(PrimaryMap.MapValue(t => Tuple.Create(t.Item1, f(t.Item2))), SecondaryMap);

        public IEnumerable<Tuple<TPrimaryKey, TSecondary, TValue>> Entries => PrimaryMap.GetKeyValuePairs().Select(t => Tuple.Create(t.Item1, t.Item2.Item1, t.Item2.Item2));


        public readonly Map1<TPrimaryKey, Tuple<TSecondary, TValue>> PrimaryMap;
        public readonly Map1<TSecondary, TPrimaryKey> SecondaryMap;
    }

    public static class TwinMap1
    {
        public static TwinMap1<TPrimaryKey, TSecondary, TValue> Build<TPrimaryKey, TSecondary, TValue>(Array1<Tuple<TPrimaryKey, TSecondary, TValue>> entries) =>
            TwinMap1<TPrimaryKey, TSecondary, TValue>.Build(entries);

        public static Option<TwinMap1<TPrimaryKey, TSecondary, TValue>> Contingent<TPrimaryKey, TSecondary, TValue>(IEnumerable<Tuple<TPrimaryKey, TSecondary, TValue>> entries) =>
            TwinMap1<TPrimaryKey, TSecondary, TValue>.Contingent(entries);

        public static TwinMap1<TPrimaryKey, TSecondary, TValue> Insist<TPrimaryKey, TSecondary, TValue>(IEnumerable<Tuple<TPrimaryKey, TSecondary, TValue>> entries) =>
            TwinMap1<TPrimaryKey, TSecondary, TValue>.Contingent(entries).Match(
                x => x,
                () =>
                {
                    throw new Exception("Tried to 'Insist' a TwinMap1 off an empty list!");
                });


    }

}
