using System;
using System.Collections.Generic;
using System.Linq;

namespace PlayStudios.Functional
{

    // A sort of smart array for associating with a QID as index
    public sealed class QualifiedPackedStore<TQualification, TValue>
    {
        public QualifiedPackedStore(TValue [] data)
        {
            Data = data;
        }

        private readonly TValue [] Data;

        public IEnumerable<TValue> GetValues() => Data;

        public IEnumerable<Tuple<QID<TQualification>, TValue>> GetIDQualifiedValues() => Data.Select((x, ord) => Tuple.Create(QID.Build<TQualification>(ord), x));

        public TValue GetValue(QID<TQualification> key) => Data[key.IDValue];


        public QualifiedPackedStore<TQualification, U> Transformed<U>(Func<TValue, U> f) => new QualifiedPackedStore<TQualification, U>(Data.Select(f).ToArray());
        public QualifiedPackedStore<TQualification, U> Transformed<U>(Func<QID<TQualification>, TValue, U> f) => new QualifiedPackedStore<TQualification, U>(Data.Select((v, ord) => f(QID.Build<TQualification>(ord), v)).ToArray());
        public QualifiedPackedStore<QU, TValue> Requalified<QU>() => new QualifiedPackedStore<QU, TValue>(Data);

        // This uses "U" type as the search clause, i.e. no intermediary transformer - **ONLY** use when U can serve as a search key.
        public Func<U, QID<TQualification>> BuildReverseSearchBasedOnValueDictionary<U>(Func<TValue, U> f) => Data.Select((x, ord) => Tuple.Create(f(x), QID.Build<TQualification>(ord))).ToDictionary().GetValue;

        // This uses "U" type as the interim key for search!! ; but exposes TValue - obvious useful when TValue works as a key.
        public Func<TValue, QID<TQualification>> BuildReverseSearchBasedOnInterimKey<U>(Func<TValue, U> f) => Data.Select((x, ord) => Tuple.Create(f(x), QID.Build<TQualification>(ord))).ToDictionary().Let(d => new Func<TValue, QID<TQualification>>(x => d.GetValue(f(x))));
    }

    public static class QualifiedPackedStore
    {
        public struct Builder<TQualification> // To get around the type-work
        {
            public QualifiedPackedStore<TQualification, TValue> Build<TValue>(IEnumerable<TValue> data) => new QualifiedPackedStore<TQualification, TValue>(data.ToArray());
        }

        public static Builder<TQualification> Qualified<TQualification>() => new Builder<TQualification>();
    }
}
