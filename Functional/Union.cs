using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlayStudios.Functional
{
    public abstract class Union<A, B, C>
    {
        public abstract void Apply(Action<A> ifA, Action<B> ifB, Action<C> ifC);
        public abstract R Match<R>(Func<A, R> ifA, Func<B, R> ifB, Func<C, R> ifC);

        public virtual Option<A> AIfPresent() => Alg.None<A>();
        public virtual Option<B> BIfPresent() => Alg.None<B>();
        public virtual Option<C> CIfPresent() => Alg.None<C>();
    }

    public static class Union
    {
        public static Union<A,B,C> Build<A,B,C>(Option<A> a, Option<B> b, Option<C> c)
        {
            return a.Match<Union<A,B,C>>(
                someA => new UnionA<A, B, C>(someA),
                () => b.Match<Union<A,B,C>>(
                    someB => new UnionB<A, B, C>(someB),
                    () => c.Match<Union<A,B,C>>(
                        someC => new UnionC<A, B, C>(someC),
                        () => { throw new Exception("Expected one of A, B, C when building Union<A,B,C>"); })));
        }

        public static Union<A, B, C> Build<A, B, C>(A a) => new UnionA<A, B, C>(a);
        public static Union<A, B, C> Build<A, B, C>(B b) => new UnionB<A, B, C>(b);
        public static Union<A, B, C> Build<A, B, C>(C c) => new UnionC<A, B, C>(c);
    }

    public sealed class UnionA<A, B, C> : Union<A, B, C>
    {
        private A Value;

        public UnionA(A value)
        {
            Value = value;
        }

        public override void Apply(Action<A> ifA, Action<B> ifB, Action<C> ifC) => ifA(Value);

        public override T Match<T>(Func<A, T> ifA, Func<B, T> ifB, Func<C, T> ifC) => ifA(Value);

        public override Option<A> AIfPresent() => Alg.Some(Value);
    }

    public sealed class UnionB<A, B, C> : Union<A, B, C>
    {
        private B Value;

        public UnionB(B value)
        {
            Value = value;
        }

        public override void Apply(Action<A> ifA, Action<B> ifB, Action<C> ifC) => ifB(Value);

        public override T Match<T>(Func<A, T> ifA, Func<B, T> ifB, Func<C, T> ifC) => ifB(Value);

        public override Option<B> BIfPresent() => Alg.Some(Value);

    }

    public sealed class UnionC<A, B, C> : Union<A, B, C>
    {
        private C Value;

        public UnionC(C value)
        {
            Value = value;
        }

        public override void Apply(Action<A> ifA, Action<B> ifB, Action<C> ifC) => ifC(Value);

        public override T Match<T>(Func<A, T> ifA, Func<B, T> ifB, Func<C, T> ifC) => ifC(Value);
        public override Option<C> CIfPresent() => Alg.Some(Value);
    }
}
