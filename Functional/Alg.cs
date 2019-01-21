using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using JetBrains.Annotations; // replacement for System.Diagnostics.Contracts to get alternate PureAttribute - in attempt to get [Pure] to be seen by resharper
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization.Json;
using System.Text;
using Microsoft.FSharp.Core;
using Microsoft.FSharp.Collections;

namespace PlayStudios.Functional
{


    public sealed class WriteBackAndResult<TWriteBack, TResult>
    {
        public WriteBackAndResult(TWriteBack writeBack, TResult result)
        {
            WriteBack = writeBack;
            Result = result;
        }

        public readonly TWriteBack WriteBack;
        public readonly TResult Result;

        public WriteBackAndResult<TWriteBackU, TResult> WithTransformedWriteBack<TWriteBackU>(Func<TWriteBack, TWriteBackU> f) => new WriteBackAndResult<TWriteBackU, TResult>(writeBack: f(WriteBack), result: Result);
        public WriteBackAndResult<TWriteBack, TResultU> WithTransformedResult<TResultU>(Func<TResult, TResultU> f) => new WriteBackAndResult<TWriteBack, TResultU>(writeBack: WriteBack, result: f(Result));

        public WriteBackAndResult<TWriteBackU, TResultU> WithTransformedAll<TWriteBackU, TResultU>(Func<TWriteBack, TWriteBackU> fw, Func<TResult, TResultU> fr) =>
            new WriteBackAndResult<TWriteBackU, TResultU>(fw(WriteBack), fr(Result));

        public WriteBackAndResult<TWriteBackU, TResultU> WithTransformedAll<TWriteBackU, TResultU>(Func<TWriteBack, TResult, TWriteBackU> fw, Func<TWriteBack, TResult, TResultU> fr) =>
            new WriteBackAndResult<TWriteBackU, TResultU>(fw(WriteBack, Result), fr(WriteBack, Result));

    }


    // Used to get rid of warning in Resharper of an IEnumerable being double-read.
    public interface IEnumerableReenumerable<out T> : IEnumerable<T>
    {
    }

    public sealed class ReadPoint<T>
    {
        public static ReadPoint<T> InitialReadPoint(IEnumerable<T> enumerable)
        {
            IEnumerator<T> enumerator = enumerable.GetEnumerator();
            bool atEntry = enumerator.MoveNext();
            return new ReadPoint<T>(atEntry ? enumerator : null, atEntry ? enumerator.Current : default(T), !atEntry);
        }

        private ReadPoint(IEnumerator<T> enumerator, T t, bool atEnd)
        {
            mEnumerator = enumerator;
            mNext = null;
            mT = t;
            AtEnd = atEnd;
        }

        private sealed class Reenumerable : IEnumerableReenumerable<T>
        {
            public Reenumerable(Func<IEnumerator<T>> getEnumerator)
            {
                mGetEnumerator = getEnumerator;
            }

            public IEnumerator<T> GetEnumerator() => mGetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => mGetEnumerator();

            private readonly Func<IEnumerator<T>> mGetEnumerator;
        }

        private IEnumerable<T> TakenToEndAssist()
        {
            var current = this;
            while (!current.AtEnd)
            {
                yield return current.Value;
                current = current.Next;
            }
        }

        public IEnumerableReenumerable<T> TakenToEnd() => new Reenumerable(TakenToEndAssist().GetEnumerator);

        /// <summary>
        /// All ReadPoints including this one, and the .AtEnd
        /// </summary>
        /// <returns></returns>
        public IEnumerable<ReadPoint<T>> AllReadPointsDownIncludingEnd()
        {
            var current = this;
            if (!AtEnd)
            {
                do
                {
                    yield return current;
                    current = current.Next;
                } while (!current.AtEnd);
                yield return current;
            }
        }



        public bool AtEnd { get; }

        public T Value
        {
            get
            {
                if (mT == null)
                {
                    throw new Exception("Error: Attempted to read value AtEnd");
                }
                return mT;
            }
        }

        public Option<T> PossibleHere => AtEnd ? Alg.None<T>() : Alg.Some(Value);
        public Option<Tuple<T, ReadPoint<T>>> PossibleHereAndNext => AtEnd ? Alg.None<Tuple<T, ReadPoint<T>>>() : Alg.Some(Tuple.Create(Value, Next));

        public ReadPoint<T> Next
        {
            get
            {
                lock (this) // bit ganky locking on 'this'; but no other always-present objects around
                            // performance tradeoff on what is already a borderline peformance step of making this thread-safe
                {
                    if (AtEnd)
                    {
                        throw new Exception("Attempting to read off end of sequence");
                    }
                    if (mNext == null)
                    {
                        IEnumerator<T> enumerator = mEnumerator;
                        if (enumerator == null)
                        {
                            throw new Exception("Null Enumerator!");
                        }
                        mEnumerator = null;
                        bool atEntry = enumerator.MoveNext();
                        mNext = new ReadPoint<T>(atEntry ? enumerator : null, atEntry ? enumerator.Current : default(T), !atEntry);
                    }
                    return mNext;
                }
            }
        }

        private IEnumerator<T> mEnumerator;
        private readonly T mT;
        private ReadPoint<T> mNext;
    }

    public static class Alg
    {
        // FSharp's Unit, a "Void" of sorts.  It has no value, so creating a singleton instance that all C# code can use will do the job
        // Used to standardize the use of a "Nothing" concept (and replace class Nothing)
        public static readonly Unit UnitValue = (Unit)Activator.CreateInstance(typeof(Unit), true);

        public static FSharpList<T> Cons<T>(T newHead, FSharpList<T> existing) => new FSharpList<T>(newHead, existing);
        public static List1<T> Cons<T>(T newHead, List1<T> existing) => List1.Build(newHead, Some(existing));
        public static List1<T> Cons<T>(T newHead, Option<List1<T>> existing) => List1.Build(newHead, existing);

        public static IEnumerable<T> Cons<T>(T newHead, IEnumerable<T> existing)
        {
            yield return newHead;
            foreach (var v in existing)
            {
                yield return v;
            }
        }

        public static IEnumerable<T> Sole<T>(T soleItem) => new[] { soleItem };

        public static T[] ArrayLiteral<T>(params T[] list) => list;


        public static Func<T, T> GetScrubber<T>()
        {
            Dictionary<T, T> d = new Dictionary<T, T>();
            return
                str =>
                {
                    var r = d.GetValueIfPresent(str);
                    if (r.HasValue)
                    {
                        return r.Value;
                    }
                    d[str] = str;
                    return str;
                };
        }


