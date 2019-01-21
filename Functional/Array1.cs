using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using static PlayStudios.Functional.Alg;

namespace PlayStudios.Functional
{
    [DataContract] // tradeoff in utility vs having to make an import version, a lot of these cases are in ms leaning configs - Serialization is .Net so not too horrible
    public sealed class Array1<T> : IComparable<Array1<T>>, IEquatable<Array1<T>>, IComparable, IEnumerable1<T> // deliberately a class for composition reasons. Too bad can't have its optionality tuned.
    {
        public Array1(T first, T[] rest)
        {
            First = first;
            Rest = rest;
        }

        public Option<Array1<T>> Where(Func<T, bool> f)
        {
            if (f(First))
            {
                return Some(new Array1<T>(First, Rest.Where(f).ToArray()));
            }

            var r = new List<T>();
            using (var current = (Rest as IEnumerable<T>).GetEnumerator())
            {
                while (current.MoveNext())
                {
                    var candidate = current.Current;
                    if (f(candidate))
                    {
                        r.Add(candidate);
                    }
                }
            }

            return When(r.Any(), () => new Array1<T>(r[0], r.Skip(1).ToArray()));
        }


        public DStandardEnumerable1Form<T> GetStandardForm() => () => Tuple.Create(First, ((IEnumerable<T>)Rest).GetEnumerator());

        public Array1<T> ToArray1() => this; // Not that hokey to return 'this' - immutable container, why not. . . . Could become a problem if people were mutating the 'rest' or something.

        public IEnumerable1<U> SelectL<U>(Func<T, U> f) => Enumerable1Util.SelectL(GetStandardForm(), f);

        public Array1<V> Zip<U, V>(Array1<U> other, Func<T, U, V> f) => new Array1<V>(f(First, other.First), Rest.Zip(other.Rest, f).ToArray());

        public U Aggregate<U>(U seed, Func<U, T, U> f) => // assume base case is first and aggregate
            Rest.Aggregate(f(seed, First), f);

        public T Aggregate(Func<T, T, T> f) =>
            Rest.Aggregate(First, f);

        public bool Any(Func<T, bool> f) => f(First) || Rest.Any(f);

        public bool All(Func<T, bool> f) => f(First) && Rest.All(f);

        public int Length => 1 + Rest.Length;



        // Pass in good length
        // Using this as an optimization
        private static V[] BuiltArray<U, V>(int length, IEnumerable<U> origin, Func<U, V> f)
        {
            V[] r = new V[length];
            int currentIndex = 0;
            foreach (var v in origin)
            {
                r[currentIndex] = f(v);
                ++ currentIndex;
            }

            if (currentIndex != length)
            {
                throw new Exception("Length mismatch - expected " + length + " got " + currentIndex);
            }
            return r;
        }

        public Array1<U> Select<U>(Func<T, U> f) => new Array1<U>(f(First), BuiltArray(Rest.Length, Rest, f));

        public Array1<U> Select<U>(Func<T, int, U> f)
        {
            U[] r = new U[Rest.Length];
            int currentIndex = 0;
            var firstVal = f(First, 0); // might as well execute in order
            foreach (var v in Rest)
            {
                r[currentIndex] = f(v, currentIndex + 1 /* Remember, we're off by 1 to make room for 'first' */);
                ++currentIndex;
            }
            return new Array1<U>(firstVal, r);
        }



        public Enumerable1<T> ToEnumerable1() => Enumerable1.Build(First, Rest);

        public Array1<U> SelectMany<U>(Func<T, Array1<U>> f)
        {
            return Array1.Insist(ToEnumerable().SelectMany(x => f(x).ToEnumerable()));
/*
            Something wrong with this - was just an optimization anyway
            var fr0 = f(First);


            int totalLengthOfNewRest = fr0.Rest.Length;

            // technically correct assumption in that every element in Rest has to provide at least 1 as F returns an Array1
            var rs = new Array1<U>[fr0.Rest.Length]; // new List<U>(fr0.Rest);
            {
                int currentIndex = 0;
                foreach (var s in Rest.Select(f))
                {
                    rs[currentIndex] = s;
                    totalLengthOfNewRest += s.Length;
                    ++ currentIndex;
                }
            }

            var fr0First = fr0.First;
            var fr0Rest = fr0.Rest;
            {
                var r = new U[totalLengthOfNewRest];
                int fr0RestLength = fr0Rest.Length;
                Array.Copy(fr0Rest, 0, r, 0, fr0RestLength);
                int currentIndex = fr0RestLength;

                foreach (var g in rs)
                {
                    r[currentIndex] = g.First;
                    ++ currentIndex;
                    foreach (var v in g.Rest)
                    {
                        r[currentIndex] = v;
                        ++ currentIndex;
                    }
                }

                if (currentIndex != totalLengthOfNewRest)
                {
                    throw new Exception("Grave error in implementation of SelectMany - guess it hasn't been tested/used at all");
                }

                return new Array1<U>(fr0First, r);
            }
            */
        }

