using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

namespace PlayStudios.Functional
{
    public static class ExtensionsIEnumerable
    {
        public class Enumerated<TValue>
        {
            public Enumerated(TValue value, int index)
            {
                Value = value;
                Index = index;
            }

            public Tuple<TValue, int> Tuple()
            {
                return new Tuple<TValue, int>(Value, Index);
            }

            public TValue Value { get; private set; }
            public int Index { get; private set; }
        }

        public static IEnumerable<Enumerated<TInput>> Enumerate<TInput>(this IEnumerable<TInput> list)
        {
            return list.Select((x, ord) => new Enumerated<TInput>(x, ord));
        }

        /// <summary>
        /// Groups element into sub-lists of adjacent that share the same property, true or false, from the predicate.
        /// Basically performs edge detection in order to operate.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="elements"></param>
        /// <param name="predicate"></param>
        /// <returns></returns>
        public static IEnumerable<Tuple<IEnumerable<T>, bool>> ClusteredByPredicate<T>(this IEnumerable<T> elements, Func<T, bool> predicate)
        {
            // We'll build a list assigning numeric, so we can have true, false AND "added at the front" for edge detection purposes.
            var withTrueFalseAsOneZero = Alg.ReadPoint(elements.Select(x => Tuple.Create(x, predicate(x) ? 1 : 0))).TakenToEnd();

            return Alg.Map(
                (c, prior) =>
                {
                    var cf = c.First();
                    return Alg.When(
                        cf.Item2 != prior.Item2,
                        () => Tuple.Create(c.TakeWhile(x => x.Item2 == cf.Item2).Select(x => x.Item1), (cf.Item2 == 1)));
                },
                withTrueFalseAsOneZero.Tails().Where(x => x.Any()), new[] { Tuple.Create(default(T), -1) }.Concat(withTrueFalseAsOneZero)).SelectMany(x => x.ToEnumerable());
        }

        // A, B, C, D -> (A,B), (B,C), (C,D)
        // From: http://stackoverflow.com/a/1581482/99377
        public static IEnumerable<TResult> Pairwise<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, TSource, TResult> resultSelector)
        {
            TSource previous = default(TSource);

            using (var it = source.GetEnumerator())
            {
                if (it.MoveNext())
                    previous = it.Current;

                while (it.MoveNext())
                    yield return resultSelector(previous, previous = it.Current);
            }
        }

        /// <summary>
        /// http://hackage.haskell.org/package/base-4.9.0.0/docs/Data-List.html#v:partition
        /// Splits a source IEnumerable into a pair of IEnumerables, where elements of Item1 match a predicate and Item2 is everything else.
        /// e.g.: [ 1, 2, 3, 4, 5 ].PartitionedByPredicate(isEven) -> ([ 2, 4 ], [ 1, 3, 5 ])
        /// This version uses some caching mechanisms - the trivial way is to scan the list twice with the predicate inverted for one
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="source"></param>
        /// <param name="predicate"></param>
        /// <returns></returns>
        public static Tuple<IEnumerable<T>, IEnumerable<T>> PartitionedByPredicate<T>(this IEnumerable<T> source, Func<T, bool> predicate)
        {
            return source
                .Select(t => Tuple.Create(t, predicate(t)))
                .Tails()
                .SelectMany(x => x.Take(1))
                .Let(l => Tuple.Create( l.Where(x => x.Item2).Select(x => x.Item1)
                                      , l.Where(x => !x.Item2).Select(x => x.Item1)));
        }

        public static T SingleOrFailure<T>(this IEnumerable<T> source, Func<String> onFailure)
        {
            try
            {
                return source.Single();
            }
            catch (InvalidOperationException e)
            {
                throw new InvalidOperationException(onFailure(), e);
            }
        }

        public static T SingleOrFailure<T>(this IEnumerable<T> source, Func<T, Boolean> predicate, Func<String> onFailure)
        {
            try
            {
                return source.Single(predicate);
            }
            catch (InvalidOperationException e)
            {
                throw new InvalidOperationException(onFailure(), e);
            }
        }

        public static IEnumerable<IEnumerable<T>> Tails<T>(this IEnumerable<T> original)
        {
            return Alg.ReadPoint(original).AllReadPointsDownIncludingEnd().Select(x => x.TakenToEnd());
        }

