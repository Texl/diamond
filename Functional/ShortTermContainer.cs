using System;
using System.Collections.Generic;
using System.Threading;

namespace PlayStudios.Functional
{
    public sealed class MutableValueHolder<TValue>
    {
        public MutableValueHolder(TValue initial)
        {
            MutableValue = initial;
        }
        public TValue MutableValue;
    }

    public sealed class STCValue<TValue>
    {
        public STCValue(Action<Action<MutableValueHolder<TValue>>> runWith, Action markForDeathNoTimerGuard)
        {
            RunWith = runWith;
            MarkForDeathNoTimerGuard = markForDeathNoTimerGuard;
        }

        public readonly Action<Action<MutableValueHolder<TValue>>> RunWith; // Assumed provider will put something inside the closure to capture the value/keep it alive
        public T With<T>(Func<MutableValueHolder<TValue>, T> f) => Alg.With(RunWith, f);

        public readonly Action MarkForDeathNoTimerGuard;
    }


    public sealed class ShortTermContainer<TKey, TValue>
    {
        private sealed class Entry
        {
            public Entry(MutableValueHolder<TValue> initial, int refCount)
            {
                ValueHolder = initial;
                RefCount = refCount;
            }

            public readonly MutableValueHolder<TValue> ValueHolder;
            public readonly int[] mEntryLock = new int[] {0};
            public int RefCount;
            public Action DiscardAnyTimer = () => { };
            public bool MarkedForDeathNoCountDown = false; // keep a change to this a one-way affair
        }



        private sealed class ReleaseAnchor
        {
            public ReleaseAnchor(Action release)
            {
                mRelease = release;
            }
            private readonly Action mRelease;
            ~ReleaseAnchor()
            {
                mRelease();
            }
        }

        private STCValue<TValue> AcquireSTCFromKnownEntryRefCounted(TKey key, Entry entryRefCountedHere)
        {
            Action justWipeItNotConsideringLockAtAll = () => mKeyToEntry.Remove(key);

            // have the entry, and the object isn't going to die now/for a while
            Action decRefNotConsideringLockAtAll = // do all locking of *both*
                () =>
                {
                    --entryRefCountedHere.RefCount;
                    if (entryRefCountedHere.RefCount == 0)
                    {
                        if (entryRefCountedHere.MarkedForDeathNoCountDown)
                        {
                            justWipeItNotConsideringLockAtAll();
                        }
                        else
                        {
                            // really, put it on deathwatch!

                            Timer timer = null;
                            timer = new Timer( // and null it back out when done
                                _ =>
                                {
                                    // here not in any lock
                                    lock (mThisLock) // have to preserve outer/inner locking order due to lock order in initial GET that may hit us
                                    {
                                        lock (entryRefCountedHere.mEntryLock) // take them in order
                                        {
                                            if (timer != null)
                                            {
                                                timer.Dispose();
                                                timer = null;
                                                if (entryRefCountedHere.RefCount == 0)
                                                {
                                                    justWipeItNotConsideringLockAtAll();
                                                }
                                                entryRefCountedHere.DiscardAnyTimer = () => { };
                                            }
                                        }
                                    }
                                }, null, 10000, -1);

                            entryRefCountedHere.DiscardAnyTimer =
                                () =>
                                {
                                    timer.Dispose();
                                    timer = null;
                                };
                        }
                    }
                };

            ReleaseAnchor ra = new ReleaseAnchor(() =>
            {
                lock (mThisLock) // have to preserve outer/inner locking order due to lock order in initial GET that may hit us
                {
                    lock (entryRefCountedHere.mEntryLock)
                    {
                        decRefNotConsideringLockAtAll();
                    }
                }
            });

            return new STCValue<TValue>(
                new Action<Action<MutableValueHolder<TValue>>>(
                    rw =>
                    {
                        lock (entryRefCountedHere.mEntryLock) // key - on access lock around the entry
                                                              // This technically doesn't muck with rest of operations, it only protects the mutable value
                                                              // Alternative, I guess, is the using code locks it
                                                              // An explicit policy one way or another I guess. Technically, if locks are intended to compose
                                                              // In some way it would be better to have it work some other way.
                                                              // -- The lock serves no other purpose as holding 'ra' keeps the refcount >= 1
                        {
                            rw(entryRefCountedHere.ValueHolder);
                        }
                        // ra's anchor running after this line will take the locks itself,
                        // i.e. it's cleaner to have this outside.
                        GC.KeepAlive(ra); // key - to make mere existence of this closure keep the thing alive.
                    }),
                () =>
                {
                    lock (mThisLock)
                    {
                        lock (entryRefCountedHere.mEntryLock)
                        {
                            entryRefCountedHere.DiscardAnyTimer();
                            entryRefCountedHere.MarkedForDeathNoCountDown = true;
                            // Technically, refcount will be > 0 due to still having a hold on reference to keep this alive
                            if (entryRefCountedHere.RefCount <= 0)
                            {
                                throw new Exception("Grave code error - refcount should never have gotten to 0");
                            }
                            GC.KeepAlive(ra); // key - to make mere existence of this closure keep the thing alive.    
                        }
                    }
                });
        }

        /*
        private Entry GetRefCountedEntry(TKey key, Func<TValue> buildInitialIfNotFound)
        {
            lock (mThisLock)
            {
                var possible = mKeyToEntry.GetValueIfPresent(key);
                if (possible.HasValue)
                {
                    var entry = possible.Value;
                    lock (entry.mEntryLock)
                    {
                        if (entry.MarkedForDeathNoCountDown)
                        {
                            throw new Exception("Attempting an AddRef on an entry marked for death - it's technically still there; but should only be so marked as an indicator noone would ever ask again. Reassess outside code");
                        }
                        entry.DiscardAnyTimer(); // just in case it got refcounted to 0 and counting down.
                        entry.DiscardAnyTimer = () => { };
                        ++entry.RefCount;
                        return entry;
                    }
                }
                var e = new Entry(new MutableValueHolder<TValue>(buildInitialIfNotFound()), 1);
                mKeyToEntry.Add(key, e);
                return e;
            }
        }
        */

        private Entry BuildBrandNewRefCountedEntry(TKey key, TValue initial)
        {
            lock (mThisLock)
            {
                var e = new Entry(new MutableValueHolder<TValue>(initial), 1);
                mKeyToEntry.Add(key, e);
                return e;
            }
        }

        private Option<Entry> GetRefCountedEntryIfExists(TKey key)
        {
            lock (mThisLock)
            {
                return
                    mKeyToEntry.GetValueIfPresent(key).Select(
                        entry =>
                        {
                            lock (entry.mEntryLock)
                            {
                                if (entry.MarkedForDeathNoCountDown)
                                {
                                    throw new Exception("Attempting an AddRef on an entry marked for death - it's technically still there; but should only be so marked as an indicator noone would ever ask again. Reassess outside code");
                                }
                                entry.DiscardAnyTimer(); // just in case it got refcounted to 0 and counting down.
                                entry.DiscardAnyTimer = () => { };
                                ++entry.RefCount;
                                return entry;
                            }
                        });
            }
        }

        public STCValue<TValue> Add(TKey key, TValue initial) => AcquireSTCFromKnownEntryRefCounted(key, BuildBrandNewRefCountedEntry(key, initial));
        public Option<STCValue<TValue>> GetIfExists(TKey key) => GetRefCountedEntryIfExists(key).Select(x => AcquireSTCFromKnownEntryRefCounted(key, x));

        private readonly int[] mThisLock = new int[] {0};
        private readonly Dictionary<TKey, Entry> mKeyToEntry = new Dictionary<TKey, Entry>();
    }
}
