using System;
using System.Collections.Generic;
using System.Linq;

namespace PlayStudios.Functional
{
    public sealed class Enumerable1<T>
    {
        public Enumerable1(T first, IEnumerable<T> rest)
        {
            First = first;
            Rest = rest;
        }

        public Option<Enumerable1<T>> Where(Func<T, bool> f)
        {
            if (f(First))
            {
                return Alg.Some(new Enumerable1<T>(First, Rest.Where(f).ToArray()));
            }
            var r = new List<T>();
            var current = ((IEnumerable<T>)Rest).GetEnumerator();
            while (current.MoveNext())
            {
                var candidate = current.Current;
                if (f(candidate))
                {
                    r.Add(candidate);
                }
            }
            return Alg.When(r.Any(), () => new Enumerable1<T>(r[0], r.Skip(1).ToArray()));
        }

        public Enumerable1<V> Zip<U, V>(Enumerable1<U> other, Func<T, U, V> f) => new Enumerable1<V>(f(First, other.First), Rest.Zip(other.Rest, f).ToArray());

        public U Aggregate<U>(U seed, Func<U, T, U> f) // assume base case is first and aggregate
            => Rest.Aggregate(f(seed, First), f);

        public bool Any(Func<T, bool> f) => f(First) || Rest.Any(f);

        public bool All(Func<T, bool> f) => f(First) && Rest.All(f);
        

        public Enumerable1<U> Select<U>(Func<T, U> f) => new Enumerable1<U>(f(First), Rest.Select(f).ToArray());

        public Enumerable1<U> Select<U>(Func<T, int, U> f) => new Enumerable1<U>(f(First, 0), Rest.Select((x, ord) => f(x, ord + 1)).ToArray());


        public Enumerable1<U> SelectMany<U>(Func<T, Enumerable1<U>> f) // can't guarantee Array against IEnumerable<U>
        {
            var fr0 = f(First);

            // technically correct assumption in that every element in Rest has to provide at least 1 as F returns an Enumerable1
            var r = new List<U>(fr0.Rest);
            foreach (var s in Rest.Select(f))
            {
                r.Add(s.First);
                r.AddRange(s.Rest);
            }
            return new Enumerable1<U>(fr0.First, r.ToArray());
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
                throw new Exception("Enumerable1 assumption for Single() failed - REST element was non-empty");
            }
            return First;
        }

        public T[] ToArray() => ToEnumerable().ToArray();

        public Tuple<T, IEnumerable<T>> GetTupleForm() => Tuple.Create(First, Rest);

        public readonly T First;

        public readonly IEnumerable<T> Rest;
    }

    public static class Enumerable1
    {
        public static Enumerable1<T> Build<T>(T first, IEnumerable<T> rest) => new Enumerable1<T>(first, rest.ToArray());

        public static Enumerable1<T> Sole<T>(T sole) => Build(sole, Enumerable.Empty<T>());

        public static Option<Enumerable1<T>> ContingentWithSkip1<T>(IEnumerable<T> elements)
        {
            var current = elements.GetEnumerator();
            if (current.MoveNext())
            {
                var first = current.Current;
                return Alg.Some(Build(first, elements.Skip(1)));
            }
            else
            {
                return Alg.None<Enumerable1<T>>();
            }
        }

        public static Enumerable1<T> InsistWithSkip1<T>(IEnumerable<T> elements) => ContingentWithSkip1(elements).Match(x => x, () => { throw new Exception("You insisted that the input list would always have at least 1 element; but were mistaken"); });

        public static long Sum<TSource>(this Enumerable1<TSource> source, Func<TSource, long> selector) => selector(source.First) + source.Rest.Sum(selector);
        public static double Sum<TSource>(this Enumerable1<TSource> source, Func<TSource, double> selector) => selector(source.First) + source.Rest.Sum(selector);
        public static decimal Sum<TSource>(this Enumerable1<TSource> source, Func<TSource, decimal> selector) => selector(source.First) + source.Rest.Sum(selector);

    }

}
