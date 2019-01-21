using System;
using System.Diagnostics;
using JetBrains.Annotations; // replacement for System.Diagnostics.Contracts to get alternate PureAttribute - in attempt to get [Pure] to be seen by resharper
using Microsoft.FSharp.Core;

namespace PlayStudios.Functional
{
    public static class Either
    {
        public static Either<U, U> Select<T, U>(this Either<T, T> e, Func<T, U> f) => e.Map(f, f);
    }

    // currently storing in defaultable form to preserve valueness of contents.
    [Serializable()]
    [DebuggerTypeProxy(typeof(Either<,>.DebugProxy))]
    public sealed class Either<L, R> : IEquatable<Either<L, R>>, IComparable<Either<L, R>>, IComparable
    {
        private Either(bool isLeft, L left, R right)
        {
            mIsLeft = isLeft;
            mLeft = left;
            mRight = right;
        }

        private class DebugProxy
        {
            public DebugProxy(Either<L, R> either)
            {
                MaybeLeft = either.LeftIfPresent();
                MaybeRight = either.RightIfPresent();
            }

            public readonly Option<L> MaybeLeft;
            public readonly Option<R> MaybeRight;
        }


        public int GuardFieldToPreventAutoSerializationToJSON { get { throw new Exception("GuardFieldToPreventAutoSerializationToJSON on Either " + typeof(L) + ", " + typeof(R) + " - Do something else do not put this into serializable models"); } }

        public static Either<L, R> FromLeft(L left) => new Either<L, R>(true, left, default(R));

        public static Either<L, R> FromRight(R right) => new Either<L, R>(false, default(L), right);

        [Pure]
        public Option<L> LeftIfPresent() => mIsLeft ? Alg.Some(mLeft) : Alg.None<L>();

        [Pure]
        public Option<R> RightIfPresent() => mIsLeft ? Alg.None<R>() : Alg.Some(mRight);

        public bool IsLeft => mIsLeft;
        public bool IsRight => !mIsLeft;

        [Pure]
        public void Apply(Action<L> ifLeft, Action<R> ifRight)
        {
            if (mIsLeft)
            {
                ifLeft(mLeft);
            }
            else
            {
                ifRight(mRight);
            }
        }

        [Pure]
        public U Match<U>(Func<L, U> ifLeft, Func<R, U> ifRight) => mIsLeft ? ifLeft(mLeft) : ifRight(mRight);

        [Pure]
        public Either<A, B> Select<A, B>(Func<L, A> f0, Func<R, B> f1) => Match(i0 => Either<A, B>.FromLeft(f0(i0)), i1 => Either<A, B>.FromRight(f1(i1)));

        [Pure]
        public U MatchFS<U>(FSharpFunc<L, U> left, FSharpFunc<R, U> right) => Match(left.Invoke, right.Invoke);

        [Pure]
        public Either<R, L> Flipped() => Match(Either<R, L>.FromRight, Either<R, L>.FromLeft);

        [Pure]
        public Either<LL, RR> Map<LL, RR>(Func<L, LL> l, Func<R, RR> r) => IsLeft ? Either<LL, RR>.FromLeft(l(mLeft)) : Either<LL, RR>.FromRight(r(mRight));


        // Maybe shouldn't be an instance function
        [Pure]
        public TFinal Cross<LL, RR, TFinal>(Either<LL, RR> other, Func<L, LL, TFinal> a, Func<L, RR, TFinal> b, Func<R, LL, TFinal> c, Func<R, RR, TFinal> d) =>
            // Hehehh, hard/impossible to botch this line and still compile
            Match(l => other.Match(ll => a(l, ll), rr => b(l, rr)), r => other.Match(ll => c(r, ll), rr => d(r, rr)));

            //IsLeft ? Either<LL, RR>.FromLeft(l(mLeft)) : Either<LL, RR>.FromRight(r(mRight));

        private readonly bool mIsLeft;
        private readonly L mLeft;
        private readonly R mRight;

        public override bool Equals(object obj)
        {
            if (obj is Either<L, R>)
            {
                return Equals((Either<L, R>)obj);
            }
            return false;
        }
        public bool Equals(Either<L, R> obj)
        {
            // Doing this without lambdas so as to not deal with 'this' capture.
            if (obj.mIsLeft!= mIsLeft)
            {
                return false;
            }
            if (mIsLeft)
            {
                return mLeft.Equals(obj.mLeft);
            }
            return mRight.Equals(obj.mRight);
        }

        public override int GetHashCode() => (mIsLeft ? mLeft.GetHashCode() : ((-1) ^ mRight.GetHashCode())) ^ 0x55555555;

