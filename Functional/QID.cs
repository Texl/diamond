using System;
namespace PlayStudios.Functional
{
    // qualified typeID - solely to take the place of an int-based ID but with a type in it to prevent them being cross-purposed
    // Putting comparison on it too so it can be used in F# comparison-based containers.
    // Keeping serialization **off** this one deliberately
    public struct QID<TQualification> : IComparable<QID<TQualification>>, IEquatable<QID<TQualification>>, IComparable
    {
        public QID(int idValue)
        {
            IDValue = idValue;
#if DEBUG
            mInitialized = true;
#endif
        }
        public static QID<TQualification> Build(int idValue) => new QID<TQualification>(idValue);
        public readonly int IDValue;

        public int CompareTo(QID<TQualification> other) => IDValue.CompareTo(other.IDValue);
        public int CompareTo(object other)
        {
#if DEBUG
            VerifyThisIsInitialized();
#endif
            if (other is QID<TQualification>)
            {
#if DEBUG
                VerifyIsInitialized((QID<TQualification>)other);
#endif
                return CompareTo((QID<TQualification>)other);
            }
            throw new Exception("Bad attempt to compare QID of " + typeof(TQualification) + " to " + other.GetType());
        }

        public override string ToString()
        {
#if DEBUG
            VerifyThisIsInitialized();
#endif
            return IDValue.ToString();
        }

        public override bool Equals(object obj)
        {
#if DEBUG
            VerifyThisIsInitialized();
#endif
            return (obj is QID<TQualification>) && Equals((QID<TQualification>)obj);
        }

        public override int GetHashCode() => IDValue.GetHashCode();

        public bool Equals(QID<TQualification> obj)
        {
#if DEBUG
            VerifyThisIsInitialized();
            VerifyIsInitialized(obj);
#endif

            return obj.IDValue == IDValue;
        }

        public QID<U> Requalified<U>() => new QID<U>(IDValue);


        // Prevent anything that would try to auto-serialize public properties.
        public int GuardFieldToPreventAutoSerializationToJSON { get { throw new Exception("GuardFieldToPreventAutoSerializationToJSON QID" + typeof(TQualification).ToString() + " - use .IDValue"); } }

#if DEBUG
        private readonly bool mInitialized; // going to check this everywhere direct, rather than factor, for performance reasons.

        private void ComplainUninitialized(string id)
        {
            throw new Exception(id + " QID " + typeof(TQualification) + " being used without being initialized, i.e. didn't assign value at this location, not that it was created in malformed state. Wherever it came from is uninitialized and must be fixed");
        }

        private static void VerifyIsInitialized<U>(QID<U> other)
        {
            if (!other.mInitialized)
            {
                other.ComplainUninitialized("Other");
            }
        }
        private void VerifyThisIsInitialized()
        {
            if (!mInitialized)
            {
                ComplainUninitialized("This");
            }
        }
#endif

    }

    public static class QID
    {
        public static QID<TQualification> Build<TQualification>(int idValue) => QID<TQualification>.Build(idValue);
    }
}
