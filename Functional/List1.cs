using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.FSharp.Collections;
using static PlayStudios.Functional.Alg;

namespace PlayStudios.Functional
{
    // Note - this thing doesn't currently serialize - it may never serialize - reassess
    public sealed class List1<T> : IComparable<List1<T>>, IEquatable<List1<T>>, IComparable, IEnumerable1<T> // deliberately a class for composition reasons. Too bad can't have its optionality tuned.
    {
        public List1(T head, Option<List1<T>> tail)
        {
            Head = head;
            Tail = tail;
        }
        public readonly T Head;

        public readonly Option<List1<T>> Tail;

        public IEnumerable<T> ToEnumerable() // without growing stack based on length
        {
            yield return Head;
            var current = Tail;
            while (current.HasValue)
            {
                var v = current;
                yield return v.Value.Head;
                current = v.Value.Tail;
            }
        }

        private IEnumerable<T> GetTailAsEnumerable()
        {
            var current = Tail;
            while (current.HasValue)
            {
                var tv = Tail.Value;
                yield return tv.Head;
                current = tv.Tail;
            }
        }

        public DStandardEnumerable1Form<T> GetStandardForm() => () => Tuple.Create(Head, GetTailAsEnumerable().GetEnumerator());

        public IEnumerable1<U> SelectL<U>(Func<T, U> f) => Enumerable1Util.SelectL(GetStandardForm(), f);


        // TODO - optimize this (would need structural changes to List1's privacy to enable efficient forward build). Putting this here as a placeholder so that
        // end-users don't scatter .Insist(.ToEnumable()) themselves
        public List1<U> Select<U>(Func<T, U> f) => List1.Insist(ToEnumerable().Select(f));

        public Array1<T> ToArray1() => Enumerable1Util.ToArray1(GetStandardForm());

        public int Count()
        {
            int count = 1;
            var current = Tail;
            while (current.HasValue)
            {
                ++ count;
                current = current.Value.Tail;
            }
            return count;
        }

        public TAccumulate Aggregate<TAccumulate>(TAccumulate seed, Func<TAccumulate, T, TAccumulate> func) => ToEnumerable().Aggregate(seed, func);

        public int Length
        {
            get
            {
                int len = 1;
                var current = this;
                while (current.Tail.HasValue)
                {
                    current = current.Tail.Value;
                    ++ len;
                }
                return len;
            }
        }

        /*
        public List1<T> Reverse()
        {
            var accum = new List1<T>(Head, None<List1<T>>());
            var current = Tail;
            while (current.HasValue)
            {
                
            }
        }
        */

        public T[] ToArray() => ToEnumerable().ToArray(); // Have to count them one way or another to make the array. . .


        private static int CompareToT(T a, T b)
        {
            if (a is IComparable comparable)
            {
                return comparable.CompareTo(b);
            }

            throw new Exception(typeof(T).ToString() + " is not comparable");
        }

        public int CompareTo(List1<T> other)
        {
            var ch = CompareToT(Head, other.Head);
            if (CompareToT(Head, other.Head) == 0)
            {
                using (var a = Tail.ToEnumerable().SelectMany(x => x.ToEnumerable()).GetEnumerator())
                {
                    using (var b = other.Tail.ToEnumerable().SelectMany(x => x.ToEnumerable()).GetEnumerator())
                    {
                        for (;;)
                        {
                            bool an = a.MoveNext();
                            bool bn = b.MoveNext();
                            if (an != bn) // one must have stopped - ergo, different lengths
                            {
                                return an ? 1 : -1; // left/'this' continues, it's called the greater of the 2
                            }

                            if (!an) // flags are equal after check above - an is false, both are false - both hit end, ergo both are equal
                            {
                                return 0; // bn == false. They're equal
                            }

                            int c = CompareToT(a.Current, b.Current);
                            if (c != 0) // they mismatch - hit the end
                            {
                                return c;
                            }
                        }
                    }
                }
            }
            return ch;

        }