        public override string ToString() // could get a bit nuts trying to get consensus happiness with this, make each user spit its own string
        {
            // throw new Exception("Don't try to dig string out of Exception " + typeof(L).ToString() + ", " + typeof(R));

            return Match(l => $"Left {l}", r => $"Right {r}"); // With new resharper ability, nested-element pretty-printing (which neets ToString() nested) too much a 'pro' for any 'con' of having ToString()
        }

        public int CompareTo(Either<L, R> other)
        {
            if (mIsLeft)
            {
                if (other.mIsLeft)
                {
                    // bit ganky - thing is there's no way to statically prove data is comparable
                    var dataAsIComparable = mLeft as IComparable;
                    if (dataAsIComparable != null)
                    {
                        return dataAsIComparable.CompareTo(other.mLeft);
                    }
                    else
                    {
                        throw new Exception("Trying to compare non-comparable Either core type Left of " + typeof(L));
                    }
                }
                return 1; // this is left, compare is 1
            }
            else
            {
                if (!other.mIsLeft)
                { // both are right
                    var dataAsIComparable = mRight as IComparable;
                    if (dataAsIComparable != null)
                    {
                        return dataAsIComparable.CompareTo(other.mRight);
                    }
                    else
                    {
                        throw new Exception("Trying to compare non-comparable Either core type Right of " + typeof(R));
                    }
                }
                return -1; // this is right, other is left, -1
            }
        }

        public int CompareTo(object other)
        {
            if (other is Either<L, R>)
            {
                return CompareTo((Either<L, R>)other);
            }
            throw new Exception("Bad attempt to compare Either of " + typeof(L) + ", " + typeof(R) + " to ");
        }

    }

    [Serializable()]
    // Not storing by value like Either2 - should probably update
    public sealed class Union3<I0, I1, I2>
    {
        private Union3(int which, object data)
        {
            mWhich = which;
            mData = data;
        }
        public static Union3<I0, I1, I2> From0(I0 x) => new Union3<I0, I1, I2>(0, x);

        public static Union3<I0, I1, I2> From1(I1 x) => new Union3<I0, I1, I2>(1, x);

        public static Union3<I0, I1, I2> From2(I2 x) => new Union3<I0, I1, I2>(2, x);

        public static Union3<I0, I1, I2> From01(Either<I0, I1> o) => o.Match(From0, From1);

        public static Union3<I0, I1, I2> From12(Either<I1, I2> o) => o.Match(From1, From2);

        public static Union3<I0, I1, I2> From02(Either<I0, I2> o) => o.Match(From0, From2);

        [Pure]
        public Option<I0> I0IfPresent() => (mWhich == 0) ? Alg.Some<I0>((I0) mData) : Alg.None<I0>();

        [Pure]
        public Option<I1> I1IfPresent() => (mWhich == 1) ? Alg.Some<I1>((I1) mData) : Alg.None<I1>();

        [Pure]
        public Option<I2> I2IfPresent() => (mWhich == 2) ? Alg.Some<I2>((I2) mData) : Alg.None<I2>();

        [Pure]
        public U Match<U>(Func<I0, U> f0, Func<I1, U> f1, Func<I2, U> f2) => (mWhich == 0) ? f0((I0) mData) : ((mWhich == 1) ? f1((I1) mData) : f2((I2) mData));

        [Pure]
        public U MatchFS<U>(FSharpFunc<I0, U> f0, FSharpFunc<I1, U> f1, FSharpFunc<I2, U> f2) => Match(f0.Invoke, f1.Invoke, f2.Invoke);

        [Pure]
        public Union3<II0, II1, II2> Map<II0, II1, II2>(Func<I0, II0> f0, Func<I1, II1> f1, Func<I2, II2> f2) =>
            Match(
                x => Union3<II0, II1, II2>.From0(f0(x)),
                x => Union3<II0, II1, II2>.From1(f1(x)),
                x => Union3<II0, II1, II2>.From2(f2(x)));

        public void Apply(Action<I0> f0, Action<I1> f1, Action<I2> f2)
        {
            switch (mWhich)
            {
                case 0:
                    f0((I0) mData);
                    break;
                case 1:
                    f1((I1) mData);
                    break;
                default:
                    f2((I2) mData);
                    break;
            }
        }

        public bool IsI0 => mWhich == 0;
        public bool IsI1 => mWhich == 1;
        public bool IsI2 => mWhich == 2;

