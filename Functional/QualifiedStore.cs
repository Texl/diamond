using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlayStudios.Functional
{
    public sealed class QualifiedStore<TQualification, TValue>
    {
        public QualifiedStore(Dictionary<QID<TQualification>, TValue> data)
        {
            Data = data;
        }

        public readonly Dictionary<QID<TQualification>, TValue> Data;

        public TValue GetValue(QID<TQualification> key) => Data.GetValue(key);

        public IEnumerable<Tuple<TValue, QID<TQualification>>> GetKVReversed() => Data.Select(kv => Tuple.Create(kv.Value, kv.Key));

        public QualifiedStore<TQualification, U> Transformed<U>(Func<TValue, U> f) => new QualifiedStore<TQualification, U>(Data.ToDictionary(x => x.Key, x => f(x.Value)));
        public QualifiedStore<QU, TValue> Requalified<QU>() => new QualifiedStore<QU, TValue>(Data.ToDictionary(x => x.Key.Requalified<QU>(), x => x.Value));

    }

    public static class QualifiedStore
    {
        public static QualifiedStore<TQualification, TValue> Build<TQualification, TValue>(IEnumerable<Tuple<QID<TQualification>, TValue>> data) => new QualifiedStore<TQualification, TValue>(data.ToDictionary(x => x.Item1, x => x.Item2));
        public static QualifiedStore<TQualification, TValue> Build<TQualification, TValue>(IEnumerable<(QID<TQualification> QID, TValue Value)> data) => new QualifiedStore<TQualification, TValue>(data.ToDictionary(x => x.QID, x => x.Value));
    }
}