        public int CompareTo(object other)
        {
            if (other is List1<T> theOther)
            {
                return CompareTo(theOther);
            }
            throw new Exception("Bad attempt to compare List1 of " + typeof(T) + " to " + other.GetType());
        }

        public override string ToString() => "(List1 [" + MergedStrings(Intersperse(",", ToEnumerable().Select(x => x.ToString()))) + "])";
        public override bool Equals(object obj) => (obj is List1<T> list1) && (this.Equals(list1));
        public override int GetHashCode() => CombinedListOfHashCodes(ToEnumerable().Select(x => x.GetHashCode()));

        public bool Equals(List1<T> obj) => CompareTo(obj) == 0;
    }


    public static class List1
    {
        public static Option<List1<T>> Contingent<T>(IEnumerable<T> list) => list.Reverse().Aggregate(None<List1<T>>(), (current, v) => Some(new List1<T>(v, current)));

        public static List1<T> Insist<T>(IEnumerable<T> list) => Contingent(list).Match(x => x, () => { throw new Exception("You insisted that the input list would always have at least 1 element; but were mistaken"); });

        public static List1<T> InsistR<T>(IEnumerable<T> listR) =>
            listR.Aggregate(None<List1<T>>(), (current, v) => Some(new List1<T>(v, current))).Match(x => x, () => { throw new Exception("You insisted that the input list would always have at least 1 element; but were mistaken"); });

        public static List1<T> Sole<T>(T val) => new List1<T>(val, None<List1<T>>());

        public static List1<T> Build<T>(T val, Option<List1<T>> tail) => new List1<T>(val, tail);


        private static List1<T> FromListR<T>(T toBecomeFirst, FSharpList<T> restR)
        {
            var accum = None<List1<T>>();
            var read = restR;
            while (! read.IsEmpty)
            {
                accum = Some(new List1<T>(read.Head, accum));
                read = read.Tail;
            }
            return new List1<T>(toBecomeFirst, accum);
        }

        public static List1<U> Select<T, U>(this List1<T> source, Func<T, U> f)
        {
            var toBecomeFirst = f(source.Head);
            var current = source.Tail;
            var accum = FSharpList<U>.Empty;
            while (current.HasValue)
            {
                var cv = current.Value;
                accum = Cons(f(cv.Head), accum);
                current = cv.Tail;
            }
            return FromListR(toBecomeFirst, accum);
        }
        public static List1<U> Select<T, U>(this List1<T> source, Func<T, int, U> f)
        {
            var toBecomeFirst = f(source.Head, 0);
            var current = source.Tail;
            int num = 0;
            var accum = FSharpList<U>.Empty;
            while (current.HasValue)
            {
                var cv = current.Value;
                ++num;
                accum = Cons(f(cv.Head, num), accum);
                current = cv.Tail;
            }
            return FromListR(toBecomeFirst, accum);
        }

        public static List1<U> Zip<T1, T2, U>(this List1<T1> source1, List1<T2> source2, Func<T1, T2, U> f)
        {
            var toBecomeFirst = f(source1.Head, source2.Head);
            var current1 = source1.Tail;
            var current2 = source2.Tail;
            var accum = FSharpList<U>.Empty;
            while (current1.HasValue && current2.HasValue)
            {
                var cv1 = current1.Value;
                var cv2 = current2.Value;
                accum = Cons(f(cv1.Head, cv2.Head), accum);
                current1 = cv1.Tail;
                current2 = cv2.Tail;
            }
            return FromListR(toBecomeFirst, accum);
        }


        public static TAccumulate Fold<TSource, TAccumulate>(this List1<TSource> source, TAccumulate seed, Func<TAccumulate, TSource, TAccumulate> func)
        {
            var current = func(seed, source.Head);
            var rp = source.Tail;
            while (rp.HasValue)
            {
                var n = rp.Value;
                current = func(current, n.Head);
                rp = n.Tail;
            }
            return current;
        }

        public static List1<T> Reverse<T>(this List1<T> source) => source.Fold(None<List1<T>>(), (s, n) => Some(new List1<T>(n, s))).Value;
    }
}