        public bool Contains(T t) => (First.Equals(t)) || Rest.Contains(t);

        public IEnumerable<U> SelectMany<U>(Func<T, IEnumerable<U>> f) // can't guarantee Array against IEnumerable<U>
        {
            foreach (var v in f(First))
            {
                yield return v;
            }
            foreach (var l in Rest)
            {
                foreach (var v in f(l))
                {
                    yield return v;
                }
            }
        }

        public IEnumerable<T> ToEnumerable()
        {
            yield return First;
            foreach (var v in Rest)
            {
                yield return v;
            }
        }

        public T Single()
        {
            if (Rest.Any())
            {
                throw new Exception("Array1 assumption for Single() failed - the length was actually " + Length);
            }
            return First;
        }

        public T Last() => Rest.Any() ? Rest.Last() : First;

        public T[] ToArray()
        {
            int length = Length;
            T[] r = new T[length];
            r[0] = First;
            for (int i = 1; i < length; ++ i)
            {
                r[i] = Rest[i - 1];
            }
            return r;
        }

        public Tuple<T, T[]> GetTupleForm() => Tuple.Create(First, Rest);

        public T this[int i] => (i == 0) ? First : Rest[i - 1];

        [DataMember]
        public readonly T First;

        [DataMember]
        public readonly T[] Rest;


        public int CompareTo(Array1<T> other) =>
            (Rest.Length == other.Rest.Length)
                ? ToEnumerable().Zip(
                    other.ToEnumerable(),
                    (a, b) =>
                    {
                        if (a is IComparable comparable)
                        {
                            return comparable.CompareTo(b);
                        }
                        throw new Exception("Trying to compare Array1 based on non-comparable type " + typeof(T).ToString());
                    }).SkipWhile(c => c == 0).Take(1).Sum() // The SUM will get a 0 on full equality which is as you'd want
                : Rest.Length.CompareTo(other.Rest.Length);

        public int CompareTo(object other)
        {
            var theOther = other as Array1<T>;
            if (theOther != null)
            {
                return CompareTo(theOther);
            }
            throw new Exception("Bad attempt to compare Array of " + typeof(T) + " to " + other.GetType());
        }

        public override string ToString() => "(Array1 [" + MergedStrings(Intersperse(",", ToEnumerable().Select(x => x.ToString()))) + "])";
        public override bool Equals(object obj) => (obj is Array1<T> array1) && (this.Equals(array1));
        public override int GetHashCode() => CombinedListOfHashCodes(ToEnumerable().Select(x => x.GetHashCode()));

        public bool Equals(Array1<T> obj) => (Rest.Length == obj.Rest.Length) && ToEnumerable().Zip(obj.ToEnumerable(), (a, b) => a.Equals(b)).All(x => x);

    }

    public static class Array1
    {
        public static Array1<T> Build<T>(Enumerable1<T> entries) => Build(entries.First, entries.Rest.ToArray());

        public static Array1<T> Build<T>(T first, IEnumerable<T> rest) => new Array1<T>(first, rest.ToArray());
        public static Array1<T> Build<T>(T first, params T[] rest) => new Array1<T>(first, rest);

        public static Array1<T> Sole<T>(T sole) => Build(sole, Enumerable.Empty<T>());

        public static Option<Array1<T>> Contingent<T>(IEnumerable<T> elements)
        {
            using (var current = elements.GetEnumerator())
            {
                if (current.MoveNext())
                {
                    var first = current.Current;
                    var rest = new List<T>();
                    while (current.MoveNext())
                    {
                        rest.Add(current.Current);
                    }

                    return Some(Build(first, rest));
                }
                else
                {
                    return None<Array1<T>>();
                }
            }
        }

        public static Array1<T> Insist<T>(IEnumerable<T> elements) => Contingent(elements).Match(x => x, () => { throw new Exception("You insisted that the input list would always have at least 1 element; but were mistaken"); });

        public static int Sum<TSource>(this Array1<TSource> source, Func<TSource, int> selector) => selector(source.First) + source.Rest.Sum(selector);
        public static double Sum<TSource>(this Array1<TSource> source, Func<TSource, double> selector) => selector(source.First) + source.Rest.Sum(selector);
        public static long Sum<TSource>(this Array1<TSource> source, Func<TSource, long> selector) => selector(source.First) + source.Rest.Sum(selector);

    }

}
