using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using static PlayStudios.Functional.Alg;


namespace PlayStudios.Functional
{
    public sealed class ThreadWorkGroup : IDisposable
    {
        public ThreadWorkGroup(Func<Func<object>, Func<object>> add, Action dispose)
        {
            mAdd = add;
            mDispose = dispose;
        }

        public Func<T> AddWorkUnit<T>(Func<T> workUnit)
        {
            var r = mAdd(() => (object)workUnit());
            return () => (T)r();
        }

        public void Dispose() => mDispose();

        private readonly Func<Func<object>, Func<object>> mAdd;
        private readonly Action mDispose;
    }

    public static class ForThreadWorkGroup
    {


        private sealed class AboutThisThread
        {

            // Both guids currently a 'red herring' - as the threadlocal is in scope of a group that's the first, and the second is never used - the optionality is effectively a flag
            public Option<(Guid forThreadWorkGroup, Guid forThisSpecific)> IfFromWorkGroupGroupAndThread = None<(Guid forThreadWorkGroup, Guid forGuid)>();
        }


        public static ThreadWorkGroup BuildThreadWorkGroup(int numThreads)
        {
            Guid thisGroup = Guid.NewGuid();



            var workBacklogLock = NewLockWithControl();
            int numThreadsStillRunning = 0;
            int numOfOurThreadsWaiting = 0;

            List<Action> outstandingWorkUnits = new List<Action>();


            int GetIdealThreadCountNow() => numThreads + numOfOurThreadsWaiting;

            // Call these in workBacklogLock
            bool AtLeastOneThreadShouldGoAway() => (!outstandingWorkUnits.Any()) || (numThreadsStillRunning > GetIdealThreadCountNow());

            bool AtLeastOneThreadShouldBeAdded() => numThreadsStillRunning < GetIdealThreadCountNow();

            // This would technically be safe at the toplevel due to the fact that it's only populated and given any form due to threads fired up within a specific thread group
            // Effectively this just as of writing acts as a flag from the expicitly set internal option's optionality
            ThreadLocal<AboutThisThread> threadLocal = new ThreadLocal<AboutThisThread>(() => new AboutThisThread());



            void ActuallyStartWorkerThreadCallerResponsibleForUppingNumThreadsStillRunning(Guid idThisThread) =>
                new Thread(
                        () =>
                        {
                            var att = threadLocal.Value;
                            if (att.IfFromWorkGroupGroupAndThread.HasValue)
                            {
                                throw new Exception("Grave grave error, threadlocal should have left that thing uninitialized!");
                            }

                            att.IfFromWorkGroupGroupAndThread = Some((thisGroup, idThisThread));



                            for (; ; )
                            {
                                Action run = null;
                                if (With( // inner returns true to break out of loop
                                    workBacklogLock,
                                    inLockInner =>
                                    {
                                        if (!AtLeastOneThreadShouldGoAway())
                                        {
                                            run = outstandingWorkUnits[0];
                                            outstandingWorkUnits.RemoveAt(0);
                                            return false; // don't break out of loop - still have work to do
                                        }
                                        else
                                        {
                                            --numThreadsStillRunning;
                                            // This is how the old worked, doing a pulseall when a worker ends.
                                            // But technically, nothing else is waiting but a single value draw
                                            inLockInner.PulseAll(); // wake up the rest
                                            return true; // break out of loop - no more work to do
                                        }
                                    }))
                                {
                                    break;
                                }

                                run();

                            }
                        }).
                        Start();

            Guid AssumeHaveWorkBacklogLockStartOneThread()
            {
                ++numThreadsStillRunning;
                // fire one up to take this then any other work units
                Guid idThisThread = Guid.NewGuid();
                ActuallyStartWorkerThreadCallerResponsibleForUppingNumThreadsStillRunning(idThisThread);
                return idThisThread;
            }


            return
                new ThreadWorkGroup(
                    add:
                    buildActual =>
                    {
                        var workUnitLock = NewLockWithControl();
                        bool finalValueReady = false;
                        Func<object> getFinalValueOrThrow = null; // Hold the exception in here



                        void WorkUnit()
                        {
                            object val = null;
                            Exception exceptionOnFatal = null;
                            // Gate on exceptionOnFatal being non-null

                            try
                            {
                                val = buildActual(); // deliberately do the work outside of any lock
                            }
                            catch (Exception ex)
                            {
                                exceptionOnFatal = ex;
                            }

                            workUnitLock(
                                inLock =>
                                {
                                    finalValueReady = true;
                                    getFinalValueOrThrow = (exceptionOnFatal != null)
                                        ? new Func<object>(() => { throw new Exception("Uncaught error in work unit:", exceptionOnFatal); })
                                        : Func(() => val);
                                    inLock.PulseAll();
                                });
                        }

                        //Guid idThisThread = Guid.NewGuid();

                        var idForThreadIfWeStartedOneHere = // hmm probably don't care about its identity
                            With(
                                workBacklogLock,
                                _ =>
                                {
                                    outstandingWorkUnits.Add(WorkUnit);
                                    return When(AtLeastOneThreadShouldBeAdded(), AssumeHaveWorkBacklogLockStartOneThread);
                                });

                        object GetFinalValueBlocking() =>
                            With(
                                workUnitLock,
                                inLock =>
                                {
                                    for (; ; )
                                    {
                                        if (finalValueReady)
                                        {
                                            if (getFinalValueOrThrow == null) // this is to distinguish a possibly incorporated exception from a simple 'this just wasn't provided a value candidate'
                                            {
                                                throw new Exception("getFinalValueOrThrow never set - this wasn't an inner exception on a worker thread; but a logical error within BuildThreadWorkGroup's mechanism");
                                            }

                                            return getFinalValueOrThrow();
                                        }

                                        // Here's the trick - the person waiting - may himself be in the same thread thing!
                                        threadLocal.Value.IfFromWorkGroupGroupAndThread.Where(x => x.forThreadWorkGroup.Equals(thisGroup)).Select(x => x.forThisSpecific).Apply(
                                            (idForThreadAboutToWait) =>
                                            {
                                                // one of ours - we're gonna stop, so mark it such and maybe make another thread.
                                                ++numOfOurThreadsWaiting;
                                                if (AtLeastOneThreadShouldBeAdded())
                                                {
                                                    AssumeHaveWorkBacklogLockStartOneThread();
                                                }
                                                inLock.Wait();
                                                --numOfOurThreadsWaiting;
                                            },
                                            inLock.Wait);
                                    }
                                });

                        return GetFinalValueBlocking;
                    },
                    dispose:
                    () =>
                    {
                        workBacklogLock(
                            inLock =>
                            {
                                while (numThreadsStillRunning > 0)
                                {
                                    inLock.Wait();
                                }
                            });
                    });
        }
    }
}
