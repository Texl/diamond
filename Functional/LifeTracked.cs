using System;
using static PlayStudios.Functional.Alg;

namespace PlayStudios.Functional
{
    public sealed class LifeTracked<T>
    {
        public sealed class Alive
        {
            public Alive(T data)
            {
                Data = data;
            }
            public readonly T Data;
        }

        public void Activate(T data, Action kill) =>
            mLock(
                () =>
                {
                    mIfAlive.ForEach(_ => throw new Exception("Lifetime already initialized!"));
                    mIfAlive = Some((alive: new Alive(data: data), kill: kill));
                    ++mActivationCount;
                });

        public void Shutdown() =>
            mLock(
                () =>
                {
                    mIfAlive.Apply(x => x.kill(), () => throw new Exception("Trying to kill with no initialization in the first place!"));
                    mIfAlive = None<(Alive alive, Action kill)>();
                });

        public T Data =>
            With(
                mLock,
                () =>
                    mIfAlive.Match(
                        x => x.alive.Data,
                        () => throw new Exception("Data not live. Activate/Shutdown has been done " + mActivationCount + " times")));



        private readonly Action<Action> mLock = NewLock();
        private Option<(Alive alive, Action kill)> mIfAlive = None<(Alive alive, Action kill)>();
        private int mActivationCount = 0;
    }

    public static class LifeTracked
    {
        public static LifeTracked<T> NewUnstarted<T>() => new LifeTracked<T>();
    }
}
