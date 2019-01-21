using System;
using System.Runtime.CompilerServices;
using static PlayStudios.Functional.Alg;


namespace PlayStudios.Functional.Containers
{


    // Sole rationale for keeping this variant - SwapOut capability which means it needs a lock that all other operations will share (without which there'd be a gap between 'delete' and 're-add'
    // At time of writing nothing using this; but leaving the assumption in for completeness.
    public sealed class KeyWeakDictionary_SwapCapable<TWeakOwningKey, TContained>
        where TWeakOwningKey : class   // by definition, has to be a reference
        where TContained : class // underlying construct takes classes as value, only. Punting to the outer code to wrap anything that needs to be otherwise
    {
        public KeyWeakDictionary_SwapCapable()
        {
        }

        public void Add(TWeakOwningKey weakOwningKey, TContained contained)
        {
            lock (mLock)
            {
                WeakOwningKeyToContained.Add(weakOwningKey, contained);
            }
        }

        public TContained GetValue(TWeakOwningKey weakOwningKey)
        {
            lock (mLock)
            {
                return WeakOwningKeyToContained.GetValue(weakOwningKey, _ => throw new Exception("No - assumption of this wider container that key existence implies membership always - grave coding or assumption error in container."));
            }
        }

        public TContained GetAndBuildIfAbsent(TWeakOwningKey weakOwningKey, Func<TContained> buildANewOne)
        {
            lock (mLock) // key - it builds *inside* the lock
            {
                return WeakOwningKeyToContained.GetValue(weakOwningKey, k => buildANewOne());
            }
        }



        // At first glance, this may look redundant - why not return Option<TContained> then the caller build it to the new key and insert it?
        // Then, realize that the use case may have others come in from different threads with the same key!!
        // Currently, holding the full lock thwarts this outright, the next guy will simply find the value.
        // Perhaps another would be to extend the container to have a per-weak-owning-key lockable.

        // Or WeakOwningKeyToContained containing Either<Func<TContained>, TContained> and have the left portion have a closed-over 'wait' 
        // However, because ergonomically .Match(x => x(), x => x) will be inside lock (mLock) so the wait and PulseAll may have to be off of mLock
        // Minor performance matter but the Wait then has to be a loop as all are awakened.
        //
        // Better would be to have the 'Either' but simply factor the primary lock and have everyone return an 'either' out of it.
        public TContained GetAndIfAbsentBuildSplitInjectAndOuterReturnInsideTheLock(TWeakOwningKey weakOwningKey, Func<InjectAndOuterReturn<TContained>> getToInjectAndOuterReturn)
        {
            lock (mLock) // key - it builds *inside* the lock
            {
                var ifNotInitiallyFoundThisIsInjectAndOuterReturn = None<InjectAndOuterReturn<TContained>>();
                var r =
                    WeakOwningKeyToContained.GetValue(weakOwningKey, k =>
                    {
                        // weakOwningKey/k (which should be identical) are irrelevant.

                        var toInjectAndOuterReturn = getToInjectAndOuterReturn();
                        ifNotInitiallyFoundThisIsInjectAndOuterReturn = Some(toInjectAndOuterReturn); // mark outer code that this path was taken and has a return that's different to what's to be injected
                        return toInjectAndOuterReturn.ToInject;
                    });

                // Really, the to-inject decision is made inside of the underlying container's lock - this could be outside of mLock FWIW
                return
                    ifNotInitiallyFoundThisIsInjectAndOuterReturn.Match(
                        x => x.ToReturn, // injected, use the inject-return
                        () => r // found first time, return *that*
                        );
            }
        }

        public TContained SwapOutValueWithExistingAsAtomicOperation(TWeakOwningKey weakOwningKey, TContained replacementValue)
        {
            lock (mLock) // key - it builds *inside* the lock
            {
                var preReplacementValue = WeakOwningKeyToContained.GetValue(weakOwningKey, _ => throw new Exception("No - assumption of this wider container that key existence implies membership always - grave coding or assumption error in container."));
                WeakOwningKeyToContained.Remove(weakOwningKey); // the add doesn't allow a duplicate
                WeakOwningKeyToContained.Add(weakOwningKey, replacementValue);
                return preReplacementValue;
            }
        }

        private readonly string[] mLock = new string[] { " " };
        private readonly ConditionalWeakTable<TWeakOwningKey, TContained> WeakOwningKeyToContained = new ConditionalWeakTable<TWeakOwningKey, TContained>();
    }


}
