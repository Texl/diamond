using System;

namespace PlayStudios.Functional
{
    public static class Transformation
    {
        public sealed class WithCarry<TOld, TCarry, TNew>
        {
            public WithCarry(
                Func<TOld, Tuple<TCarry, TNew>> toNew,
                Func<TNew, TOld> fromNewForCosmetic,
                Func<TCarry, TNew, TOld> fromNew)
            {
                ToNew = toNew;
                FromNewForCosmetic = fromNewForCosmetic;
                FromNew = fromNew;
            }
            public readonly Func<TOld, Tuple<TCarry, TNew>> ToNew;
            public readonly Func<TNew, TOld> FromNewForCosmetic;
            public readonly Func<TCarry, TNew, TOld> FromNew;
        }

        public static class WithCarry
        {
            public static WithCarry<TOld, TCarry, TNew> Build<TOld, TCarry, TNew>(
                Func<TOld, Tuple<TCarry, TNew>> toNew,
                Func<TNew, TOld> fromNewForCosmetic,
                Func<TCarry, TNew, TOld> fromNew) =>
                    new WithCarry<TOld, TCarry, TNew>(toNew, fromNewForCosmetic, fromNew);

        }
    }
}