        // fun - these have value in that A: you can put a lambda's param name with type, and B: don't have to fully qualify return type
        public static Func<R> Func<R>(Func<R> f) => f;
        public static Func<I0, R> Func<I0, R>(Func<I0, R> f) => f;
        public static Func<I0, I1, R> Func<I0, I1, R>(Func<I0, I1, R> f) => f;
        public static Func<I0, I1, I2, R> Func<I0, I1, I2, R>(Func<I0, I1, I2, R> f) => f;
        public static Func<I0, I1, I2, I3, R> Func<I0, I1, I2, I3, R>(Func<I0, I1, I2, I3, R> f) => f;
        public static Func<I0, I1, I2, I3, I4, R> Func<I0, I1, I2, I3, I4, R>(Func<I0, I1, I2, I3, I4, R> f) => f;
        public static Func<I0, I1, I2, I3, I4, I5, R> Func<I0, I1, I2, I3, I4, I5, R>(Func<I0, I1, I2, I3, I4, I5, R> f) => f;
        public static Func<I0, I1, I2, I3, I4, I5, I6, R> Func<I0, I1, I2, I3, I4, I5, I6, R>(Func<I0, I1, I2, I3, I4, I5, I6, R> f) => f;
        public static Func<I0, I1, I2, I3, I4, I5, I6, I7, R> Func<I0, I1, I2, I3, I4, I5, I6, I7, R>(Func<I0, I1, I2, I3, I4, I5, I6, I7, R> f) => f;


        public static Action Action(Action a) => a;
        public static Action<I0> Action<I0>(Action<I0> a) => a;
        public static Action<I0, I1> Action<I0, I1>(Action<I0, I1> a) => a;
        public static Action<I0, I1, I2> Action<I0, I1, I2>(Action<I0, I1, I2> a) => a;
        public static Action<I0, I1, I2, I3> Action<I0, I1, I2, I3>(Action<I0, I1, I2, I3> a) => a;
        public static Action<I0, I1, I2, I3, I4> Action<I0, I1, I2, I3, I4>(Action<I0, I1, I2, I3, I4> a) => a;



        // Y Combinator
        public static Action Y(Func<Action, Action> f) => f(() => Y(f)());
        public static Action<I0> Y<I0>(Func<Action<I0>, Action<I0>> f) => f(n => Y(f)(n));

        public static Func<R> Y<R>(Func<Func<R>, Func<R>> f) => f(() => Y(f)());

        public static Func<I0, R> Y<I0, R>(Func<Func<I0, R>, Func<I0, R>> f) => f(n => Y(f)(n));

        public static Func<I0, I1, R> Y<I0, I1, R>(Func<Func<I0, I1, R>, Func<I0, I1, R>> f) => f((a, b) => Y(f)(a, b));

        public static Func<I0, I1, I2, R> Y<I0, I1, I2, R>(Func<Func<I0, I1, I2, R>, Func<I0, I1, I2, R>> f) => f((a, b, c) => Y(f)(a, b, c));

        public static Func<I0, I1, I2, I3, R> Y<I0, I1, I2, I3, R>(Func<Func<I0, I1, I2, I3, R>, Func<I0, I1, I2, I3, R>> f) => f((a, b, c, d) => Y(f)(a, b, c, d));

        public static Func<I0, I1, I2, I3, I4, R> Y<I0, I1, I2, I3, I4, R>(Func<Func<I0, I1, I2, I3, I4, R>, Func<I0, I1, I2, I3, I4, R>> f) => f((a, b, c, d, e) => Y(f)(a, b, c, d, e));

        public static Func<I0, I1, I2, I3, I4, I5, R> Y<I0, I1, I2, I3, I4, I5, R>(Func<Func<I0, I1, I2, I3, I4, I5, R>, Func<I0, I1, I2, I3, I4, I5, R>> func) => func((a, b, c, d, e, f) => Y(func)(a, b, c, d, e, f));

        public static ReadPoint<T> ReadPoint<T>(IEnumerable<T> list) => Functional.ReadPoint<T>.InitialReadPoint(list);

        public static IEnumerable<int> ZeroUpIntSequence
        {
            get
            {
                for (int i = 0; ; ++i)
                {
                    yield return i;
                }
            }
        }

        public static Action<Action<T>> GetRunToKnownValue<T>(T val) => rw => rw(val);


        public static Action<Action<T>> GetRunToKnowableValue<T>(Func<T> getValue) => rw => rw(getValue());


        /// <summary>
        /// OLC - "Optional List Contribution".  Currently named "When" after the pattern (when predicate (list x)); but maybe it should stay OLC
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="condition"></param>
        /// <param name="onTrue"></param>
        /// <returns></returns>
        public static Option<T> When<T>(bool condition, Func<T> onTrue) => condition ? Some(onTrue()) : None<T>();

        /// <summary>
        /// Allows a way to match based on a bool.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="condition"></param>
        /// <param name="ifTrue"></param>
        /// <param name="ifFalse"></param>
        /// <returns></returns>
        public static T BooleanMatch<T>(bool condition, Func<T> ifTrue, Func<T> ifFalse) =>
            condition ? ifTrue() : ifFalse();

        /// <summary>
        /// Multipart List Contribution - like OLC (optional list contribution)
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="check"></param>
        /// <param name="onTrue"></param>
        /// <returns></returns>
        public static IEnumerable<T> MLC<T>(bool check, Func<IEnumerable<T>> onTrue) => check ? onTrue() : Enumerable.Empty<T>();

        /// <summary>
        /// MLC (multipart list contribution) made to work with options.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="check"></param>
        /// <param name="onTrue"></param>
        /// <returns></returns>
        public static Option<T> MLC<T>(bool check, Func<Option<T>> onTrue) => check ? onTrue() : None<T>();

        /// <summary>
        /// MLC (Multipart List Contribution) made to work with arrays.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="check"></param>
        /// <param name="onTrue"></param>
        /// <returns></returns>
        public static T[] MLC<T>(bool check, Func<T[]> onTrue) => check ? onTrue() : new T[] { };

        [Pure]
        public static IEnumerable<T> MergedOptions<T>(this IEnumerable<Option<T>> optionList) => optionList.SelectMany(x => x.ToEnumerable());

        [Pure]
        public static IEnumerable<U> SelectMany<T, U>(this IEnumerable<T> l, Func<T, Option<U>> f) => l.SelectMany(x => f(x).ToEnumerable());

        [Pure]
        public static IEnumerable<U> SelectMany<T, U>(this IEnumerable<T> l, Func<T, int, Option<U>> f) => l.SelectMany((x, ord) => f(x, ord).ToEnumerable());


        // Sum deliberately named apart such that search-replace can rename them all
        // Easier to turn OSum into Sum than back (which would interfere with IEnumerable Sum)
        [Pure]
        public static int OSum(this Option<int> o) => (o.HasValue) ? o.Value : 0;

        [Pure]
        public static decimal OSum(this Option<decimal> o) => (o.HasValue) ? o.Value : 0;

        [Pure]
        public static double OSum(this Option<double> o) => (o.HasValue) ? o.Value : 0;

        [Pure]
        public static ulong OSum(this Option<ulong> o) => (o.HasValue) ? o.Value : 0;

        [Pure]
        public static long OSum(this Option<long> o) => (o.HasValue) ? o.Value : 0;

        [Pure]
        public static long OSum<T>(this Option<T> o, Func<T, long> f) => (o.HasValue) ? f(o.Value) : 0;


