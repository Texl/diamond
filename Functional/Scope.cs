using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PlayStudios.Functional;

namespace PlayStudios.Functional
{
    public static class Scoping
    {
        public static Func<ValueCloseFinally<Tuple<Outer, Inner>>> GetMerged<Outer, Inner>(
            Func<ValueCloseFinally<Outer>> getOuter,
            Func<ValueCloseFinally<Inner>> getInner)
        {
            return
                () =>
                    {
                        var t = getOuter(); // if this fails let it through
                        bool tFinalized = false;
                        // if it throws here, who cares - no need to run "finally" for t.
                        try
                        {
                            var u = getInner();
                            bool uFinalized = false;
                            return new ValueCloseFinally<Tuple<Outer, Inner>>(
                                Tuple.Create(t.Value, u.Value),
                                () =>
                                    {
                                        u.Close();

                                        uFinalized = true; // set this flag in *advance* - if we throw don't retry
                                        u.Finally();
                                        t.Close();
                                    },
                                () =>
                                    {
                                        try
                                        {
                                            if (!uFinalized) // may have been done before closing T
                                            {
                                                u.Finally();
                                            }
                                        }
                                        finally
                                        {
                                            tFinalized = true;
                                            t.Finally();
                                        }
                                    }
                                );
                        }
                        catch (Exception) // this should just catch if getInner() call fails
                        {
                            if (! tFinalized)
                            {
                                t.Finally();
                            }
                            throw;
                        }
                };
        }

        public static Action<Action<T>> GetRunner<T>(Func<ValueCloseFinally<T>> getIt)
        {
            return
                f =>
                    {
                        var g = getIt();
                        try
                        {
                            f(g.Value);
                            g.Close();
                        }
                        finally
                        {
                            g.Finally();
                        }
                    };
        }

        public static Func<Func<T, R>, R> GetCaller<T, R>(Func<ValueCloseFinally<T>> getIt)
        {
            var runner = GetRunner(getIt);
            return rw => Alg.With(runner, rw);
        }


        // These "merged" functions are temporary - may switch to working in terms of Func<ValueClose...>
        public static Scope<Tuple<I0, I1>> Merged<I0, I1>(Scope<I0> i0, Scope<I1> i1)
        {
            return new Scope<Tuple<I0, I1>>(GetMerged(i0.GetValueCloseFinally, i1.GetValueCloseFinally));
        }
        public static Scope<Tuple<Tuple<I0, I1>, I2>> Merged<I0, I1, I2>(Scope<I0> i0, Scope<I1> i1, Scope<I2> i2)
        {
            return new Scope<Tuple<Tuple<I0, I1>, I2>>(GetMerged(GetMerged(i0.GetValueCloseFinally, i1.GetValueCloseFinally), i2.GetValueCloseFinally));
        }




    }


    public sealed class ValueCloseFinally<TValue>
    {
        public ValueCloseFinally(TValue value, Action close, Action finally_)
        {
            Value = value;
            Close = close;
            Finally = finally_;
        }

        public TValue Value { get; private set; }
        public Action Close { get; private set; }
        public Action Finally { get; private set; }
    }

    public sealed class Scope<T>
    {
        public Scope(Func<ValueCloseFinally<T>> getValueCloseFinally)
        {
            GetValueCloseFinally = getValueCloseFinally;
            Run = Scoping.GetRunner(GetValueCloseFinally);
        }

        public Scope(T value)
        {
            GetValueCloseFinally = () => new ValueCloseFinally<T>(value, () => { }, () => { });
            Run = Scoping.GetRunner(GetValueCloseFinally);
        }

        public Action<Action<T>> Run { get; private set; }

        public TReturn With<TReturn>(Func<T, TReturn> f)
        {
            return Alg.With(Run, f);
        }

        // For cases where you have someScoping trivial like the "this" being a lock that merely protects a known pointer.
        // Essentially, this remaps it from one type to another.
        public Scope<U> FromTrivialInnerAddition<U>(Func<T, U> f)
        {
            return new Scope<U>(() =>
                {
                    var item = GetValueCloseFinally();

                    try
                    {
                        return new ValueCloseFinally<U>(f(item.Value), item.Close, item.Finally);
                    }
                    catch
                    {
                        item.Finally();
                        throw;
                    }
                });
        }

        // kept in order to have the type to build a Scope<2>
        public Func<ValueCloseFinally<T>> GetValueCloseFinally { get; private set; }
    }
}
