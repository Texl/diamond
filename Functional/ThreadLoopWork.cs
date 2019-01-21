using System;
using System.Threading;
using Microsoft.FSharp.Core;
using static PlayStudios.Functional.Alg;

namespace PlayStudios.Functional
{
    public static class ThreadLoopWork
    {
        // Useful for recursive core that flings its 'state' forward.
        // The pattern of making use of this could perhaps be factored into a new form of "Y".
        public delegate DDoit DDoit();

        public sealed class PollingLoopAccess
        {
            public readonly Action KillTheLoop;
            public readonly Action ForceLoopUpdateAsap;

            public PollingLoopAccess(Action killTheLoop, Action forceLoopUpdateAsap)
            {
                KillTheLoop = killTheLoop;
                ForceLoopUpdateAsap = forceLoopUpdateAsap;
            }
        }

        public static PollingLoopAccess StartUpAPollingLoopWithForceUpdateCapability(Func<TimeSpan> drawTimeSpanDesired, DDoit doit)
        {
            var l = NewLockWithControl();
            bool wantOut = false;
            bool wantForceUpdate = false;
            new Thread(
                () =>
                {
                    DDoit current = doit; // only one thread will muck with this.
                    while (true)
                    {
                        var timeStart = DateTime.Now;
                        if (
                            With(
                                l,
                                c =>
                                    wantOut))
                        {
                            return;
                        }

                        var timeSpanDesired = drawTimeSpanDesired();

                        current = current();

                        for (; ; )
                        {
                            var timeEnd = DateTime.Now;

                            var timeSpanSoFar = timeEnd - timeStart;

                            if (wantForceUpdate)
                            {
                                wantForceUpdate = false;
                                break;
                            }
                            else if (timeSpanSoFar < timeSpanDesired)
                            {
                                l(c => c.Wait(timeSpanDesired - timeSpanSoFar));
                            }
                            else
                            {
                                break;
                            }
                        }
                    }
                }).Start();

            return
                new PollingLoopAccess(
                    killTheLoop:
                    () => l(
                        c =>
                        {
                            wantOut = true;
                            c.PulseAll();
                        }),
                    forceLoopUpdateAsap:
                    () => l(
                        c =>
                        {
                            wantForceUpdate = true;
                            c.PulseAll();
                        }));
        }

        public static Action StartUpAPollingLoop(Func<TimeSpan> drawTimeSpanDesired, DDoit doit) =>
            StartUpAPollingLoopWithForceUpdateCapability(drawTimeSpanDesired, doit).KillTheLoop;

        public static DDoit GetDDoitToRunForever<T>(T initialValue, Func<T, T> doitToReplacement, Action<Exception> reportException) =>
            Y<T, DDoit>(
                recurse =>
                    Func(
                        (T currentKnown) =>
                            new DDoit(
                                () =>
                                    TryWithException(
                                        () =>
                                        {
                                            var replacement = doitToReplacement(currentKnown);
                                            return Func(() => recurse(replacement));
                                        }).Match(
                                        exception =>
                                        {
                                            reportException(exception);
                                            return recurse(currentKnown);
                                        },
                                        x => x() // calling the Func that gets a Doit here, otherwise we're double-thunking each 'doit' call.
                                    )
                            )))(initialValue);

        public static DDoit GetDDoitToRunForever(Action doit, Action<Exception> reportException) =>
            GetDDoitToRunForever(
                UnitValue,
                (Unit _) =>
                {
                    doit();
                    return UnitValue;
                },
                reportException);
    }
}
