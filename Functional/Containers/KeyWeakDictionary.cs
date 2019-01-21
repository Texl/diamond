using System;
using System.Runtime.CompilerServices;
using static PlayStudios.Functional.Alg;

namespace PlayStudios.Functional.Containers
{


    public sealed class InjectAndOuterReturn<TContained>
    {
        public InjectAndOuterReturn(TContained toInject, TContained toReturn)
        {
            ToInject = toInject;
            ToReturn = toReturn;
        }

        public readonly TContained ToInject;
        public readonly TContained ToReturn;
    }

    // Dumb wrapper of ConditionalWeakTable - ergonomics. _SwapCapable alternative is heavier-weight with its own lock *solely* in support of swapping.
    public sealed class KeyWeakDictionary<TWeakOwningKey, TContained>
        where TWeakOwningKey : class   // by definition, has to be a reference
        where TContained : class // underlying construct takes classes as value, only. Punting to the outer code to wrap anything that needs to be otherwise
    {
        public KeyWeakDictionary()
        {
        }

        public void Add(TWeakOwningKey weakOwningKey, TContained contained)
        {
            // this internally locks
            WeakOwningKeyToContained.Add(weakOwningKey, contained);
        }

        public TContained GetValue(TWeakOwningKey weakOwningKey)
        {
            // internally locks
            return WeakOwningKeyToContained.GetValue(weakOwningKey, _ => throw new Exception("No - assumption of this wider container that key existence implies membership always - grave coding or assumption error in container."));
        }

        public TContained GetAndBuildIfAbsent(TWeakOwningKey weakOwningKey, Func<TContained> buildANewOne)
        {
            // internally locks
            return WeakOwningKeyToContained.GetValue(weakOwningKey, k => buildANewOne());
        }

        public T GetToAlreadyPresentOrRebuilt<T>(TWeakOwningKey weakOwningKey, Func<TContained, T> alreadyExisted, Func<(TContained, T)> buildANewOne)
        {
            Option<T> builtANewOne = None<T>();
            // internally locks
            var containedEitherWay =
                WeakOwningKeyToContained.GetValue(
                    weakOwningKey,
                    k =>
                    {
                        var (contained, t) = buildANewOne();
                        builtANewOne = Some(t);
                        return contained;
                    });

            return
                builtANewOne.Match(
                    x => x,
                    () => alreadyExisted(containedEitherWay));
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
            var ifNotInitiallyFoundThisIsInjectAndOuterReturn = None<InjectAndOuterReturn<TContained>>();

            // safe assumption - the injection determination is made inside the underlying construct's lock
            var r =
                WeakOwningKeyToContained.GetValue(
                    weakOwningKey,
                    k =>
                    {
                        // weakOwningKey/k (which should be identical) are irrelevant.

                        var toInjectAndOuterReturn = getToInjectAndOuterReturn();
                        ifNotInitiallyFoundThisIsInjectAndOuterReturn = Some(toInjectAndOuterReturn); // mark outer code that this path was taken and has a return that's different to what's to be injected
                        return toInjectAndOuterReturn.ToInject;
                    });
            return
                ifNotInitiallyFoundThisIsInjectAndOuterReturn.Match(
                    x => x.ToReturn, // injected, use the inject-return
                    () => r // found first time, return *that*
                );
        }


        public bool RemoveReturningTrueIfWasPresent(TWeakOwningKey weakOwningKey) => WeakOwningKeyToContained.Remove(weakOwningKey);

        private readonly ConditionalWeakTable<TWeakOwningKey, TContained> WeakOwningKeyToContained = new ConditionalWeakTable<TWeakOwningKey, TContained>();
    }

}
