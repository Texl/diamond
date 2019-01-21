using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using static PlayStudios.Functional.Alg;

namespace PlayStudios.Functional.Containers
{
    // for case where 'last man standing' keeps the item alive
    // Key must be a class inherently
    // Contained - deferring to the caller to wrap it in a class if necessary
    public sealed class KeyWeakSharedSoleItemContainer<TWeakOwningKey, TSharedContained>
        where TWeakOwningKey : class // by definition, has to be a reference

        where TSharedContained : class // by definition, need this to be a class for it to have a weakref of its own for reacquisition if all keys go away.
    // **TECHNICALLY** could stop going weak on this and take the restriction away, then lose reacquisition-without-recreate.

    {
        public KeyWeakSharedSoleItemContainer(TSharedContained contained)
        {
            PossibleRecovery = new WeakReference<TSharedContained>(contained);
        }


        // Necessary to prevent capture of 'this'. Caused some mayhem, basically, you capture 'this' you end up with this in the watcher on the right, of the
        // ConditionalWeakTable; but the 'this' points back to the table itself.
        private static WeakOwningKeyLifeWatcher NewWeakOwningKeyLifeWatcher(object l, Dictionary<Guid, WeakReference<TWeakOwningKey>> idToWeakOfOwningKey, Guid newKeyHolderID) =>
            new WeakOwningKeyLifeWatcher(
                () =>
                {
                    lock (l)
                    {
                        idToWeakOfOwningKey.Remove(newKeyHolderID);
                    }
                });


        public void AddUnrepresentedKeyWithRecreateIfMissing(TWeakOwningKey weakOwningKey, Func<TSharedContained> toCreateIfMissing)
        {
            var newKeyHolderID = Guid.NewGuid();
            var newKeyHolder = NewWeakOwningKeyLifeWatcher(mLock, IDToWeakOfOwningKey, newKeyHolderID);

            lock (mLock)
            {
                if (PossibleRecovery.TryGetTarget(out var currentContained))
                {
                    WeakOwningKeyToContained.Add(weakOwningKey, Tuple.Create(currentContained, newKeyHolder)); // can fail/grenade
                    IDToWeakOfOwningKey.Add(newKeyHolderID, new WeakReference<TWeakOwningKey>(weakOwningKey));
                }
                else
                {
                    currentContained = toCreateIfMissing();
                    PossibleRecovery.SetTarget(currentContained);
                    WeakOwningKeyToContained.Add(weakOwningKey, Tuple.Create(currentContained, newKeyHolder)); // can fail/grenade
                    IDToWeakOfOwningKey.Add(newKeyHolderID, new WeakReference<TWeakOwningKey>(weakOwningKey));
                }
            }
        }

        public sealed class AddKeyResult
        {
            public AddKeyResult(bool addedWeakOwningKeyAsNew, bool createdIfMissing)
            {
                AddedWeakOwningKeyAsNew = addedWeakOwningKeyAsNew;
                CreatedIfMissing = createdIfMissing;
            }
            public readonly bool AddedWeakOwningKeyAsNew;
            public readonly bool CreatedIfMissing;
        }

        public AddKeyResult AddKeyIfNotAlreadyPresentWithRecreateIfMissing(TWeakOwningKey weakOwningKey, Func<TSharedContained> toCreateIfMissing)
        {
            //var newKeyHolderID = Guid.NewGuid();
            //var newKeyHolder = NewWeakOwningKeyLifeWatcher(mLock, IDToWeakOfOwningKey, newKeyHolderID);

            lock (mLock)
            {
                if (PossibleRecovery.TryGetTarget(out var currentContained))
                {
                    if (WeakOwningKeyToContained.TryGetValue(weakOwningKey, out var existing))
                    {   // already exists - do nothing
                        return new AddKeyResult(addedWeakOwningKeyAsNew: false, createdIfMissing: false); // one way or another, it was already there
                    }
                    else
                    {
                        var newKeyHolderID = Guid.NewGuid();
                        var newKeyHolder = NewWeakOwningKeyLifeWatcher(mLock, IDToWeakOfOwningKey, newKeyHolderID);
                        WeakOwningKeyToContained.Add(weakOwningKey, Tuple.Create(currentContained, newKeyHolder)); // should never fail - no existing
                        IDToWeakOfOwningKey.Add(newKeyHolderID, new WeakReference<TWeakOwningKey>(weakOwningKey));
                        return new AddKeyResult(addedWeakOwningKeyAsNew: true, createdIfMissing: false);
                    }

                }
                else
                {
                    if (WeakOwningKeyToContained.TryGetValue(weakOwningKey, out var existing))
                    {   // already exists - do nothing
                        throw new Exception("Contradiction - Recovery value doesn't exist but value slot does!");
                    }
                    else
                    {
                        currentContained = toCreateIfMissing();
                        PossibleRecovery.SetTarget(currentContained);
                        var newKeyHolderID = Guid.NewGuid();
                        var newKeyHolder = NewWeakOwningKeyLifeWatcher(mLock, IDToWeakOfOwningKey, newKeyHolderID);
                        WeakOwningKeyToContained.Add(weakOwningKey, Tuple.Create(currentContained, newKeyHolder)); // can fail/grenade
                        IDToWeakOfOwningKey.Add(newKeyHolderID, new WeakReference<TWeakOwningKey>(weakOwningKey));
                        return new AddKeyResult(addedWeakOwningKeyAsNew: true, createdIfMissing: true);
                    }

                }

                /*
            if (PossibleRecovery.TryGetTarget(out var currentContained))
            {
                IDToWeakOfOwningKey.Add(newKeyHolderID, new WeakReference<TWeakOwningKey>(weakOwningKey));


            }
            else
            {
                IDToWeakOfOwningKey.Add(newKeyHolderID, new WeakReference<TWeakOwningKey>(weakOwningKey));


                currentContained = toCreateIfMissing();
                PossibleRecovery.SetTarget(currentContained);
            }

            if (WeakOwningKeyToContained.TryGetValue(weakOwningKey, out var existing))
            {   // already exists
                WeakOwningKeyToContained.Remove()
            }
            else
            {
                WeakOwningKeyToContained.Add(weakOwningKey, Tuple.Create(currentContained, newKeyHolder));
            }
            */
            }
        }


        // This would be 'always' if Shared were made a hard direct reference
        public void AddKeyIfValueAvailable(TWeakOwningKey weakOwningKey)
        {

            lock (mLock)
            {
                if (PossibleRecovery.TryGetTarget(out var currentContained))
                {
                    var newKeyHolderID = Guid.NewGuid();
                    var newKeyHolder = NewWeakOwningKeyLifeWatcher(mLock, IDToWeakOfOwningKey, newKeyHolderID);
                    IDToWeakOfOwningKey.Add(newKeyHolderID, new WeakReference<TWeakOwningKey>(weakOwningKey));
                    WeakOwningKeyToContained.Add(weakOwningKey, Tuple.Create(currentContained, newKeyHolder));
                }
            }
        }

        public TSharedContained GetFromWeakOwningKey(TWeakOwningKey weakOwningKey) // resource key is more a guard at this stage that you were ever an intentional client.
        {
            lock (mLock)
            {
                return WeakOwningKeyToContained.GetValue(weakOwningKey, _ => throw new Exception("No - assumption of this wider container that key existence implies membership always - grave coding or assumption error in container.")).Item1;
            }
        }

        public Option<TSharedContained> GetContainedIfAvailable()
        {
            lock (mLock)
            {
                return PossibleRecovery.TryGetTarget(out var currentContained) ? Some(currentContained) : None<TSharedContained>();
            }
        }

        public void RemoveKeyIfStillThere(TWeakOwningKey weakOwningKey)
        {
            lock (mLock)
            {
                WeakOwningKeyToContained.Remove(weakOwningKey);
            }
        }

        public TWeakOwningKey[] GetLiveKeys()
        {
            lock (mLock)
            {
                return
                    IDToWeakOfOwningKey.SelectMany(
                        kv => kv.Value.TryGetTarget(out var r) ? Some(r) : None<TWeakOwningKey>()).ToArray();
            }
        }


#if DEBUG
        public int __Diagnostic_Only_CountLiveKeys
        {
            get
            {
                lock (mLock)
                {
                    return
                        IDToWeakOfOwningKey.Sum(
                            kv => kv.Value.TryGetTarget(out TWeakOwningKey r) ? 1 : 0);
                }
            }
        }
#endif


        // One job - on destruction, call 'clearOut' which basically removes IDToWeakOfOwningKey.Delete() in safety.
        private sealed class WeakOwningKeyLifeWatcher
        {
            public WeakOwningKeyLifeWatcher(Action clearOut)
            {
                mClearOut = clearOut;
            }

            private readonly Action mClearOut;

            ~WeakOwningKeyLifeWatcher()
            {
                mClearOut();
            }
        }

        private readonly string[] mLock = new string[] {" "};
        private readonly WeakReference<TSharedContained> PossibleRecovery; // only relevant if the dictionary empties but the object happens to be around.

        // KeyHolder just an optimization, when it becomes eligible it may finalize and cull the dead weakOwningKey's guid from IDToWeakOfResourceKey

        // Remember, if the 'weakowningkeylifewatcher' or anything to the right points indirectly back to the container it's a leak
        private readonly ConditionalWeakTable<TWeakOwningKey, Tuple<TSharedContained, WeakOwningKeyLifeWatcher>> WeakOwningKeyToContained = new ConditionalWeakTable<TWeakOwningKey, Tuple<TSharedContained, WeakOwningKeyLifeWatcher>>();

        private readonly Dictionary<Guid, WeakReference<TWeakOwningKey>> IDToWeakOfOwningKey = new Dictionary<Guid, WeakReference<TWeakOwningKey>>();
        
        
    }
}