        [Pure]
        public static int OSum<T>(this Option<T> o, Func<T, int> f) => (o.HasValue) ? f(o.Value) : 0;

        [Pure]
        public static decimal OSum<T>(this Option<T> o, Func<T, decimal> f) => (o.HasValue) ? f(o.Value) : 0;

        [Pure]
        public static double OSum<T>(this Option<T> o, Func<T, double> f) => (o.HasValue) ? f(o.Value) : 0;

        /// <summary>
        /// Some - Option with value
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="t"></param>
        /// <returns></returns>
        [Pure]
        public static Option<T> Some<T>(T t) => Option<T>.Some(t);

        /// <summary>
        /// None - Option without value.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        [Pure]
        public static Option<T> None<T>() => Option<T>.None();


        public static Option<T> CappedSourceToOption<T>(IEnumerable<T> source)
        {
            var enumerator = source.GetEnumerator();
            try
            {
                if (enumerator.MoveNext())
                {
                    var t = enumerator.Current;
                    if (enumerator.MoveNext())
                    {
                        throw new Exception("Tried to turn a list > length 1 to an Option");
                    }
                    return Some(t);
                }
                else
                {
                    return None<T>();
                }
            }
            finally
            {
                enumerator.Dispose();
            }
        }
        public static T OrDefault<T>(this Option<T> o, Func<T> ifNone)
        {
            return o.Match(x => x, ifNone);
        }


        /// <summary>
        ///  Feel free to give this a better name.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="makeNew"></param>
        /// <returns></returns>
        /// Left variants independent so as to minimize the CPU cost of the base case
        /// (lock/isvalid/return)
        public static Func<T> GetDeferred<T>(Func<T> makeNew)
        {
            var ifNotExists = // dual-use - this reference's null is the flag that the object exists
                              // but also, clearing it releases reference on both the closure that
                              // creates the new object *and* the lock construct
                new
                {
                    MakeNew = makeNew,      // retained ability to build T
                    TheLock = NewLock()     // factored scoped locking construct
                };

            T valueIfExists = default(T);  // won't use this unless ifNotExists == null
            return
                () =>
                {
                    // assumption - that reference read/write is atomic (for speed) - https://msdn.microsoft.com/en-us/library/aa691278(v=vs.71).aspx
                    var ifNotExistsSnapshot = ifNotExists;

                    if (ifNotExistsSnapshot == null) // if it was null, it is forever null - assumption
                    {
                        return valueIfExists;  // Didn't pay for lock, just return it
                    }
                    return
                        With(
                            ifNotExistsSnapshot.TheLock, // even if ifNotExits may have gone null, the snapshot has been verified as good.
                            () =>
                            {
                                if (ifNotExists == null) // double-checked lock - checking again - ifExists *may* have nulled after last check but while waiting for the lock.
                                {
                                    return valueIfExists; // Payed for lock; but value was created before obtaining it. Return value.
                                }
                                else
                                {
                                    valueIfExists = ifNotExists.MakeNew();     // Noone has built T yet - build it now

                                    ifNotExists = null; // effectively flags that we have a valid value, and releases the
                                                        // no -longer-needed ability closure to make T, and the lock construct

                                    return valueIfExists;  // return newly created/stored value
                                }
                            });
                };
        }

        // Kind of got cornered into this with Config logistics issues
        // Left variants independent so as to minimize the CPU cost of the base case
        // (lock/isvalid/return)
        public static Func<I, T> GetDeferred<I, T>(Func<I, T> makeNew)
        {
            var ifNotExists = // dual-use - this reference's null is the flag that the object exists
                              // but also, clearing it releases reference on both the closure that
                              // creates the new object *and* the lock construct
                new
                {
                    MakeNew = makeNew,      // retained ability to build T
                    TheLock = NewLock()     // factored scoped locking construct
                };

            T valueIfExists = default(T);  // won't use this unless ifNotExists == null
            return
                i =>
                {
                    // assumption - that reference read/write is atomic (for speed) - https://msdn.microsoft.com/en-us/library/aa691278(v=vs.71).aspx
                    var ifNotExistsSnapshot = ifNotExists;

                    if (ifNotExistsSnapshot == null) // if it was null, it is forever null - assumption
                    {
                        return valueIfExists;  // Didn't pay for lock, just return it
                    }
                    return
                        With(
                            ifNotExistsSnapshot.TheLock, // even if ifNotExits may have gone null, the snapshot has been verified as good.
                            () =>
                            {
                                if (ifNotExists == null) // double-checked lock - checking again - ifExists *may* have nulled after last check but while waiting for the lock.
                                {
                                    return valueIfExists; // Payed for lock; but value was created before obtaining it. Return value.
                                }
                                else
                                {
                                    valueIfExists = ifNotExists.MakeNew(i);     // Noone has built T yet - build it now

                                    ifNotExists = null; // effectively flags that we have a valid value, and releases the
                                                        // no -longer-needed ability closure to make T, and the lock construct

                                    return valueIfExists;  // return newly created/stored value
                                }
                            });
                };
        }


        public static Func<int, T> RingArray<T>(Array1<T> original) =>
            original.Length.Let(
                modulus =>
                    Func((int index) =>
                        original[(index % modulus + modulus) % modulus]));


        public static Array1<Array1<T>> TransposedRectangle<T>(Array1<Array1<T>> original)
        {
            var lengthOfFirst = original.First.Length;

            if (original.Rest.Any(x => x.Length != lengthOfFirst))
            {
                throw new Exception("Length mismatch - has to be rectangle");
            }

            return
                Array1.Build(
                    Array1.Build(
                        original.First.First,
                        original.Rest.Select(x => x.First)),
                    Enumerable.Range(0, original.First.Rest.Length).Select(
                        ind =>
                            original.Rest.Select(x => x.Rest[ind]).ToArray()).Select(
                                (arr, ind2) =>
                                    Array1.Build(original.First.Rest[ind2], arr)));
        }

        public static IEnumerable<IEnumerable<T>> TransposedRectangle<T>(IEnumerable<IEnumerable<T>> original)
        {
            var orp = ReadPoint(original);
            if (!orp.AtEnd)
            {
                int numHorizontalInOriginal = orp.Value.Count();
                // We cache this
                var originalsAsArrays = ReadPoint(original.Select(row =>
                {
                    var r = row.ToArray();
                    if (r.Length != numHorizontalInOriginal)
                    {
                        throw new Exception("Row size mismatch");
                    }
                    return r;
                })).TakenToEnd();
                return Enumerable.Range(0, numHorizontalInOriginal).Select(ordinal => originalsAsArrays.Select(row => row[ordinal]));
            }
            else
            {
                return new IEnumerable<T>[] { };
            }
        }

        public static T[,] PopulatedSquare<T>(IEnumerable<IEnumerable<T>> contents)
        {
            var contentsArrayArray = contents.Select(x => x.ToArray()).ToArray();
            var widths = new HashSet<int>(contentsArrayArray.Select(x => x.Length));
            var width = widths.Single();
            var height = contentsArrayArray.Length;
            var r = new T[width, height];
            for (int x = 0; x < width; ++x)
            {
                for (int y = 0; y < height; ++y)
                {
                    r[x, y] = contentsArrayArray[y][x];
                }
            }
            return r;
        }

