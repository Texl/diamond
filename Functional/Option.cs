using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using JetBrains.Annotations; // replacement for System.Diagnostics.Contracts to get alternate PureAttribute - in attempt to get [Pure] to be seen by resharper
using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Core;
using static PlayStudios.Functional.Alg;

namespace PlayStudios.Functional
{
    public static class Option
    {
        public static Option<T> FromNullable<T>(T t) where T : class
        {
            if (t != null)
            {
                return Option<T>.Some(t);
            }
            else
            {
                return Option<T>.None();
            }
        }

        public static Option<T> FromNullable<T>(Nullable<T> t) where T : struct
        {
            if (t.HasValue)
            {
                return Option<T>.Some(t.Value);
            }
            else
            {
                return Option<T>.None();
            }
        }

        public static Option<T> MaybeFirst<T>(this IEnumerable<T> list, Func<T, bool> predicate)
        {
            using (var enumerator = list.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    if (predicate(enumerator.Current))
                    {
                        return Option<T>.Some(enumerator.Current);
                    }
                }

                return Option<T>.None();
            }
        }

        public static Option<T> MaybeFirst<T>(this IEnumerable<T> list)
        {
            using (var enumerator = list.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    return Option<T>.Some(enumerator.Current);
                }

                return Option<T>.None();
            }
        }
        

        public static Option<T> MaybeLast<T>(this IEnumerable<T> list)
        {
            var lastSeenIfAny = Option<T>.None();

            using (var enumerator = list.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    lastSeenIfAny = Some(enumerator.Current);
                }

                return lastSeenIfAny;
            }
        }

        public static Option<T> MaybeSingle<T>(this IEnumerable<T> source, Func<T, bool> predicate)
        {
            // Preserving (original) SingleOrDefault semantics - if we find more than one, grenade!
            var foundOne = Option<T>.None();
            using (var enumerator = source.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    if (predicate(enumerator.Current))
                    {
                        if (foundOne.HasValue)
                        {
                            throw new Exception("Found more than one in sequence in MaybeSingle, which is continuing semantics of SingleOrDefault from its original implementation");
                        }
                        foundOne = Option<T>.Some(enumerator.Current);
                    }
                }

                return foundOne;
            }
        }

        // Similar pattern as ChoiceOutcome.FromSequence
        private static Option<FSharpList<T>> FromSequenceH<T>(FSharpList<Option<T>> sources, FSharpList<T> accumulatedResult) =>
            sources.IsEmpty
                ? Some(accumulatedResult)
                : sources.Head.SelectMany(elt => FromSequenceH(sources.Tail, FSharpList<T>.Cons(elt, accumulatedResult)));

        public static Option<FSharpList<T>> FromSequence<T>(IEnumerable<Option<T>> sources) =>
            FromSequenceH(ListModule.OfSeq(sources.Reverse()), ListModule.Empty<T>());

        public static T? ToNullable<T>(this Option<T> option) where T : struct =>
            option.Match(v => (T?)v, () => default(T?));

        public static T ToNullPossible<T>(this Option<T> o) where T : class =>
            o.Match(v => v, () => null);

        public static T ValueOrDefault<T>(this Option<T> o, Func<T> getDefault) =>
            o.Match(v => v, getDefault);

        /// <summary>
        /// Left-biased choice of Option - basically returns `other` iff this Option is None
        /// </summary>
        /// <example>
        /// [Some(x), Some(y)] => Some(x)
        /// [Some(x), None]    => Some(x)
        /// [None,    Some(y)] => Some(y)
        /// [None,    None]    => None
        /// </example>
        public static Option<T> OrElse<T>(this Option<T> o, Option<T> other) =>
            o.Match(_ => o, () => other);

        public static Option<T> OrElse<T>(this Option<T> o, Func<Option<T>> genOther) =>
            o.Match(_ => o, genOther);

        /// <summary>
        /// If all params are present, returns Some Some T[]
        /// If all params are absent, returns Some None
        /// Otherwise, returns None
        /// </summary>
        public static Option<Option<T[]>> MaybeAligned<T>(params Option<T>[] os) =>
            os.SelectMany(o => o).ToArray()
                .Let(values =>
                    values.Length == os.Length
                        ? Some(Some(values))
                        : values.Length == 0
                            ? Some(None<T[]>())
                            : None<Option<T[]>>());

        /// <summary>
        /// If all params are present, returns Some T[]
        /// If all params are absent, returns None
        /// Otherwise, throws
        /// </summary>
        public static Option<T[]> InsistAligned<T>(params Option<T>[] os) =>
            MaybeAligned(os)
                .ValueOrDefault(() => throw new Exception("Options are not aligned. (Failure insisting they are either all present or absent)"));
    }

    /// <summary>
    /// Switched this to a struct for space/performance reasons - trivial to switch back
    /// Only snag seems to be that C# (at least, C# 4) doesn't or can't treat or detect it as IEnumerable<T> in all cases,
    /// usually manifests itself around/in a SelectMany, where you have to make an explicit lambda
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable()]
    [DebuggerTypeProxy(typeof(Option<>.DebugProxy))]
    public struct Option<T> // DELIBERATELY NOT implementing IEnumerable : IEnumerable<T>
        : IEquatable<Option<T>>, IComparable<Option<T>>, IComparable
    {
        private class DebugProxy
        {
            public DebugProxy(Option<T> option)
            {
                Valid = option.mValid;
                Data = option.mData;
            }

            public readonly bool Valid;
            public readonly T Data;
        }

        private Option(bool valid, T data)
        {
            mData = data;
            mValid = valid;
#if DEBUG
            mInitialized = true;
#endif
        }

        [Pure]
        public static Option<T> Some(T t) => new Option<T>(true, t);

        [Pure]
        public static Option<T> None() => mNone;

        private sealed class TEnumerator : IEnumerator<T>
        {
            public TEnumerator(T data) // assume valid on start
            {
                mStillValid = true;
                mData = data;
            }

            object IEnumerator.Current
            {
                get { Debug.Assert(!mStillValid); return mData; }
            }

            public T Current { get { Debug.Assert(!mStillValid); return mData; } }

            public bool MoveNext()
            {
                bool r = mStillValid;
                mStillValid = false;
                return r;
            }

            public void Reset()
            {
                mStillValid = true;
            }

            public void Dispose()
            {
            }


            private bool mStillValid;
            private readonly T mData;
        }

        private sealed class TEmpty : IEnumerator<T>
        {
            object IEnumerator.Current
            {
                get { throw new Exception("Can't take current - empty"); }
            }

            public T Current { get { throw new Exception("Can't take current - empty"); } }

            public bool MoveNext() => false;

            public void Reset()
            {
            }

            public void Dispose()
            {
            }
        }

        [Pure]
        public IEnumerator<T> GetEnumerator()
        {
#if DEBUG
            VerifyThisIsInitialized();
#endif
            if (mValid)
            {
                return new TEnumerator(mData);
            }
            return new TEmpty();
        }

        // Named apart from IEnumerable Concatenate - when you're using this, you're saying that one *or* the other can have value; but not both.
        // Consider removing to go orthogonal (i.e. an OAppend, an Option equivalent of Alg.One and Alg.Many)
        [Pure]
        public Option<T> OConcat(Option<T> other)
        {
#if DEBUG
            VerifyThisIsInitialized();
#endif
            if (mValid)
            {
#if DEBUG
                VerifyIsInitialized(other);
#endif
                if (other.mValid)
                {
                    throw new Exception("O-Concatenating 2 non-empty Options - invalid logic check premise and go to IEnumerable in solution if necessary");
                }
                return this;
            }
            else
            {
                return other;
            }
        }



        // Kinda goofy - reassess after the switchover
        public int Count(Func<T, bool> f)
        {
#if DEBUG
            VerifyThisIsInitialized();
#endif

            return Any(f) ? 1 : 0;
        }


        public T Single()
        {
#if DEBUG
            VerifyThisIsInitialized();
#endif
            return Value;
        }

        [Pure]
        public TAccumulate Aggregate<TAccumulate>(TAccumulate seed, Func<TAccumulate, T, TAccumulate> func)
        {
#if DEBUG
            VerifyThisIsInitialized();
#endif
            return mValid ? func(seed, mData) : seed;
        }


        [Pure]
        public bool Any()
        {
#if DEBUG
            VerifyThisIsInitialized();
#endif

            return mValid;
        }

        [Pure]
        public bool All(Func<T, bool> f)
        {
#if DEBUG
            VerifyThisIsInitialized();
#endif

            if (mValid)
            {
                return f(mData);
            }
            return true; // All empty = true
        }

        [Pure]
        public bool Any(Func<T, bool> f)
        {
#if DEBUG
            VerifyThisIsInitialized();
#endif

            if (mValid)
            {
                return f(mData);
            }
            return false;
        }

        [Pure]
        public V Cross<U, V>(Option<U> other, Func<T, U, V> both, Func<Either<T, U>, V> either, Func<V> none)
        {
#if DEBUG
            VerifyThisIsInitialized();
            VerifyIsInitialized(other);
#endif
            if (other.HasValue)
            {
                if (HasValue)
                {
                    return both(Value, other.Value);
                }
                return either(Either<T, U>.FromRight(other.Value));
            }
            if (HasValue)
            {
                return either(Either<T, U>.FromLeft(Value));
            }
            return none();
        }

        public void And<U>(Option<U> other, Action<T, U> both, Action leftMissing, Action rightMissing)
        {
#if DEBUG
            VerifyThisIsInitialized();
            VerifyIsInitialized(other);
#endif
            if (HasValue && other.HasValue)
            {
                both(Value, other.Value);
                return;
            }
            
            if (!HasValue)
            {
                leftMissing();
            }

            if (!other.HasValue)
            {
                rightMissing();
            }
        }


        private static readonly T[] EmptyArray = new T[] {};


        [Pure]
        public T[] ToArray()
        {
#if DEBUG
            VerifyThisIsInitialized();
#endif

            return mValid ? new[] {mData} : EmptyArray;
        }

        [Pure]
        public IEnumerable<T> ToEnumerable()
        {
#if DEBUG
            VerifyThisIsInitialized();
#endif

            return mValid ? new[] {mData} : EmptyArray;
        }

        public int GuardFieldToPreventAutoSerializationToJSON { get { throw new Exception("GuardFieldToPreventAutoSerializationToJSON " + typeof(T).ToString() + " - call .ToArray() on it to get an Array and use *that*"); } }

        public bool HasValue
        {
            get
            {
#if DEBUG
                VerifyThisIsInitialized();
#endif

                return mValid;
            }
        }

        public T Value
        {
            get
            {
#if DEBUG
                VerifyThisIsInitialized();
#endif

                if (! mValid) { throw new Exception("Trying to .Value an Option of type " + typeof(T).ToString()); }; return mData;
            }
        }

        [Pure]
        public Option<U> Select<U>(Func<T, U> transformed)
        {
#if DEBUG
            VerifyThisIsInitialized();
#endif

            return mValid? Option<U>.Some(transformed(mData)) : Option<U>.None();
        }

        [Pure]
        public Option<U> SelectMany<U>(Func<T, Option<U>> transformed)
        {
#if DEBUG
            VerifyThisIsInitialized();
#endif
            return mValid ? transformed(mData) : Option<U>.None();
        }

        // Need this sucker for Query-form linq queries
        [Pure]
        public Option<V> SelectMany<U, V>(Func<T, Option<U>> tu, Func<T, U, V> tuv) => SelectMany(t => tu(t).Select(u => tuv(t, u)));

        [Pure]
        public Option<T> Where(Func<T, bool> predicate)
        {
#if DEBUG
            VerifyThisIsInitialized();
#endif

            return (mValid && predicate(mData)) ? this : mNone;
        }

        [Pure]
        public bool Contains(T v)
        {
#if DEBUG
            VerifyThisIsInitialized();
#endif
            return mValid && (v.Equals(mData));
        }


        /// <summary>
        /// Just too useful to leave out
        /// </summary>
        /// <typeparam name="U"></typeparam>
        /// <param name="?"></param>
        /// <returns></returns>
        [Pure]
        public U Match<U>(Func<T, U> ifPresent, Func<U> ifAbsent)
        {
#if DEBUG
            VerifyThisIsInitialized();
#endif
            return mValid ? ifPresent(mData) : ifAbsent();
        }

        [Pure]
        public void Apply(Action<T> ifPresent, Action ifAbsent)
        {
#if DEBUG
            VerifyThisIsInitialized();
#endif
            if (mValid)
            {
                ifPresent(mData);
            }
            else
            {
                ifAbsent();
            }
        }

        [Pure]
        public void ForEach(Action<T> ifPresent)
        {
#if DEBUG
            VerifyThisIsInitialized();
#endif

            if (mValid)
            {
                ifPresent(mData);
            }
        }

        [Pure]
        public Option<V> Zip<U, V>(Option<U> ou, Func<T, U, V> f)
        {
#if DEBUG
            VerifyThisIsInitialized();
#endif

            return mValid && ou.HasValue ? Option<V>.Some(f(mData, ou.mData)) : Option<V>.None();
        }

        [Pure]
        public Action Runner(Action<T> toRun)
        {
#if DEBUG
            VerifyThisIsInitialized();
#endif

            if (mValid)
            {
                var data = mData;
                return () => toRun(data);
            }
            else
            {
                return () => { };
            }
        }

        private static readonly Option<T> mNone = new Option<T>(false, default(T));

        private readonly T mData;
        private readonly bool mValid;

#if DEBUG
        private readonly bool mInitialized; // going to check this everywhere direct, rather than factor, for performance reasons.

        private void ComplainUninitialized(string id)
        {
            throw new Exception(id + " Option " + typeof(T) + " being used without being initialized, i.e. didn't assign value at this location, not that it was created in malformed state. Wherever it came from is uninitialized and must be fixed");
        }

        private static void VerifyIsInitialized<U>(Option<U> other)
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

        public override int GetHashCode()
        {
#if DEBUG
            VerifyThisIsInitialized();
#endif
            return mValid ? mData.GetHashCode() : 0;
        }

        public override bool Equals(object obj)
        {
#if DEBUG
            VerifyThisIsInitialized();
#endif
            return obj is Option<T> && Equals((Option<T>)obj);
        }

        public bool Equals(Option<T> obj)
        {
#if DEBUG
            VerifyThisIsInitialized();
#endif
            // Doing this without lambdas so as to not deal with 'this' capture.
            if (obj.HasValue != HasValue)
            {
                return false;
            }
            if (HasValue)
            {
                return obj.mData.Equals(mData);
            }
            return true;
        }

        public override string ToString() // could get a bit nuts trying to get consensus happiness with this, make each user spit its own string
        {
#if DEBUG
            VerifyThisIsInitialized();
#endif
            // throw new Exception("Don't try to dig string out of Option " + typeof (T).ToString());

            return mValid ? $"Some {mData}" : "None"; // With new resharper ability, nested-element pretty-printing (which neets ToString() nested) too much a 'pro' for any 'con' of having ToString()
        }

        public int CompareTo(Option<T> other)
        {
#if DEBUG
            VerifyThisIsInitialized();
#endif
            if (mValid)
            {
                if (other.mValid)
                {
#if DEBUG
                    VerifyIsInitialized(other);
#endif
                    // bit ganky - thing is there's no way to statically prove data is comparable
                    var dataAsIComparable = mData as IComparable;
                    if (dataAsIComparable != null)
                    {
                        int r = dataAsIComparable.CompareTo(other.mData);
                        return r;
                    }
                    else
                    {
                        throw new Exception("Trying to compare non-comparable Option core type " + typeof(T));
                    }
                }
                return 1;
            }
            else
            {
                return other.mValid ? -1 : 0;
            }
        }

        public int CompareTo(object other)
        {
#if DEBUG
            VerifyThisIsInitialized();
#endif
            if (other is Option<T>)
            {
#if DEBUG
                VerifyIsInitialized((Option<T>)other);
#endif
                return CompareTo((Option<T>)other);
            }
            throw new Exception("Bad attempt to compare Option of " + typeof(T) + " to ");
        }

        public FSharpOption<T> ToFSOption()
        {
#if DEBUG
            VerifyThisIsInitialized();
#endif
            return mValid ? FSharpOption<T>.Some(mData) : FSharpOption<T>.None;
        }

    }
}
