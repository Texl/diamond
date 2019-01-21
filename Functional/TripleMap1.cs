using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlayStudios.Functional
{
    public sealed class TripleMap1<TPrimaryKey, TSecondary, TTertiary, TValue>
    {
        private TripleMap1(Map1<TPrimaryKey, Tuple<TSecondary, TTertiary, TValue>> primaryMap, Map1<TSecondary, TPrimaryKey> secondaryMap, Map1<TTertiary, TPrimaryKey> tertiaryMap1)
        {
            PrimaryMap = primaryMap;
            SecondaryMap = secondaryMap;
            TertiaryMap = tertiaryMap1;
        }

        public static Option<TripleMap1<TPrimaryKey, TSecondary, TTertiary, TValue>> Contingent(IEnumerable<Tuple<TPrimaryKey, TSecondary, TTertiary, TValue>> entries) =>
            Map1.Contingent(entries.Select(x => Tuple.Create(x.Item1, Tuple.Create(x.Item2, x.Item3, x.Item4)))).Select(
                p =>
                    new TripleMap1<TPrimaryKey, TSecondary, TTertiary, TValue>(
                        p,
                        Map1.Insist(p.GetKeyValuePairs().Select(x => Tuple.Create(x.Item2.Item1, x.Item1))),
                        Map1.Insist(p.GetKeyValuePairs().Select(x => Tuple.Create(x.Item2.Item2, x.Item1)))));

        public TripleMap1<TPrimaryKey, TSecondary, TTertiary, TValueU> MapValue<TValueU>(Func<TValue, TValueU> f) => new TripleMap1<TPrimaryKey, TSecondary, TTertiary, TValueU>(PrimaryMap.MapValue(t => Tuple.Create(t.Item1, t.Item2, f(t.Item3))), SecondaryMap, TertiaryMap);

        public IEnumerable<Tuple<TPrimaryKey, TSecondary, TTertiary, TValue>> Entries => PrimaryMap.GetKeyValuePairs().Select(t => Tuple.Create(t.Item1, t.Item2.Item1, t.Item2.Item2, t.Item2.Item3));


        public readonly Map1<TPrimaryKey, Tuple<TSecondary, TTertiary, TValue>> PrimaryMap;
        public readonly Map1<TSecondary, TPrimaryKey> SecondaryMap;
        public readonly Map1<TTertiary, TPrimaryKey> TertiaryMap;
    }

    public static class TripleMap1
    {
        public static Option<TripleMap1<TPrimaryKey, TSecondary, TTertiary, TValue>> Contingent<TPrimaryKey, TSecondary, TTertiary, TValue>(IEnumerable<Tuple<TPrimaryKey, TSecondary, TTertiary, TValue>> entries) =>
            TripleMap1<TPrimaryKey, TSecondary, TTertiary, TValue>.Contingent(entries);

        public static TripleMap1<TPrimaryKey, TSecondary, TTertiary, TValue> Insist<TPrimaryKey, TSecondary, TTertiary, TValue>(IEnumerable<Tuple<TPrimaryKey, TSecondary, TTertiary, TValue>> entries) =>
            TripleMap1<TPrimaryKey, TSecondary, TTertiary, TValue>.Contingent(entries).Match(
                x => x,
                () =>
                {
                    throw new Exception("Tried to 'Insist' a TripleMap1 off an empty list!");
                });


    }

}
