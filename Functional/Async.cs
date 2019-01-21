using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace PlayStudios.Functional
{
    /// <summary>
    /// Monadic Task wrapper - consider experimental until proven out.
    /// Adapted from https://blogs.msdn.microsoft.com/pfxteam/2013/04/03/tasks-monads-and-linq/
    /// </summary>
    public class Async<T>
    {
        public Async(Task<T> task)
        {
            Task = task;
        }

        // This to get the "await" syntax sugar.
        public TaskAwaiter<T> GetAwaiter() =>
            Task.GetAwaiter();

        /// <summary>
        /// You should really never need access to this in practice
        /// System.Threading.Tasks.Task has a lot of helper functions that needs this, though.
        /// </summary>
        public readonly Task<T> Task;
    }

    public static class Async
    {
        // Builders
        public static Async<T> FromTask<T>(Task<T> t) =>
            new Async<T>(t);

        public static Async<T> FromResult<T>(T r) =>
            new Async<T>(Task.FromResult(r));

        public static Async<T> FromRun<T>(Func<T> f) =>
            new Async<T>(Task.Run(f));

        public static Async<T[]> WhenAll<T>(params Async<T>[] asyncTasks) =>
            FromTask(Task.WhenAll(asyncTasks.Select(x => x.Task)));

        public static Async<T[]> WhenAll<T>(IEnumerable<Async<T>> asyncTasks) =>
            FromTask(Task.WhenAll(asyncTasks.Select(x => x.Task)));

        // Map
        public static Async<U> Select<T, U>(this Async<T> a, Func<T, U> f) =>
            new Async<U>(SelectH(a, f));

        // Bind
        public static Async<U> SelectMany<T, U>(this Async<T> a, Func<T, Async<U>> f) =>
            new Async<U>(SelectManyH(a, f));

        public static Async<V> SelectMany<T, U, V>(this Async<T> a, Func<T, Async<U>> f, Func<T, U, V> p) =>
            new Async<V>(SelectManyH2(a, f, p));

        // Helpers
        private static async Task<U> SelectH<T, U>(this Async<T> a, Func<T, U> f) =>
            f(await a);

        private static async Task<U> SelectManyH<T, U>(this Async<T> a, Func<T, Async<U>> f)
        {
            var t = await a;
            var u = await f(t);
            return u;
        }

        private static async Task<V> SelectManyH2<T, U, V>(this Async<T> a, Func<T, Async<U>> f, Func<T, U, V> p)
        {
            var t = await a;
            var u = await f(t);
            return p(t, u);
        }
    }
}