        /// <summary>
        /// Make infinite list of the provided item
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="t"></param>
        /// <returns></returns>
        public static IEnumerable<T> Infinitely<T>(T t)
        {
            for (;;)
            {
                yield return t;
            }
        }

        public static IEnumerable<T> Infinitely<T>(Func<T> func)
        {
            for (;;)
            {
                yield return func();
            }
        }

        public static Out GuardedLoop<In, Out>(In initial, Func<In, Either<In, Out>> f, int sanityGuardMaxLoops, Func<string> getIdentity)
        {
            In current = initial;
            int numIterations = 0;
            for (;;)
            {
                var v = f(current);
                var r = v.RightIfPresent();
                if (r.HasValue)
                {
                    return r.Value;
                }
                current = v.LeftIfPresent().Value;
                ++numIterations;
                if (numIterations > sanityGuardMaxLoops)
                {
                    throw new Exception("Guarded Loop failure at " + numIterations + " iterations for " + getIdentity());
                }
            }
        }


        // On the chopping block.
        public static Out GuardedLoop<Out>(Func<Option<Out>> returnSomeForExit, int sanityGuardMaxLoops, Func<string> getIdentity) =>
            GuardedLoop<Unit, Out>(
                UnitValue,
                dummy => returnSomeForExit().Match(Either<Unit, Out>.FromRight, () => Either<Unit, Out>.FromLeft(UnitValue)),
                    sanityGuardMaxLoops,
                getIdentity);

        private static IEnumerable<Tuple<I0, I1>> Zip<I0, I1>(IEnumerable<I0> i0, IEnumerable<I1> i1)
        {
            var e0 = i0.GetEnumerator();
            var e1 = i1.GetEnumerator();
            for (;;)
            {
                if (!e0.MoveNext())
                {
                    break;
                }
                if (!e1.MoveNext())
                {
                    break;
                }
                yield return Tuple.Create(e0.Current, e1.Current);
            }
        }

        private static IEnumerable<Tuple<I0, I1, I2>> Zip<I0, I1, I2>(IEnumerable<I0> i0, IEnumerable<I1> i1, IEnumerable<I2> i2)
        {
            var e0 = i0.GetEnumerator();
            var e1 = i1.GetEnumerator();
            var e2 = i2.GetEnumerator();
            for (;;)
            {
                if (!e0.MoveNext())
                {
                    break;
                }
                if (!e1.MoveNext())
                {
                    break;
                }
                if (!e2.MoveNext())
                {
                    break;
                }
                yield return Tuple.Create(e0.Current, e1.Current, e2.Current);
            }
        }

        public static IEnumerable<O> Map<I0, I1, O>(Func<I0, I1, O> f, IEnumerable<I0> i0, IEnumerable<I1> i1) =>
            // Calling zip, optimally would *reimplement* Zip2 here so as to not construct the tuple
            Zip(i0, i1).Select(x => f(x.Item1, x.Item2));

        public static IEnumerable<O> Map<I0, I1, I2, O>(Func<I0, I1, I2, O> f, IEnumerable<I0> i0, IEnumerable<I1> i1, IEnumerable<I2> i2) =>
            // Calling zip, optimally would *reimplement* Zip2 here so as to not construct the tuple
            Zip(i0, i1, i2).Select(x => f(x.Item1, x.Item2, x.Item3));