        public static IEnumerable<T> DropFromEnd<T>(this IEnumerable<T> source, int count)
        {   // optimized to "lazy" scan rather than fully scanning to do the "reverse". . . 
            using (IEnumerator<T> tracker = source.GetEnumerator())
            {
                T[] rotaryBuffer = new T[count];
                bool hitEndEarly = false;
                for (int i = 0; i < count; ++i)
                {
                    if (tracker.MoveNext())
                    {
                        rotaryBuffer[i] = tracker.Current;
                    }
                    else
                    {
                        hitEndEarly = true;
                        break;
                    }
                }
                if (!hitEndEarly)
                {
                    int readIndex = 0;
                    while (tracker.MoveNext())
                    {
                        T rv = rotaryBuffer[readIndex];
                        rotaryBuffer[readIndex] = tracker.Current;
                        yield return rv;
                        ++readIndex;
                        if (readIndex == count)
                        {
                            readIndex = 0;
                        }
                    }
                }
            }
        }

        public static IEnumerable<T> DropFromEnd<T>(this IEnumerable<T> source)
        {
            return source.DropFromEnd(1);
        }

        public static IEnumerable<T> DropFromEndWhile<T>(this IEnumerable<T> source, Func<T, bool> predicate)
        {   // performance-wise this scans the whole list and applies the predicate from the end inward.
            // Only alternative to this would be to pay the price of using the predicate *more* often in forward direction, looking for an unbroken run of <predicate> and storing interim.
            T[] sourceArray = source.ToArray();
            int toKeep = sourceArray.Length;
            while (toKeep > 0)
            {
                if (predicate(sourceArray[toKeep - 1]))
                {
                    toKeep = toKeep - 1;
                }
                else
                {
                    break;
                }
            }
            if (toKeep == sourceArray.Length)
            {   // use the array we've already allocated, all entries passed predicate
                return sourceArray;
            }
            else
            {   // build a new array containing subset.
                return sourceArray.Take(toKeep).ToArray();
            }
        }

        public static IEnumerable<T> Flatten<T>(this IEnumerable<IEnumerable<T>> items)
        {
            return items.SelectMany(x => x);
        }

        /// <summary>
        /// Takes a list of Options and returns a list of all the "Some" values
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="items"></param>
        /// <returns></returns>
        public static IEnumerable<T> CatOptions<T>(this IEnumerable<Option<T>> items)
        {
            return items.SelectMany(i => i.ToEnumerable());
        }

        /// <summary>
        /// A version of `Select` that can discard elements. Only `Some` values from the `selector` are in the returned collection
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="K"></typeparam>
        /// <param name="source"></param>
        /// <param name="selector"></param>
        /// <returns></returns>
        public static IEnumerable<K> SelectMaybe<T, K>(this IEnumerable<T> source, Func<T, Option<K>> selector)
        {
            return source.Select(selector).CatOptions();
        }

        /// <summary>
        /// Cross product of two sets
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="G"></typeparam>
        /// <param name="collection1"></param>
        /// <param name="collection2"></param>
        /// <returns></returns>
        public static IEnumerable<KeyValuePair<T, G>> Correlate<T, G>(this List<T> collection1, List<G> collection2)
        {
            var correlation = new List<KeyValuePair<T, G>>();
            if (collection1.Count != collection2.Count)
            {
                throw new InvalidOperationException("Correlation failed. Collections not same size");
            }
            for(int i = 0 ; i < collection1.Count; i++)
            {
                correlation.Add(new KeyValuePair<T, G>(collection1[i], collection2[i]));
            }
            return correlation;
        }

        /// <summary>
        /// Returns a random element of a list given a random function
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="collection"></param>
        /// <param name="random"></param>
        /// <returns></returns>
        public static T GetRandom<T>(this IList<T> collection, Func<int, int> random)
        {
            int selection = Math.Abs(random(collection.Count));
            int index = selection % collection.Count;
            return collection.ElementAt(index);
        }

        public static IEnumerable<IEnumerable<T>> Transpose<T>(this IEnumerable<IEnumerable<T>> original)
        {
            var orp = Alg.ReadPoint(original);
            if (!orp.AtEnd)
            {
                int numHorizontalInOriginal = orp.Value.Count();
                // We cache this
                var originalsAsArrays = Alg.ReadPoint(original.Select(row => { var r = row.ToArray(); if (r.Length != numHorizontalInOriginal) { throw new Exception("Row size mismatch"); } return r; })).TakenToEnd();
                return Enumerable.Range(0, numHorizontalInOriginal).Select(ordinal => originalsAsArrays.Select(row => row[ordinal]));
            }

            return new IEnumerable<T>[] { };
        }

        // This is an assist function, re-scans do a new draw against the source
        private static IEnumerable<T> ShuffledAssist<T>(IEnumerable<T> source, Func<T, int> weightAccess, Func<int, int> random)
        {
            List<T> originals = source.ToList();
            int totalWeight = originals.Sum(weightAccess);

            while (originals.Any())
            {
                int roll = random(totalWeight);

                for (int i = 0; i != originals.Count; ++i)
                {
                    int weight = weightAccess(originals[i]);
                    if (roll < weight)
                    {
                        totalWeight -= weight;

                        Debug.Assert(totalWeight >= 0);

                        yield return originals[i];

                        originals.RemoveAt(i);

                        Debug.Assert(originals.Count > 0 || totalWeight == 0);
                        break;
                    }

                    roll -= weight;
                }
            }
        }


