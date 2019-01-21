using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using static PlayStudios.Functional.Alg;

namespace PlayStudios.Functional.Containers
{

    // Weak key; but can look up back from the TContained - useful for mapping back from values (the non-weak/'val') to identity-elements (the weak key).
    public sealed class KeyWeakBiContainer<TWeakOwningKey, TContained>
        where TWeakOwningKey : class // inherently
        where TContained : /* IEquatable<TContained>, */ IComparable /*, IComparable<TContained> */
    {

        public TWeakOwningKey GetWeakKeyFromContainedBuildingIfNecessary(TContained contained, Func<TWeakOwningKey> buildNewResourceKey) =>
            AddUtility(contained, buildNewResourceKey, (_, w) => w).Match(x => x, x => x);


        public TWeakOwningKey AddFromContainedMustNotAlreadyExist(TContained contained, Func<TWeakOwningKey> buildNewResourceKey) =>
            AddUtility<TWeakOwningKey>(contained, buildNewResourceKey, (_, __) => throw new Exception("" + contained + " already exists in KeyWeakBiContainer")).Match(x => x, x => x);


        // Need to do it this way to prevent capturing 'this'
        private static (KeyHolder, Action) NewKeyHolderAndNeutralizeKillShot(object l, Dictionary<TContained, Tuple<WeakReference<TWeakOwningKey>, Action>> containedToKey, TContained contained)
        {
            
            // this will possibly be replaced for neutralization purposes
            Action removeIfStillAllowed = () => containedToKey.Remove(contained);

            void neutralizeKillShot()
            {
                lock (l)
                {
                    removeIfStillAllowed = () => { }; // replace with do-nothing
                }
            }

            void clearOut()
            {
                lock (l)
                {
                    // got in here because resourceKey already gone byebye
                    removeIfStillAllowed(); // need lock to call right snapshot of the function - i.e. may be replaced by 'neutralize'
                }
            }


            return
                (new KeyHolder(
                    clearOut: clearOut),
                neutralizeKillShot);
        }

        private Either<TWeakOwningKey, T> AddUtility<T>(TContained contained, Func<TWeakOwningKey> buildNewResourceKey, Func<TContained, TWeakOwningKey, T> toReturnIfAlreadyExists)
        {
            lock (mLock)
            {
                foreach (var wrn in ContainedToKeyAndNeutralizeKillShot.GetValueIfPresent(contained).ToEnumerable())
                {
                    if (wrn.Item1.TryGetTarget(out var possible))
                    {
                        return Either<TWeakOwningKey, T>.FromRight(toReturnIfAlreadyExists(contained, possible));
                    }
                    else
                    {
                        // ** IMPORTANT ** - we failed to get the weak back - the weak-key is gone, ergo while there is a contained object here, it's to be considered defunct.
                        // However, theoretically, an outstanding killshot could be hanging on this lock to remove ContainedToKey.

                        wrn.Item2(); // don't want it to come back and kill the one we will add

                        ContainedToKeyAndNeutralizeKillShot.Remove(contained); // remove it ourself, now that noone else will

                        // Will fall through and rebuild the entry.
                    }
                }

                // build it.
                var resourceKey = buildNewResourceKey();

                var (newKeyHolder, neutralizeKillShot) = NewKeyHolderAndNeutralizeKillShot(mLock, ContainedToKeyAndNeutralizeKillShot, contained);
                KeyToContained.Add(
                    resourceKey,
                    Tuple.Create(
                        contained,
                        newKeyHolder));

                ContainedToKeyAndNeutralizeKillShot.Add(contained, Tuple.Create(new WeakReference<TWeakOwningKey>(resourceKey), neutralizeKillShot));
                return Either<TWeakOwningKey, T>.FromLeft(resourceKey);
            }
        }


        public Option<TWeakOwningKey> GetPossibleWeakKeyFromValue(TContained contained)
        {
            lock (mLock)
            {
                foreach (var wrn in ContainedToKeyAndNeutralizeKillShot.GetValueIfPresent(contained).ToEnumerable())
                {
                    if (wrn.Item1.TryGetTarget(out var possible))
                    {
                        return Some(possible);
                    }
                }

                return None<TWeakOwningKey>();
            }
        }

        private sealed class KeyHolder
        {
            public KeyHolder(Action clearOut)
            {
                mClearOut = clearOut;
            }

            private readonly Action mClearOut;

            ~KeyHolder()
            {
                mClearOut();
            }
        }

        private readonly string[] mLock = new string[] {" "};
        private readonly ConditionalWeakTable<TWeakOwningKey, Tuple<TContained, KeyHolder>> KeyToContained = new ConditionalWeakTable<TWeakOwningKey, Tuple<TContained, KeyHolder>>();
        private readonly Dictionary<TContained, Tuple<WeakReference<TWeakOwningKey>, Action>> ContainedToKeyAndNeutralizeKillShot = new Dictionary<TContained, Tuple<WeakReference<TWeakOwningKey>, Action>>();
    }
}