        public static byte[] SerializedToBytes<T>(T objectToSerialize)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                BinaryFormatter bformatter = new BinaryFormatter();
                bformatter.Serialize(ms, objectToSerialize);
                // yeah, limiting this to "int" length.
                return ms.GetBuffer().Take((int)ms.Position).ToArray();
            }
        }

        public static Action<Action<Stream>> StreamSourceFromBytes(byte[] bytes) =>
            rw =>
            {
                using (MemoryStream stream = new MemoryStream(bytes))
                {
                    rw(stream);
                }
            };


        public static object DeserializedFromBytes(byte[] bytes)
        {
            using (MemoryStream ms = new MemoryStream(bytes))
            {
                BinaryFormatter bformatter = new BinaryFormatter();
                object ro = bformatter.Deserialize(ms);
                return ro;
            }
        }


        public static A DataContractDeserializedJSON<A>(string str)
        {
            using (MemoryStream memoryStream = new MemoryStream(Encoding.Default.GetBytes(str)))
            {
                var serializer = new DataContractJsonSerializer(typeof(A));
                return (A)serializer.ReadObject(memoryStream);
            }
        }

        public static string DataContractSerializedJSON<A>(A obj)
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                (new DataContractJsonSerializer(typeof(A))).WriteObject(memoryStream, obj);
                return Encoding.Default.GetString(memoryStream.ToArray());
            }
        }

        public static A DataContractDeserializedXML<A>(string str)
        {
            using (MemoryStream memoryStream = new MemoryStream(Encoding.Default.GetBytes(str)))
            {
                var serializer = new DataContractSerializer(typeof(A));
                return (A)serializer.ReadObject(memoryStream);
            }
        }

        public static string DataContractSerializedXML<A>(A obj)
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                (new DataContractSerializer(typeof(A))).WriteObject(memoryStream, obj);
                return Encoding.Default.GetString(memoryStream.ToArray());
            }
        }



        public static T DeserializedFromBytes<T>(byte[] bytes) => (T)DeserializedFromBytes(bytes);


        public static IDictionary<K, V> FilledDictionary<K, V>(IEnumerable<Tuple<K, V>> keyValues)
        {
            try
            {
                return keyValues.ToDictionary(kv => kv.Item1, kv => kv.Item2);
            }
            catch (Exception)
            {
                // find out if this was a dupes thing.
                var failKeys = keyValues.Select(x => x.Item1).GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key + " with " + g.Count()).ToArray();
                if (failKeys.Any())
                {
                    var errorMessage = "FilledDictionary fail - duplicate key (s) : " + MergedStrings(Intersperse(", ", failKeys));
                    throw new ArgumentException(errorMessage);
                }
                throw;
            }
        }

        public static void AddOrChangeElement<Key, Value>(IDictionary<Key, Value> dictionary, Key key, Func<Value> getNewValue, Func<Value, Value> getChangedValue)
        {
            if (dictionary.ContainsKey(key))
            {
                dictionary[key] = getChangedValue(dictionary[key]);
            }
            else
            {
                dictionary.Add(key, getNewValue());
            }
        }


        public struct InLock
        {
            public InLock(object l)
            {
                mLock = l;
            }

            public void Wait() => Monitor.Wait(mLock);
            public void Wait(TimeSpan timeout) => Monitor.Wait(mLock, timeout);

            public void PulseAll() => Monitor.PulseAll(mLock);
            public void PulseOne() => Monitor.Pulse(mLock);

            private readonly object mLock; // sending this through instead of 2 functions for micro-efficiency
        }



        /// <summary>
        /// Makes an executer for a function that "locks" execution of the inner function.
        /// An amusing abstraction of a lock, use with "With" to have the run code be able
        /// to return values.
        /// </summary>
        /// <returns></returns>
        public static Action<Action<InLock>> NewLockWithControl()
        {
            object theLock = new string[] { }; // can be anything
            var inLock = new InLock(theLock);

            return
                tr =>
                {
                    lock (theLock)
                    {
                        tr(inLock);
                    }
                };
        }

        public static Action<Action> NewLock()
        {
            object theLock = new string[] { }; // can be anything
            return
                tr =>
                {
                    lock (theLock)
                    {
                        tr();
                    }
                };
        }


        /// <summary>
        /// When you're tired of newing up a "Stringbuilder" for the 4 billionth time, you use this instead.
        /// </summary>
        /// <param name="strings"></param>
        /// <returns></returns>
        public static string MergedStrings(IEnumerable<string> strings)
        {
            StringBuilder r = new StringBuilder();
            foreach (string s in strings)
            {
                r.Append(s);
            }
            return r.ToString();
        }

        /// <summary>
        /// Give it a lazy-list of chars, it returns a string.
        /// </summary>
        /// <param name="chars"></param>
        /// <returns></returns>
        public static string MergedChars(IEnumerable<char> chars)
        {
            StringBuilder r = new StringBuilder();
            foreach (char c in chars)
            {
                r.Append(c);
            }
            return r.ToString();
        }

        public static Option<R> SplitAroundDiscovery<R>(this string toSearch, string toFind, Func<string, string, R> onBeforeAndAfter)
        {
            var index = toSearch.IndexOf(toFind);
            return
                When(index >= 0, () => onBeforeAndAfter(toSearch.Substring(0, index), toSearch.Substring(index + toFind.Length)));
        }

        // A useful variant
        public static Option<R> SplitAroundDiscoveryM<R>(this string toSearch, string toFind, Func<string, string, Option<R>> onBeforeAndAfter)
        {
            var index = toSearch.IndexOf(toFind);
            return When(index >= 0, () => onBeforeAndAfter(toSearch.Substring(0, index), toSearch.Substring(index + toFind.Length))).SelectMany(x => x);
        }

        /// <summary>
        /// Returns a list with "toInject" in between the elements in "originalList", i.e. 1,2,3 turns into 1,X,2,X,3
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="toInject"></param>
        /// <param name="originalList"></param>
        /// <returns></returns>
        public static IEnumerable<T> Intersperse<T>(T toInject, IEnumerable<T> originalList)
        {
            bool first = true;
            foreach (T t in originalList)
            {
                if (first)
                {
                    first = false;
                }
                else
                {
                    yield return toInject;
                }

                yield return t;
            }
        }

        public static IEnumerable<T> Append<T>(this IEnumerable<T> sequence, Func<T> appender)
        {
            foreach (T t in sequence)
            {
                yield return t;
            }

            yield return appender();
        }

        public static IEnumerable<T> Append<T>(this IEnumerable<T> sequence, Func<IEnumerable<T>> appender)
        {
            foreach (T t in sequence)
            {
                yield return t;
            }

            foreach (T t in appender())
            {
                yield return t;
            }
        }




        public static OutFinal WithCore<In, OutInner, OutInterim, OutFinal>(Action<Func<In, OutInner>> rw, Func<In, OutInterim> tr, Func<OutInterim, OutInner> fi, Func<OutInterim, int, OutFinal> returnOnResult)
        {
            OutInterim result = default(OutInterim);
            int setCount = 0;
            rw(
                i =>
                {
                    result = tr(i);
                    ++setCount;
                    return fi(result);
                });

            return returnOnResult(result, setCount);
        }




        public static WriteBackAndResult<TWriteBack, TResult> WriteBackAndResult<TWriteBack, TResult>(TWriteBack writeBack, TResult result) => new WriteBackAndResult<TWriteBack, TResult>(writeBack: writeBack, result: result);


        // Clone of splitReturn; but uses a more factored core
        public static TReturn SplitReturn<TTransIn, TTransOut, TReturn>(Action<Func<TTransIn, TTransOut>> wf, Func<TTransIn, WriteBackAndResult<TTransOut, TReturn>> f) =>
            WithCore<TTransIn, TTransOut, WriteBackAndResult<TTransOut, TReturn>, TReturn>(
                wf,
                f,
                x => x.WriteBack,
                (r, setCount) =>
                {
                    if (!(setCount == 1))
                    {
                        throw new Exception("SplitReturn<> failure, setCount = " + setCount);
                    }

                    return r.Result;
                });

#if false
        /// <summary>
        /// "With" basically is used as the combiner of a function that takes a function that takes a thing but returns nothing
        /// (A "policy", e.g. very useful for communicating a database transaction or the like) and a function that takes the protected
        /// input and returns some output.
        /// Basically, this abstracts away a lot of cases where you have to make some uninitialized "result", then populate it from within the
        /// context.
        /// The "With" functions factor away all the boilerplate for this, including checks where relevant, to make sure that the "returning"
        /// function is actually called, and called only once, so that the return value is initialized only once.
        /// </summary>
        /// <typeparam name="In"></typeparam>
        /// <typeparam name="Out"></typeparam>
        /// <param name="rw"></param>
        /// <param name="tr"></param>
        /// <param name="returnOnResult"></param>
        /// <returns></returns>
        public static Out WithCore<In, Out>(Action<Action<In>> rw, Func<In, Out> tr, Func<Out, int, Out> returnOnResult)
        {
            Out result = default(Out);
            int setCount = 0;
            rw(
                i =>
                {
                    result = tr(i);
                    ++setCount;
                });

            return returnOnResult(result, setCount);
        }
