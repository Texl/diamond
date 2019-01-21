using System;
using System.Collections;
using System.Collections.Generic;

namespace PlayStudios.Functional
{
    public delegate Tuple<T, IEnumerator<T>> DStandardEnumerable1Form<T>();

    public interface IEnumerable1<T>
    {
        DStandardEnumerable1Form<T> GetStandardForm();
        IEnumerable1<U> SelectL<U>(Func<T, U> f);

        IEnumerable<T> ToEnumerable();

        Array1<T> ToArray1();
    }

    public static class Enumerable1Util
    {

        private sealed class TransformingEnumerator<T, U> : IEnumerator<U>
        {
            public TransformingEnumerator(IEnumerator<T> original, Func<T, U> transformed)
            {
                mOriginal = original;
                mTransformed = transformed;
            }

            object IEnumerator.Current => mTransformed(mOriginal.Current);

            public U Current => mTransformed(mOriginal.Current);

            public bool MoveNext() => mOriginal.MoveNext();

            public void Reset() => mOriginal.Reset();

            public void Dispose() => mOriginal.Dispose();

            private readonly IEnumerator<T> mOriginal;
            private readonly Func<T, U> mTransformed;
        }


        // Deliberately basing on getting T, IEnumerator, not IEnumerable or Func<IEnumerator<>> - on the premise that the draw of T may have already put the generation of some costly
        // sequence in motion
        private sealed class BasicLazyIEnumerable<T> : IEnumerable1<T>
        {
            public BasicLazyIEnumerable(DStandardEnumerable1Form<T> data)
            {
                mData = data;
            }

            public DStandardEnumerable1Form<T> GetStandardForm() => mData;
            public IEnumerable1<U> SelectL<U>(Func<T, U> f) => FromTransform(mData, f);

            public IEnumerable<T> ToEnumerable()
            {
                var t = mData();
                var iterator = t.Item2;
                yield return t.Item1;
                while (iterator.MoveNext())
                {
                    yield return iterator.Current;
                }
            }

            public Array1<T> ToArray1() => Enumerable1Util.ToArray1(mData);

            private readonly DStandardEnumerable1Form<T> mData;
        }

        private static BasicLazyIEnumerable<U> FromTransform<T, U>(DStandardEnumerable1Form<T> getOriginal, Func<T, U> f) =>
            new BasicLazyIEnumerable<U>(
                () =>
                    getOriginal().Let(
                        original => Tuple.Create(f(original.Item1), (IEnumerator<U>)new TransformingEnumerator<T, U>(original.Item2, f))));

        public static IEnumerable1<U> SelectL<T, U>(DStandardEnumerable1Form<T> getIt, Func<T, U> f) => FromTransform(getIt, f);

        public static Array1<T> ToArray1<T>(DStandardEnumerable1Form<T> getIt)
        {
            var original = getIt();
            var i0 = original.Item1;
            var l = new List<T>();
            var e = original.Item2;
            while (e.MoveNext())
            {
                l.Add(e.Current);
            }
            return Array1.Build(i0, l.ToArray());
        }

        public static Array1<T> ToArray1<T>(int preKnownTailLength, DStandardEnumerable1Form<T> getIt)
        {
            var original = getIt();
            var i0 = original.Item1;
            var l = new T[preKnownTailLength];
            var e = original.Item2;
            int insertionPoint = 0;
            while (e.MoveNext())
            {
                l[insertionPoint] = e.Current;
                ++ insertionPoint;
            }

            if (insertionPoint != preKnownTailLength)
            {
                throw new Exception("Grave error preKnownTailLength of " + preKnownTailLength + " doesn't match the enumerable's reality/length of " + insertionPoint);
            }
            return Array1.Build(i0, l);
        }

    }
}
