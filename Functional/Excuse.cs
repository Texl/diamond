using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlayStudios.Functional
{
    // similar to Option<T> but provides a return type E to describe the terminal case
    public interface IExcuse<E,T>
    {
        U Match<U>(Func<E, U> ifExcuse, Func<T, U> ifResult);
        void Apply(Action<E> ifExcuse, Action<T> ifResult);
        Option<E> ExcuseIfPresent();
        Option<T> ResultIfPresent();
    }

    public class Excuse<E, T> : IExcuse<E, T>
    {
        private readonly E Value;

        public Excuse(E value)
        {
            Value = value;
        }

        public void Apply(Action<E> ifExcuse, Action<T> ifResult) => ifExcuse(Value);

        public Option<E> ExcuseIfPresent() => Option<E>.Some(Value);

        public Option<T> ResultIfPresent() => Option<T>.None();

        public U Match<U>(Func<E, U> ifExcuse, Func<T, U> ifResult) => ifExcuse(Value);
    }

    public class Result<E, T> : IExcuse<E, T>
    {
        private readonly T Value;

        public Result(T value)
        {
            Value = value;
        }

        public void Apply(Action<E> ifExcuse, Action<T> ifResult) => ifResult(Value);

        public Option<E> ExcuseIfPresent() => Option<E>.None();

        public Option<T> ResultIfPresent() => Option<T>.Some(Value);

        public U Match<U>(Func<E, U> ifExcuse, Func<T, U> ifResult) => ifResult(Value);
    }

    public static class Excuse
    {
        public static IExcuse<E, T> FromExcuse<E, T>(E value) => new Excuse<E,T>(value);
        public static IExcuse<E, T> FromResult<E, T>(T value) => new Result<E,T>(value);
        private static IExcuse<E, T> Flatten<E, T>(this IExcuse<E, IExcuse<E, T>> m) => m.Match(FromExcuse<E, T>, result => result);
        private static IExcuse<E, U> Map<E, T, U>(this IExcuse<E, T> m, Func<T, U> f) => m.Match(FromExcuse<E, U>, result => FromResult<E, U>(f(result)));
        private static IExcuse<E, U> Bind<E, T, U>(this IExcuse<E, T> m, Func<T, IExcuse<E, U>> f) => m.Map(f).Flatten();
        public static IExcuse<E, V> SelectMany<E, T, U, V>(this IExcuse<E, T> m, Func<T, IExcuse<E, U>> f, Func<T, U, V> s) => m.Bind(x => f(x).Map(y => s(x, y)));
        public static IExcuse<E, T> SelectMany<E, T>(this IExcuse<E, IExcuse<E, T>> m) => m.Flatten();
        public static IExcuse<E, U> Select<E, T, U>(this IExcuse<E, T> m, Func<T, U> f) => m.Map(f);
        public static IExcuse<E, U> Select<E, T, U>(this IExcuse<E, T> m, Func<T, IExcuse<E, U>> f) => m.Bind(f);

    }
}
