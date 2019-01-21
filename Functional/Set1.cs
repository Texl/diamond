using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.FSharp.Collections;
using static PlayStudios.Functional.Alg;

namespace PlayStudios.Functional
{
    public sealed class Set1<T>
        : IEquatable<Set1<T>>, IComparable<Set1<T>>, IComparable, IEnumerable1<T>
    {
        private Set1(FSharpSet<T> ofAtLeastOne)
        {
            Contents = ofAtLeastOne;
        }

        private readonly FSharpSet<T> Contents;

        public Set1<T> Add(T t) => new Set1<T>(Contents.Add(t));
        public IEnumerable<T> ToEnumerable() => Contents;
        public T[] ToArray() => Contents.ToArray();

        public Option<Set1<T>> Remove(T t)
        {
            var withRemoval = Contents.Remove(t);
            if (withRemoval.IsEmpty)
            {
                return None<Set1<T>>();
            }
            return Some(new Set1<T>(withRemoval));
        }

        public DStandardEnumerable1Form<T> GetStandardForm() =>
            () =>
            {
                var enumerator = ((IEnumerable<T>)Contents).GetEnumerator();
                if (!enumerator.MoveNext())
                {
                    throw new Exception("Set1's first item should have been set, grave error constructing it");
                }
                var first = enumerator.Current; // this has to succeed for a Set1 otherwise it was built wrong
                return Tuple.Create(first, enumerator);
            };

        public int Count => Contents.Count;

        public T Single()
        {
            if (Contents.Count == 1)
            {
                return Contents.Single();
            }

            throw new Exception("Asked for Set1.Single; but there were " + Contents.Count + " entries for type " + typeof(T));
        }

        public IEnumerable1<U> SelectL<U>(Func<T, U> f) => Enumerable1Util.SelectL(GetStandardForm(), f);
        public Array1<T> ToArray1() => Enumerable1Util.ToArray1(GetStandardForm());

        public static Set1<T> Build(T first, IEnumerable<T> rest) => new Set1<T>(SetModule.OfSeq(new[] { first }.Concat(rest))); // important - first in first, or we may change semantics if 'rest' includes 'first' value

        public bool All(Func<T, bool> f) => Contents.All(f);

        public bool Contains(T t) => Contents.Contains(t);

        public bool Equals(Set1<T> other) => Contents.Equals(other.Contents);

        public override bool Equals(object obj) => Equals((Set1<T>)obj);

        public int CompareTo(Set1<T> other) => ((IComparable) Contents).CompareTo(other.Contents);

        public int CompareTo(object obj) => CompareTo((Set1<T>)obj);

        public override string ToString() => "Set1 " + Contents;

        public override int GetHashCode() => Contents.GetHashCode();
    }

    public static class Set1
    {
        public static Set1<T> Sole<T>(T t) => Set1<T>.Build(t, new T[] {});
        public static Set1<T> Build<T>(T first, IEnumerable<T> rest) => Set1<T>.Build(first, rest.ToArray());


        public static Option<Set1<T>> Contingent<T>(IEnumerable<T> elements)
        {
            var current = elements.GetEnumerator();
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
                return None<Set1<T>>();
            }
        }

        public static Set1<T> Insist<T>(IEnumerable<T> elements) => Contingent(elements).Match(x => x, () => { throw new Exception("You insisted that the input list would always have at least 1 element; but were mistaken"); });

    }

}
