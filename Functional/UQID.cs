using System;

namespace PlayStudios.Functional
{
    // qualified typeID - solely to take the place of an Guid-based ID but with a type in it to prevent them being cross-purposed
    // Putting comparison on it too so it can be used in F# comparison-based containers.
    // Keeping serialization **off** this one deliberately
    public struct UQID<TQualification> : IComparable<UQID<TQualification>>, IEquatable<UQID<TQualification>>, IComparable
    {
        public UQID(Guid idValue)
        {
            IDValue = idValue;
#if DEBUG
            mInitialized = true;
#endif
        }
        public static UQID<TQualification> Build(Guid idValue) => new UQID<TQualification>(idValue);
        public readonly Guid IDValue;

        public int CompareTo(UQID<TQualification> other) => IDValue.CompareTo(other.IDValue);
        public int CompareTo(object other)
        {
#if DEBUG
            VerifyThisIsInitialized();
#endif
            if (other is UQID<TQualification>)
            {
#if DEBUG
                VerifyIsInitialized((UQID<TQualification>)other);
#endif
                return CompareTo((UQID<TQualification>)other);
            }
            throw new Exception("Bad attempt to compare UQID of " + typeof(TQualification) + " to " + other.GetType());
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
            return (obj is UQID<TQualification>) && Equals((UQID<TQualification>)obj);
        }

        public override int GetHashCode() => IDValue.GetHashCode();

        public bool Equals(UQID<TQualification> obj)
        {
#if DEBUG
            VerifyThisIsInitialized();
            VerifyIsInitialized(obj);
#endif

            return obj.IDValue.Equals(IDValue);
        }

        public UQID<U> Requalified<U>() => new UQID<U>(IDValue);


        // Prevent anything that would try to auto-serialize public properties.
        public int GuardFieldToPreventAutoSerializationToJSON { get { throw new Exception("GuardFieldToPreventAutoSerializationToJSON UQID" + typeof(TQualification).ToString() + " - use .IDValue"); } }

#if DEBUG
        private readonly bool mInitialized; // going to check this everywhere direct, rather than factor, for performance reasons.

        private void ComplainUninitialized(string id)
        {
            throw new Exception(id + " UQID " + typeof(TQualification) + " being used without being initialized, i.e. didn't assign value at this location, not that it was created in malformed state. Wherever it came from is uninitialized and must be fixed");
        }

        private static void VerifyIsInitialized<U>(UQID<U> other)
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

    public static class UQID
    {
        public static UQID<TQualification> Build<TQualification>(Guid idValue) => UQID<TQualification>.Build(idValue);
    }
}