#endif

        // Too useful to leave out, though maybe reconsider it being instance method and .Select
        public static Action<Action<U>> Select<T, U>(this Action<Action<T>> aa, Func<T, U> f) => rw => aa(t => rw(f(t)));

        public static Action<Action<Option<T>>> AASinkOption<T>(this Option<Action<Action<T>>> oaa) =>
                oaa.Match(
                    aa => new Action<Action<Option<T>>>(rw => aa(t => rw(Some(t)))),
                    () => new Action<Action<Option<T>>>(rw => rw(None<T>())));

        /// <summary>
        /// O-input case
        /// </summary>
        /// <typeparam name="Out"></typeparam>
        /// <param name="rw"></param>
        /// <param name="tr"></param>
        /// <returns></returns>
        public static Out With<Out>(Action<Action> rw, Func<Out> tr) =>
            With<Unit, Out>(
                trw => rw(() => trw(UnitValue)),
                nothing => tr());

        public static Out With<In, Out>(Action<Action<In>> rw, Func<In, Out> tr) =>
            WithCore(
                f => { rw(x => f(x)); },
                tr,
                _ => UnitValue,
                (r, setCount) =>
                {
                    if (!(setCount == 1))
                    {
                        throw new Exception("With<> failure, setCount = " + setCount);
                    }

                    return r;
                });

        public static void With<In>(Action<Action<In>> rw, Action<In> tr) =>
            With(
                rw,
                inParam =>
                {
                    tr(inParam);
                    return UnitValue;
                });

        public static Out With<I0, I1, Out>(Action<Action<I0, I1>> rw, Func<I0, I1, Out> tr) => With(rwa => rw(Curry(rwa)), UnCurry(tr));

        public static Out With<I0, I1, I2, Out>(Action<Action<I0, I1, I2>> rw, Func<I0, I1, I2, Out> tr) => With(rwa => rw(Curry3(rwa)), UnCurry(tr));

 
        public static void Populate<T>(this T[] arr, T value)
        {
            for (int i = 0; i != arr.Length; ++i)
            {
                arr[i] = value;
            }
        }

        public static void Populate<T>(this T[,] arr, T value)
        {
            for (int i = 0; i != arr.GetLength(0); ++i)
            {
                for (int j = 0; j != arr.GetLength(1); ++j)
                {
                    arr[i, j] = value;
                }
            }
        }

        /// <summary>
        /// Really just used to get the ability to put statements mid-expression.
        /// Could probably use a better name.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="get"></param>
        /// <returns></returns>
        public static T Let<T>(Func<T> get) => get();

        public static U Let<T, U>(this T original, Func<T, U> transformed) => transformed(original);

        public static void Let<T>(this T original, Action<T> run) => run(original);

        public static U Let<I0, I1, U>(this Tuple<I0, I1> original, Func<I0, I1, U> transformed) => transformed(original.Item1, original.Item2);

        public static void Let<I0, I1>(this Tuple<I0, I1> original, Action<I0, I1> run) => run(original.Item1, original.Item2);


        public static U Let<I0, I1, I2, U>(this Tuple<I0, I1, I2> original, Func<I0, I1, I2, U> transformed) => transformed(original.Item1, original.Item2, original.Item3);

        public static void Let<I0, I1, I2>(this Tuple<I0, I1, I2> original, Action<I0, I1, I2> run) => run(original.Item1, original.Item2, original.Item3);


        // Maybe different name for this usage pattern
        public static U Let<A, B, U>(A a, B b, Func<A, B, U> transformed) => transformed(a, b);

        public static IEnumerable<IEnumerable<T>> FirstPrimary<T>(this T[,] items)
        {
            int l0 = items.GetLength(0);
            int l1 = items.GetLength(1);
            return Enumerable.Range(0, l0).Select(i => Enumerable.Range(0, l1).Select(j => items[i, j]));
        }

        public static IEnumerable<IEnumerable<T>> SecondPrimary<T>(this T[,] items)
        {
            int l0 = items.GetLength(0);
            int l1 = items.GetLength(1);
            return Enumerable.Range(0, l1).Select(j => Enumerable.Range(0, l0).Select(i => items[i, j]));
        }

        // TODO with C#7.3 make this enum constraint
        public static TEnum[] GetEnumValues<TEnum>() where TEnum : struct =>
            (TEnum[])Enum.GetValues(typeof(TEnum));

        public static Either<Exception, T> TryWithException<T>(Func<T> tryForSuccess)
        {
            try
            {
                return Either<Exception, T>.FromRight(tryForSuccess());
            }
            catch (Exception e)
            {
                return Either<Exception, T>.FromLeft(e);
            }
        }

        public static Option<Exception> TryWithException(Action tryForSuccess) =>
            TryWithException(
                () =>
                {
                    tryForSuccess();
                    return UnitValue;
                }).LeftIfPresent();

        public static Option<T> Try<T>(Func<T> tryForSuccess) =>
            TryWithException(tryForSuccess).RightIfPresent();

        public static void Try(Action tryForSuccess) =>
            TryWithException(tryForSuccess);

        public static Option<short> TryParseShort(this string s)
        {
            short result = -1;
            return short.TryParse(s, out result) ? Some(result) : None<short>();
        }

        public static Option<int> TryParseInt(this string s)
        {
            int result = -1;
            return int.TryParse(s, out result) ? Some(result) : None<int>();
        }

        public static Option<Int64> TryParseInt64(this string s)
        {
            Int64 result = -1;
            return Int64.TryParse(s, out result) ? Some(result) : None<Int64>();
        }

        public static Option<ulong> TryParseULong(this string s)
        {
            ulong result = 0;
            return ulong.TryParse(s, out result) ? Some(result) : None<ulong>();
        }

        public static Option<decimal> TryParseDecimal(this string cell)
        {
            decimal result;
            return decimal.TryParse(cell, out result) ? Some(result) : None<decimal>();
        }

        public static Option<float> TryParseFloat(this string s)
        {
            float result;
            return float.TryParse(s, out result) ? Some(result) : None<float>();
        }

        public static Option<double> TryParseDouble(this string s)
        {
            double result;
            return double.TryParse(s, out result) ? Some(result) : None<double>();
        }

        public static Option<T> TryParseEnum<T>(this string s)
            where T : struct
        {
            T result;
            return Enum.TryParse(s, true, out result) ? Some(result) : None<T>();
        }

        public static Option<DateTimeOffset> TryParseUtcDateTimeOffset(this string s)
        {
            DateTimeOffset result;
            return DateTimeOffset.TryParse(s, null, System.Globalization.DateTimeStyles.AssumeUniversal, out result) ? Some(result) : None<DateTimeOffset>();
        }

        // TODO - Implement this in terms of CrossProduct
        public static IEnumerable<IEnumerable<T>> AllChoiceSetPermutations<T>(IEnumerable<IEnumerable<T>> _choiceSets)
        {
            var choiceSets = _choiceSets.Memoized();
            if (!choiceSets.Any())
            {
                yield return new T[] { };
            }
            else
            {
                foreach (T first in choiceSets.First())
                {
                    foreach (var inner in AllChoiceSetPermutations<T>(choiceSets.Skip(1)))
                    {
                        yield return new T[] { first }.Concat(inner);
                    }
                }
            }
        }

        // TODO - Implement this in terms of CrossProduct
        public static IEnumerable<T[]> AllIdentityPermutations<T>(T[] items)
        {
            if (items.Length == 1)
            {
                yield return items;
            }
            else
            {
                for (int i = 0; i < items.Length; ++i)
                {
                    T front = items[i];
                    foreach (var inner in AllIdentityPermutations(items.Where((x, ind) => ind != i).ToArray()))
                    {
                        yield return new[] { front }.Concat(inner).ToArray();
                    }
                }
            }
        }

        public static IEnumerable<T[]> AllIdentityPermutationsCapped<T>(T[] items, int count)
        {
            if (count == 0)
            {
                yield return new T[] { };
            }
            else
            {
                for (int i = 0; i < items.Length; ++i)
                {
                    T front = items[i];
                    foreach (var inner in AllIdentityPermutationsCapped(items.Where((x, ind) => ind != i).ToArray(), count - 1))
                    {
                        yield return new[] { front }.Concat(inner).ToArray();
                    }
                }
            }
        }

        public static IEnumerable<int> BitsSetLowestFirst(int v)
        {
            uint current = (uint)v; // for the fully bit-packed/negative case - without uint it'll keep trailing in a 1 and run forever
            int r = 0;
            while (current != 0)
            {
                if ((current & 1) != 0)
                {
                    yield return r;
                }
                ++r;
                current = current >> 1;
            }
        }


        public static IDictionary<K, V> ToDictionary<K, V>(this IEnumerable<Tuple<K, V>> entries) => entries.ToDictionary(x => x.Item1, x => x.Item2);

        public static IDictionary<TKey, TValue> ToDictionary<TKey, TValue>(this IEnumerable<(TKey key, TValue value)> entries) => entries.ToDictionary(x => x.key, x => x.value);

        public static TResult ToUniqueSingle<TResult, TComparableInterim>(IEnumerable<TResult> original, Func<TResult, TComparableInterim> toInterim, Func<TComparableInterim, TResult> comparableToResult, Func<TResult, string> getStringRepresentationForErrorGenPurposes)
            // picked IComparable core as Tuple has it
            where TComparableInterim : IComparable
        {
            var finals = original.Select(toInterim).GroupBy(x => x).Select(g => Tuple.Create(comparableToResult(g.Key), g.Count())).ToArray();
            if (finals.Length == 1)
            {
                return finals.Single().Item1;
            }

            if (finals.Length == 0)
            {
                throw new Exception("No UniqueSingle of type " + typeof(TResult) + " as input list was empty");
            }

            throw new Exception("UniqueSingle not unique, groups: " + MergedStrings(Intersperse(", ", finals.Select(t => "(" + getStringRepresentationForErrorGenPurposes(t.Item1) + ", " + t.Item2 + ")"))));
        }


        /// <summary>
        /// Takes a list of lists, then gives every combination from each list.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="inputLists"></param>
        /// <returns></returns>
        public static IEnumerable<IEnumerable<T>> CrossProduct<T>(IEnumerable<IEnumerable<T>> inputLists)
        {
            var lists = inputLists.ToArray();
            IEnumerator<T>[] enumerators = new IEnumerator<T>[lists.Length];
            bool allHaveContents = true;
            for (int i = 0; i < enumerators.Length; ++i)
            {
                IEnumerator<T> enumerator = lists[i].GetEnumerator();
                allHaveContents = allHaveContents && enumerator.MoveNext();
                enumerators[i] = enumerator;
            }
            if (allHaveContents)
            {
                bool haveAValue = false;
                do
                {
                    yield return enumerators.Select(x => x.Current).ToArray();
                    for (int ei = 0; ei < enumerators.Length; ++ei)
                    {
                        if (enumerators[ei].MoveNext())
                        {
                            haveAValue = true;
                            break;
                        }
                        else
                        {
                            var newEnumerator = lists[ei].GetEnumerator();
                            newEnumerator.MoveNext();
                            enumerators[ei] = newEnumerator;
                        }
                    }
                } while (haveAValue);
            }
        }


        /// <summary>
        /// Basically Haskell's Iterate function - generate an infinite list from the prior element
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="f"></param>
        /// <param name="initial"></param>
        /// <returns></returns>
        public static IEnumerable<T> Iterate<T>(Func<T, T> f, T initial)
        {
            T r = initial;
            for (;;)
            {
                yield return r;
                r = f(r);
            }
            // ReSharper disable once IteratorNeverReturns
            // . . . as intended, this is to generate an infinite list
        }

        /// <summary>
        /// Basically Haskell's ScanL
        /// </summary>
        /// <typeparam name="A"></typeparam>
        /// <typeparam name="B"></typeparam>
        /// <param name="f"></param>
        /// <param name="initial"></param>
        /// <param name="items"></param>
        /// <returns></returns>
        public static IEnumerable<A> ScanL<A, B>(Func<A, B, A> f, A initial, IEnumerable<B> items)
        {
            A current = initial;
            yield return current;
            foreach (B b in items)
            {
                current = f(current, b);
                yield return current;
            }
        }

        public static IEnumerable<A> ScanL1<A>(Func<A, A, A> f, IEnumerable<A> items)
        {
            A current = default(A);
            bool isFirst = true;
            foreach (A a in items)
            {
                if (isFirst)
                {
                    isFirst = false;
                    current = a;
                }
                else
                {
                    current = f(current, a);
                }
                yield return current;
            }
        }

        public static IEnumerable<T> SkipAndTakeWhile<T>(Func<T, bool> filter, IEnumerable<T> line) => line.SkipWhile(x => !filter(x)).TakeWhile(filter);


        /// <summary>
        /// Extracts the first contiguous 'chunk' or sequence that satisfies the filter. Analogous to SkipAndTakeWhile except it's an extension method.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="filter">The filter.</param>
        /// <param name="line">The line.</param>
        /// <returns></returns>
        public static IEnumerable<T> ExtractFirstChunk<T>(this IEnumerable<T> line, Func<T, bool> filter) => line.SkipWhile(x => !filter(x)).TakeWhile(filter);


        // Note on Curry/Uncurry - deliberately distinguishing things like Curry3+ with a number in the field
        // From the purer forms.  This eliminates the ambiguity there you may want to roll up on only the outer tuple.
        // Currently Uncurry makes the assumption that you want to fully tupleise on the function.  This may be less critical
        // than the Curry case - I consider it an open issue.
        // Both Curry/Uncurry and the forms provided - measured against a standard of tactile useability - are ultimately an open question in C#
        public static Action<Tuple<I0, I1>> UnCurry<I0, I1>(Action<I0, I1> f) => t => f(t.Item1, t.Item2);

        public static Func<Tuple<I0, I1>, O> UnCurry<I0, I1, O>(Func<I0, I1, O> f) => t => f(t.Item1, t.Item2);

        public static Action<Tuple<Tuple<I0, I1>, I2>> UnCurry<I0, I1, I2>(Action<I0, I1, I2> f) => t => f(t.Item1.Item1, t.Item1.Item2, t.Item2);

        public static Func<Tuple<Tuple<I0, I1>, I2>, O> UnCurry<I0, I1, I2, O>(Func<I0, I1, I2, O> f) => t => f(t.Item1.Item1, t.Item1.Item2, t.Item2);


        public static Action<I0, I1> Curry<I0, I1>(Action<Tuple<I0, I1>> f) => (i0, i1) => f(Tuple.Create(i0, i1));

        public static Action<I0, I1, I2> Curry3<I0, I1, I2>(Action<Tuple<Tuple<I0, I1>, I2>> f) => (i0, i1, i2) => f(Tuple.Create(Tuple.Create(i0, i1), i2));

        public static Func<I0, I1, O> Curry<I0, I1, O>(Func<Tuple<I0, I1>, O> f) => (i0, i1) => f(Tuple.Create(i0, i1));

        public static Func<I0, I1, I2, O> Curry3<I0, I1, I2, O>(Func<Tuple<Tuple<I0, I1>, I2>, O> f) => (i0, i1, i2) => f(Tuple.Create(Tuple.Create(i0, i1), i2));


        public static Either<L, R> Choice<L, R>(bool condition, Func<L> onTrue, Func<R> onFalse) => condition ? Either<L, R>.FromLeft(onTrue()) : Either<L, R>.FromRight(onFalse());

        public static Either<L, R> One<L, R>(Option<L> l, Option<R> r)
        {
            if (l.HasValue)
            {
                Debug.Assert(!r.HasValue);
                return Either<L, R>.FromLeft(l.Value);
            }
            return Either<L, R>.FromRight(r.Value);
        }

        public static Union3<I0, I1, I2> One<I0, I1, I2>(Option<I0> i0, Option<I1> i1, Option<I2> i2)
        {
            Debug.Assert(((i0.HasValue ? 1 : 0) + (i1.HasValue ? 1 : 0) + (i2.HasValue ? 1 : 0)) == 1);

            if (i0.HasValue)
            {
                return Union3<I0, I1, I2>.From0(i0.Value);
            }
            if (i1.HasValue)
            {
                return Union3<I0, I1, I2>.From1(i1.Value);
            }
            return Union3<I0, I1, I2>.From2(i2.Value);
        }

        /// <summary>
        /// Returns an Optional Either of the left or right elements. Throws an error if there is more than one value available.
        /// </summary>
        /// <typeparam name="L"></typeparam>
        /// <typeparam name="R"></typeparam>
        /// <param name="l">The l.</param>
        /// <param name="r">The r.</param>
        /// <returns></returns>
        public static Option<Either<L, R>> MaxOne<L, R>(Option<L> l, Option<R> r)
        {
            if (l.HasValue)
            {
                Debug.Assert(!r.HasValue);
                return Some(Either<L, R>.FromLeft(l.Value));
            }
            if (r.HasValue)
            {
                return Some(Either<L, R>.FromRight(r.Value));
            }
            return None<Either<L, R>>();
        }

        public static Option<Tuple<Option<I0>, Option<I1>>> OnAtLeastOne<I0, I1>(Option<I0> i0, Option<I1> i1) => When(i0.HasValue || i1.HasValue, () => Tuple.Create(i0, i1));

        // A common case coming up, too convenient because of the type inference you otherwise need to do without to build the 2 either cases.
        public static Either<L, R> FirstPresent<L, R>(Option<L> l, R r) => l.Match(Either<L, R>.FromLeft, () => Either<L, R>.FromRight(r));
        public static Either<L, R> FirstPresentL<L, R>(Option<L> l, Func<R> r) => l.Match(Either<L, R>.FromLeft, () => Either<L, R>.FromRight(r()));


        public static Option<Union3<I0, I1, I2>> MaxOne<I0, I1, I2>(Option<I0> i0, Option<I1> i1, Option<I2> i2)
        {
            int count = (((i0.HasValue ? 1 : 0) + (i1.HasValue ? 1 : 0) + (i2.HasValue ? 1 : 0)));
            if (count == 0)
            {
                return None<Union3<I0, I1, I2>>();
            }

            if (i0.HasValue)
            {
                return Some(Union3<I0, I1, I2>.From0(i0.Value));
            }
            if (i1.HasValue)
            {
                return Some(Union3<I0, I1, I2>.From1(i1.Value));
            }
            return Some(Union3<I0, I1, I2>.From2(i2.Value));
        }


        public static Func<A, C> Composed<A, B, C>(Func<B, C> bc, Func<A, B> ab) => a => bc(ab(a));


        public static FSharpMap<TKey, TValue> MapOfSeq<TKey, TValue>(IEnumerable<(TKey, TValue)> vt) => vt.ToFSharpMap();

        // For want of a better name




        public static long GreatestCommonDenominator(long first, long second, FSharpList<long> rest)
        {
            // http://en.wikipedia.org/wiki/Euclidean_algorithm

            var a = first;
            var b = second;
            var stock = rest;
            for (;;)
            {
                while (a != b)
                {
                    if (a > b)
                    {
                        a = a - b;
                    }
                    else
                    {
                        b = b - a;
                    }
                }
                // a is the answer locally
                if (stock.IsEmpty)
                {
                    return a;
                }
                else
                {
                    b = stock.Head;
                    stock = stock.Tail;
                }
            }
        }

        public static decimal GreatestCommonDenominatorD(decimal first, decimal second, FSharpList<decimal> rest)
        {
            // http://en.wikipedia.org/wiki/Euclidean_algorithm

            var a = first;
            var b = second;
            var stock = rest;
            for (;;)
            {
                while (a != b)
                {
                    if (a > b)
                    {
                        a = a - b;
                    }
                    else
                    {
                        b = b - a;
                    }
                }
                // a is the answer locally
                if (stock.IsEmpty)
                {
                    return a;
                }
                else
                {
                    b = stock.Head;
                    stock = stock.Tail;
                }
            }
        }


        // https://stackoverflow.com/questions/1646807/quick-and-simple-hash-code-combinations
        public static int CombinedListOfHashCodes(IEnumerable<int> hashCodes)
        {
            unchecked
            {
                int hash = 17;
                foreach (int c in hashCodes)
                {
                    hash = hash * 31 + c;
                }

                return hash;
            }
        }
        
        /*
        public static T[][] ToArray<T>(this IEnumerable<IEnumerable<T>> arr) =>
            arr.Select(x => x.ToArray()).ToArray();

        public static T[][][] ToArray<T>(this IEnumerable<IEnumerable<IEnumerable<T>>> arr) =>
            arr.Select(x => 
                x.Select(y => 
                    y.ToArray())
                    .ToArray())
                .ToArray();
                */

        /// <summary>
        /// Like Zip, but with more 'Zap'
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="U"></typeparam>
        /// <typeparam name="V"></typeparam>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <param name="f"></param>
        /// <returns></returns>
        public static IEnumerable<IEnumerable<V>> ZipZap<T, U, V>(this IEnumerable<IEnumerable<T>> left, IEnumerable<IEnumerable<U>> right, Func<T, U, V> f) =>
            left.Zip(right,
                (leftInner, rightInner) =>
                    leftInner.Zip(rightInner,
                        (leftPosition, rightPosition) =>
                            f(leftPosition, rightPosition)));

        /// <summary>
        /// Like ZipZap, but with more 'Zup'
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="U"></typeparam>
        /// <typeparam name="V"></typeparam>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <param name="f"></param>
        /// <returns></returns>
        public static IEnumerable<IEnumerable<IEnumerable<V>>> ZipZapZup<T, U, V>(this IEnumerable<IEnumerable<IEnumerable<T>>> left, IEnumerable<IEnumerable<IEnumerable<U>>> right, Func<T, U, V> f) =>
            left.Zip(right,
                (leftInner1, rightInner1) =>
                    leftInner1.Zip(rightInner1,
                        (leftInner2, rightInner2) =>
                            leftInner2.Zip(rightInner2,
                                (leftPosition, rightPosition) => 
                                    f(leftPosition, rightPosition))));


    }

}