        // Memoizes/caches its result, subsequent scans of the same result will give the same sequence.
        public static IEnumerableReenumerable<T> Shuffled<T>(this IEnumerable<T> source, Func<int, int> random)
        {
            // standard shuffle: everything has a weight of 1
            return Alg.ReadPoint(ShuffledAssist(source, item => 1, random)).TakenToEnd();
        }

        public static IEnumerableReenumerable<T> ShuffledWithWeights<T>(this IEnumerable<T> source, Func<T, int> weightAccess, Func<int, int> random)
        {
            return Alg.ReadPoint(ShuffledAssist(source, weightAccess, random)).TakenToEnd();
        }

        public static ISet<T> ToSet<T>(this IEnumerable<T> source)
        {
            return new HashSet<T>(source);
        }

        public static Option<T> ToOption<T>(this IEnumerable<T> source)
        {
            return Alg.CappedSourceToOption(source);
        }

        public static IEnumerableReenumerable<T> Memoized<T>(this IEnumerable<T> source)
        {
            return Alg.ReadPoint(source).TakenToEnd();
        }

        public static IEnumerable<T> Repeated<T>(this IEnumerable<T> sequence, int count)
        {
            foreach (var x in sequence)
            {
                for (int i = 0; i != count; ++i)
                {
                    yield return x;
                }
            }
        }

        public static IEnumerable<TN> RunningAggregate<T, TN>(this IEnumerable<T> collection, TN seed, Func<T, TN, TN> accumulator)
        {
            var list = new List<TN>();
            var amount = seed;
            foreach (var t in collection)
            {
                amount = accumulator(t, amount);
                list.Add(amount);
            }
            return list;
        }

        public static IEnumerable<TResult> Zip<TFirst, TSecond, TThird, TResult>(this IEnumerable<TFirst> first, IEnumerable<TSecond> second, IEnumerable<TThird> third, Func<TFirst, TSecond, TThird, TResult> resultSelector)
        {
            System.Diagnostics.Contracts.Contract.Requires(first != null && second != null && third != null && resultSelector != null);

            using (IEnumerator<TFirst> iterator1 = first.GetEnumerator())
            using (IEnumerator<TSecond> iterator2 = second.GetEnumerator())
            using (IEnumerator<TThird> iterator3 = third.GetEnumerator())
            {
                while (iterator1.MoveNext() && iterator2.MoveNext() && iterator3.MoveNext())
                {
                    yield return resultSelector(iterator1.Current, iterator2.Current, iterator3.Current);
                }
            }
        }

        public static IEnumerable<TResult> Zip<TFirst, TSecond, TThird, TFourth, TResult>(this IEnumerable<TFirst> first, IEnumerable<TSecond> second, IEnumerable<TThird> third, IEnumerable<TFourth> fourth, Func<TFirst, TSecond, TThird, TFourth, TResult> resultSelector)
        {
            System.Diagnostics.Contracts.Contract.Requires(first != null && second != null && third != null && fourth != null && resultSelector != null);

            using (IEnumerator<TFirst> iterator1 = first.GetEnumerator())
            using (IEnumerator<TSecond> iterator2 = second.GetEnumerator())
            using (IEnumerator<TThird> iterator3 = third.GetEnumerator())
            using (IEnumerator<TFourth> iterator4 = fourth.GetEnumerator())
            {
                while (iterator1.MoveNext() && iterator2.MoveNext() && iterator3.MoveNext() && iterator4.MoveNext())
                {
                    yield return resultSelector(iterator1.Current, iterator2.Current, iterator3.Current, iterator4.Current);
                }
            }
        }

        public static void ForEach<T>(this IEnumerable<T> sequence, Action<T> fn)
        {
            foreach (var t in sequence)
            {
                fn(t);
            }
        }

        public static void ForEach<T>(this IEnumerable<T> sequence, Action<T, int> fn)
        {
            foreach (var t in sequence.Enumerate())
            {
                fn(t.Value, t.Index);
            }
        }

        // analogue of string.Split
        public static IEnumerable<IEnumerable<T>> Split<T>(this IEnumerable<T> xs, Func<T, bool> predicate)
        {
            var result = new List<IEnumerable<T>>();

            IEnumerable<T> p = xs;

            while (p.Any())
            {
                p = p.SkipWhile(predicate);

                var chunk = p.TakeWhile(x => !predicate(x));

                if (chunk.Any())
                {
                    result.Add(chunk);
                }

                p = p.SkipWhile(x => !predicate(x));
            }

            return result;
        }
    }
}
