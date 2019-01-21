using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlayStudios.Functional
{
    public sealed class Trampoline<T>
    {
        public Trampoline(Either<T, Func<Trampoline<T>>> data)
        {
            Data = data;
        }
        public readonly Either<T, Func<Trampoline<T>>> Data;
    }

    public static class Trampoline
    {
        public static T RunToEnd<T>(Trampoline<T> run)
        {
            var current = run;
            while (current.Data.IsRight)
            {
                current = current.Data.RightIfPresent().Value();
            }
            return current.Data.LeftIfPresent().Value;
        }

        public static Trampoline<T> Build<T>(Either<T, Func<Trampoline<T>>> data) => new Trampoline<T>(data);
    }
}
