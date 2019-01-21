using System;
using static PlayStudios.Functional.Alg;

namespace PlayStudios.Functional
{
    public static class SumType
    {
        public sealed class Accessor<TOuterSumType, TContained>
        {
            public Accessor(
                Func<TOuterSumType, Option<TContained>> get,
                Func<TContained, TOuterSumType> create,
                Func<TOuterSumType, bool> isSameVariant)
            {
                mGet = get;
                mCreate = create;
                Exists = s => mGet(s).HasValue;
                IsSameVariant = isSameVariant;
            }

            // Exposing these functions instead of the lambdas direct as
            // to call the lambdas F# code has to call "Invoke".
            public Option<TContained> Get(TOuterSumType s) => mGet(s);

            public TOuterSumType Create(TContained t) => mCreate(t);


            public readonly Func<TOuterSumType, bool> Exists;
            private readonly Func<TOuterSumType, Option<TContained>> mGet;
            private readonly Func<TContained, TOuterSumType> mCreate;

            public readonly Func<TOuterSumType, bool> IsSameVariant;
        }

        public struct Store<TSel> : IComparable<Store<TSel>> where TSel : IEquatable<TSel>, IComparable<TSel>
        {
            public Store(object data, TSel selector)
            {
                Data = data;
                Selector = selector;
            }
            public readonly object Data;
            public readonly TSel Selector;

            // Works if the Data is comparable
            public int CompareTo(Store<TSel> other)
            {
                int fromSelectors = Selector.CompareTo(other.Selector);
                if (fromSelectors == 0)
                {
                    return ((IComparable)Data).CompareTo((IComparable)other.Data);
                }

                return fromSelectors;
            }

            public override int GetHashCode() => CombinedListOfHashCodes(ArrayLiteral(Selector.GetHashCode(), Data.GetHashCode()));
            public override bool Equals(object obj) =>obj != null && CompareTo((Store<TSel>)obj) == 0;

        }

        public static Accessor<TOuterSumType, TContained> MakeAccessor<TOuterSumType, TContained, TSel>(
            TSel selector,
            Func<TOuterSumType, Store<TSel>> getStore,
            Func<Store<TSel>, TOuterSumType> makeSumType)
            where TSel : IEquatable<TSel>, IComparable<TSel> =>
            new Accessor<TOuterSumType, TContained>(
                sumType =>
                {
                    var store = getStore(sumType);
                    if (store.Selector.Equals(selector))
                    {
                        return Some((TContained)store.Data);
                    }

                    return None<TContained>();
                },
                t => makeSumType(new Store<TSel>(t, selector)),
                o => selector.Equals(getStore(o).Selector));
    }
}