        [Pure]
        public Union3<A, B, C> Select<A, B, C>(Func<I0, A> f0, Func<I1, B> f1, Func<I2, C> f2) => Match(i0 => Union3<A, B, C>.From0(f0(i0)), i1 => Union3<A, B, C>.From1(f1(i1)), i2 => Union3<A, B, C>.From2(f2(i2)));

        private readonly int mWhich;
        private readonly object mData;
    }

    [Serializable()]
    // Not storing by value like Either2 - should probably update
    public sealed class Union4<I0, I1, I2, I3>
    {
        private Union4(int which, object data)
        {
            mWhich = which;
            mData = data;
        }
        public static Union4<I0, I1, I2, I3> From0(I0 x) => new Union4<I0, I1, I2, I3>(0, x);

        public static Union4<I0, I1, I2, I3> From1(I1 x) => new Union4<I0, I1, I2, I3>(1, x);

        public static Union4<I0, I1, I2, I3> From2(I2 x) => new Union4<I0, I1, I2, I3>(2, x);

        public static Union4<I0, I1, I2, I3> From3(I3 x) => new Union4<I0, I1, I2, I3>(3, x);

        public static Union4<I0, I1, I2, I3> From012(Union3<I0, I1, I2> o) => o.Match(From0, From1, From2);

        public static Union4<I0, I1, I2, I3> From013(Union3<I0, I1, I3> o) => o.Match(From0, From1, From3);

        public static Union4<I0, I1, I2, I3> From023(Union3<I0, I2, I3> o) => o.Match(From0, From2, From3);

        public static Union4<I0, I1, I2, I3> From123(Union3<I1, I2, I3> o) => o.Match(From1, From2, From3);

        [Pure]
        public Option<I0> I0IfPresent() => (mWhich == 0) ? Alg.Some<I0>((I0)mData) : Alg.None<I0>();

        [Pure]
        public Option<I1> I1IfPresent() => (mWhich == 1) ? Alg.Some<I1>((I1)mData) : Alg.None<I1>();

        [Pure]
        public Option<I2> I2IfPresent() => (mWhich == 2) ? Alg.Some<I2>((I2)mData) : Alg.None<I2>();

        [Pure]
        public Option<I3> I3IfPresent() => (mWhich == 3) ? Alg.Some<I3>((I3)mData) : Alg.None<I3>();

        [Pure]
        public U Match<U>(Func<I0, U> f0, Func<I1, U> f1, Func<I2, U> f2, Func<I3, U> f3) => (mWhich == 0) ? f0((I0)mData) : ((mWhich == 1) ? f1((I1)mData) : ((mWhich == 2) ? f2((I2)mData) : f3((I3)mData)));

        [Pure]
        public U MatchFS<U>(FSharpFunc<I0, U> f0, FSharpFunc<I1, U> f1, FSharpFunc<I2, U> f2, FSharpFunc<I3, U> f3) => Match(f0.Invoke, f1.Invoke, f2.Invoke, f3.Invoke);

        [Pure]
        public Union4<II0, II1, II2, II3> Map<II0, II1, II2, II3>(Func<I0, II0> f0, Func<I1, II1> f1, Func<I2, II2> f2, Func<I3, II3> f3) =>
            Match(
                x => Union4<II0, II1, II2, II3>.From0(f0(x)),
                x => Union4<II0, II1, II2, II3>.From1(f1(x)),
                x => Union4<II0, II1, II2, II3>.From2(f2(x)),
                x => Union4<II0, II1, II2, II3>.From3(f3(x)));

        public void Apply(Action<I0> f0, Action<I1> f1, Action<I2> f2, Action<I3> f3)
        {
            switch (mWhich)
            {
                case 0:
                    f0((I0)mData);
                    break;
                case 1:
                    f1((I1)mData);
                    break;
                case 2:
                    f2((I2)mData);
                    break;
                default:
                    f3((I3)mData);
                    break;
            }
        }

        public bool IsI0 => mWhich == 0;
        public bool IsI1 => mWhich == 1;
        public bool IsI2 => mWhich == 2;
        public bool IsI3 => mWhich == 3;

        [Pure]
        public Union4<A, B, C, D> Select<A, B, C, D>(Func<I0, A> f0, Func<I1, B> f1, Func<I2, C> f2, Func<I3, D> f3) => Match(i0 => Union4<A, B, C, D>.From0(f0(i0)), i1 => Union4<A, B, C, D>.From1(f1(i1)), i2 => Union4<A, B, C, D>.From2(f2(i2)), i3 => Union4<A, B, C, D>.From3(f3(i3)));

        private readonly int mWhich;
        private readonly object mData;
    }
}
