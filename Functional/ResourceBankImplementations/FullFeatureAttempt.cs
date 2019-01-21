using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.FSharp.Collections;
using static PlayStudios.Functional.Alg;

namespace PlayStudios.Functional.ResourceBankImplementations
{

#if false
    public sealed class FullFeatureAttempt
    {
        private sealed class ObjectPresent
        {
            public ObjectPresent(object obj, FSharpSet<Guid> majorsStillOwedValue, FSharpSet<Guid> minorsStillOwedValue)
            {
                Obj = obj;
                MajorsStillOwedValue = majorsStillOwedValue;
                MinorsStillOwedValue = minorsStillOwedValue;
            }
            public readonly object Obj;
            public readonly FSharpSet<Guid> MajorsStillOwedValue;
            public readonly FSharpSet<Guid> MinorsStillOwedValue;
        }


        private sealed class ObjectSlot
        {
            // keeping 'stillowed' so every draw deducts the value
            public Option<ObjectPresent> PossibleObjectAndChildRefsStillOwedValue = None<ObjectPresent>();
        }

        private sealed class DW<T>
        {
            public DW(T value, Action onDestroy)
            {
                Value = value;
                mOnDestroy = onDestroy;
            }
            public readonly T Value;
            private readonly Action mOnDestroy;

            ~DW()
            {
                mOnDestroy();
            }
        }

        private FullFeatureAttempt()
        {
        }

        public static FullFeatureAttempt BuildNew() => new FullFeatureAttempt();

        public enum ResourceChain { }; // QID based on this should be deterministic


        private sealed class NodeCommon
        {
            public NodeCommon(object lockThisChain, Func<ObjectSlot, Action> getKillForNewSlotAssumesLock, Action checkForMatchAndRebuildAssumingAlreadyLocked)
            {
                LockThisChain = lockThisChain;
                GetKillForNewSlotAssumesLock = getKillForNewSlotAssumesLock;
                CheckForMatchAndRebuildAssumingAlreadyLocked = checkForMatchAndRebuildAssumingAlreadyLocked;
            }
            public readonly object LockThisChain;
            public readonly Func<ObjectSlot, Action> GetKillForNewSlotAssumesLock;
            public readonly Action CheckForMatchAndRebuildAssumingAlreadyLocked;
        }

        private delegate void DAlterTotalMajorCountToParent(Func<int, int> changeNumber);
        private sealed class Node
        {
            public Node(NodeCommon nodeCommon, ObjectSlot objectSlot, Func<object> recreate, bool isForMinor, Option<DAlterTotalMajorCountToParent> parent)
            {
                NodeCommon = nodeCommon;
                ObjectSlot = objectSlot;
                Recreate = recreate;
                IsForMinor = isForMinor;
                Parent = parent;
            }

            public readonly NodeCommon NodeCommon;
            public readonly ObjectSlot ObjectSlot;
            public readonly Func<object> Recreate;
            public readonly Option<DAlterTotalMajorCountToParent> Parent; // parent makes recreate pseudor-redundant in that it captures it - albeit via its DW<>
            public readonly bool IsForMinor;
            public FSharpSet<Guid> MajorChildReferencesBackToThis = SetModule.Empty<Guid>();
            public FSharpSet<Guid> MinorChildReferencesBackToThis = SetModule.Empty<Guid>();
            public FSharpMap<Guid, int> ChildRefToMajorCountItRepresents = MapModule.Empty<Guid, int>(); // never contains a zero, delete

            public object GetValueHereAssumingAlreadyLocked()
            {
                if (ObjectSlot.PossibleObjectAndChildRefsStillOwedValue.HasValue)
                {
                    return ObjectSlot.PossibleObjectAndChildRefsStillOwedValue.Value.Obj;
                }
                var r = Recreate();
                ObjectSlot.PossibleObjectAndChildRefsStillOwedValue = Some(new ObjectPresent(r, MajorChildReferencesBackToThis, MinorChildReferencesBackToThis));
                return r;
            }

        }


        private static void AlterTotalMajorsHereandDown(Guid childIdentity, Node node, Func<int, int> modifyTotalMajors /* to support increment and decrement */)
        {
            var existing = node.ChildRefToMajorCountItRepresents.GetValueIfPresent(childIdentity);

            if (existing.HasValue)
            {
                var newValue = modifyTotalMajors(existing.Value);
                node.ChildRefToMajorCountItRepresents = (newValue == 0) ? node.ChildRefToMajorCountItRepresents.Remove(childIdentity) : node.ChildRefToMajorCountItRepresents.Add(childIdentity, newValue);
            }
            else
            {
                int newValue = modifyTotalMajors(0);
                if (newValue <= 0)
                {
                    throw new Exception("Grave error - should never be sending in zero or subtract and not have a major known of rootward in the tree - should only be in this branch in the positive case");
                }
                node.ChildRefToMajorCountItRepresents = node.ChildRefToMajorCountItRepresents.Add(childIdentity, newValue);
            }

            if (node.Parent.HasValue)
            {
                node.Parent.Value(modifyTotalMajors);
            }
        }

        // important - building sub need to keep its value-generating ability alive
        private static DW<Node> BuildSubNode(DW<Node> parent, Func<object, object> f, bool isForMinor) // don't need thread to do this, only lock when GetKill is called as matter of course
        {
            var subsObjectSlot = new ObjectSlot();
            var nodeCommon = parent.Value.NodeCommon;

            var myGuidRelativeToParent = Guid.NewGuid();
            var underlyingKill =
                Func(
                    () =>
                    {
                        lock (nodeCommon.LockThisChain)
                        {
                                // A prior revision had this lock-assuming call outside the lock, a probable/obvious cause of symptom A.H. found in a 3/2017 bulk test.
                                var returnedUnderlyingKill = nodeCommon.GetKillForNewSlotAssumesLock(subsObjectSlot);


                                // add childrefs to parent
                                if (isForMinor)
                            {
                                parent.Value.MinorChildReferencesBackToThis = parent.Value.MinorChildReferencesBackToThis.Add(myGuidRelativeToParent);
                            }
                            else
                            {
                                parent.Value.MajorChildReferencesBackToThis = parent.Value.MajorChildReferencesBackToThis.Add(myGuidRelativeToParent);
                            }

                            var originalObjectSlot = parent.Value.ObjectSlot;
                            if (originalObjectSlot.PossibleObjectAndChildRefsStillOwedValue.HasValue)
                            {
                                var ot = originalObjectSlot.PossibleObjectAndChildRefsStillOwedValue.Value;
                                originalObjectSlot.PossibleObjectAndChildRefsStillOwedValue = Some(new ObjectPresent(ot.Obj, isForMinor ? ot.MajorsStillOwedValue : ot.MajorsStillOwedValue.Add(myGuidRelativeToParent), isForMinor ? ot.MinorsStillOwedValue.Add(myGuidRelativeToParent) : ot.MinorsStillOwedValue));
                            }
                            if (!isForMinor)
                            {
                                AlterTotalMajorsHereandDown(myGuidRelativeToParent, parent.Value, x => x + 1);
                            }

                            return returnedUnderlyingKill;
                        }
                    })();

            Action decrefParentAssumingLock =
                () =>
                {
                    var parentObjectSlot = parent.Value.ObjectSlot;
                    if (parentObjectSlot.PossibleObjectAndChildRefsStillOwedValue.HasValue)
                    {
                        var ot = parentObjectSlot.PossibleObjectAndChildRefsStillOwedValue.Value;
                        parentObjectSlot.PossibleObjectAndChildRefsStillOwedValue = Some(new ObjectPresent(ot.Obj, isForMinor ? ot.MajorsStillOwedValue : ot.MajorsStillOwedValue.Remove(myGuidRelativeToParent), isForMinor ? ot.MinorsStillOwedValue.Remove(myGuidRelativeToParent) : ot.MinorsStillOwedValue));

                            // Here is where we'd compare the 'stillOwedValue' to 0, and, if the Node is for a minor/not needed for its own sake, obliterate its own cached value.
                            // Remember, originalObjectSlot refers to the parent - have we refcounted it down to zero?

                            // Here's the real trick - if none in the ChildRefToMajorCountItRepresents are represented in either of the pathway arrays, this means
                            // noone under you, who is or has a major, is owed anything.

                            var op = parentObjectSlot.PossibleObjectAndChildRefsStillOwedValue.Value;
                        if (parent.Value.IsForMinor
                            &&
                            (!op.MajorsStillOwedValue.Any(parent.Value.ChildRefToMajorCountItRepresents.ContainsKey))
                            &&
                            (!op.MinorsStillOwedValue.Any(parent.Value.ChildRefToMajorCountItRepresents.ContainsKey)))
                        {
                            parentObjectSlot.PossibleObjectAndChildRefsStillOwedValue = None<ObjectPresent>();
                        }
                    }
                };

            return
                new DW<Node>(
                    new Node(
                        nodeCommon,
                        subsObjectSlot,
                        () =>
                        {
                            var backsValue = parent.Value.GetValueHereAssumingAlreadyLocked();
                                // This *should* have forced it to exist.
                                decrefParentAssumingLock(); // we took our view of it.
                                return f(backsValue);
                        } // the capture of original and its DW here is *crucial*
                        , isForMinor,
                        Some(new DAlterTotalMajorCountToParent(ff => AlterTotalMajorsHereandDown(myGuidRelativeToParent, parent.Value, ff)))),
                    () =>
                    {
                        lock (nodeCommon.LockThisChain)
                        {
                                // Remove childrefs from parent - we're done
                                if (isForMinor)
                            {
                                parent.Value.MinorChildReferencesBackToThis = parent.Value.MinorChildReferencesBackToThis.Remove(myGuidRelativeToParent);
                            }
                            else
                            {
                                parent.Value.MajorChildReferencesBackToThis = parent.Value.MajorChildReferencesBackToThis.Remove(myGuidRelativeToParent);
                            }

                                // Going away - if not a minor, deduct from parent and any under's count of how many majors are interested
                                if (!isForMinor)
                            {
                                AlterTotalMajorsHereandDown(myGuidRelativeToParent, parent.Value, x => x - 1);
                            }
                                // Important to change majors count first in that decrefParent will have the parent assess if anyone cares about its value

                                decrefParentAssumingLock();


                            underlyingKill();
                        }
                    });

        }

        private sealed class Contents<T> : IRContents<T>
        {
            public Contents(DW<Node> node)
            {
                mNode = node;
            }

            public IRContents<U> Transformed<U>(Func<T, U> f) => new Contents<U>(BuildSubNode(mNode, i => (object)f((T)i), false));
            public IRContentsMinor<U> TransformedToMinor<U>(Func<T, U> f) => new ContentsMinor<U>(BuildSubNode(mNode, i => (object)f((T)i), true));

            public Action<Action<T>> RunWith => RunWithIt;

            private T JustGetTheObjectScopeDoesntMatterInThisRealm()
            {
                var node = mNode.Value;
                var nodeCommon = node.NodeCommon;


                lock (nodeCommon.LockThisChain)
                {
                    nodeCommon.CheckForMatchAndRebuildAssumingAlreadyLocked();
                    return (T)node.GetValueHereAssumingAlreadyLocked();
                }
            }

            public T PullSnapshotHereAsOptimization() => JustGetTheObjectScopeDoesntMatterInThisRealm();

            private readonly DW<Node> mNode;
            private void RunWithIt(Action<T> rw) => rw(JustGetTheObjectScopeDoesntMatterInThisRealm());
        }

        private sealed class ContentsMinor<T> : IRContentsMinor<T>
        {
            public ContentsMinor(DW<Node> node)
            {
                mNode = node;
            }

            public IRContents<U> Transformed<U>(Func<T, U> f) => new Contents<U>(BuildSubNode(mNode, i => (object)f((T)i), false));
            public IRContentsMinor<U> TransformedToMinor<U>(Func<T, U> f) => new ContentsMinor<U>(BuildSubNode(mNode, i => (object)f((T)i), true));

            private readonly DW<Node> mNode;
        }

        private sealed class PerResourceChain
        {
            public PerResourceChain(Action wipeAll)
            {
                WipeAll = wipeAll;
            }
            public readonly Action WipeAll;
        }

        private QID<ResourceChain> AllocateResourceChainIDTakingMasterLock()
        {
            lock (MasterLock)
            {
                var r = NextResourceChainOrdinal;
                ++NextResourceChainOrdinal;
                return QID.Build<ResourceChain>(r);
            }
        }

        public sealed class NewResourceChain<T>
        {
            public NewResourceChain(T value, Action wipe)
            {
                Value = value;
                Wipe = wipe;
            }
            public readonly T Value;
            public readonly Action Wipe;
        }




        // Ultimately need a different opener for providing R<T> vs Rm<T>
        private NewResourceChain<DW<Node>> SimpleRootCase<T>(OfResourceBank.DResourceSourceDefinition<T> resourceSourceDefinition, bool isForMinor)
        {
            object lockThisChain = new string[] { "" };
            // var calculatedObjectSlots = new LifetimeBase<ObjectSlot>();
            var objectSlot = new ObjectSlot();



            Func<OfResourceBank.IdentityAndDraw<T>> currentGetIdentityAndGetDraw_thisMutatesAndIsClosedOver = null;
            OfResourceBank.IdentityAndDraw<T> currentIdentityAndGetDraw_thisMutatesAndIsClosedOver = null;




            Dictionary<Guid, ObjectSlot> perSurvivor = new Dictionary<Guid, ObjectSlot>();

            var resourceChainID = AllocateResourceChainIDTakingMasterLock();


            Func<ObjectSlot, Action> addAndGetKillAssumesLock =
                value =>
                {
                        // current implementation capturing *this*
                        var insertID = Guid.NewGuid();
                        // lock (theLock)
                        {
                        perSurvivor.Add(insertID, value);
                        return
                            () => // this is the returned 'kill' funciton - it takes its own lock.
                                {
                                bool removeWholeChain = false; // okay to fill this in lock because once true, it's all over anyway
                                    lock (lockThisChain)
                                {
                                    perSurvivor.Remove(insertID);
                                    if (!perSurvivor.Any())
                                    {
                                        removeWholeChain = true;
                                    }
                                }
                                if (removeWholeChain)
                                {
                                    lock (MasterLock)
                                    {
                                        ChainIDToPerChain.Remove(resourceChainID);
                                    }
                                }
                            };
                    }
                };


            Action wipeAssumingHaveChainLock =
                () =>
                {
                    foreach (var e in perSurvivor.Values)
                    {
                        e.PossibleObjectAndChildRefsStillOwedValue = None<ObjectPresent>();
                    }
                };

            currentGetIdentityAndGetDraw_thisMutatesAndIsClosedOver =
                resourceSourceDefinition(
                    replacement =>
                    {
                        lock (lockThisChain)
                        {
                            currentGetIdentityAndGetDraw_thisMutatesAndIsClosedOver = replacement;
                            currentIdentityAndGetDraw_thisMutatesAndIsClosedOver = currentGetIdentityAndGetDraw_thisMutatesAndIsClosedOver();
                            wipeAssumingHaveChainLock();
                        }
                    });

            // give it an initial value
            currentIdentityAndGetDraw_thisMutatesAndIsClosedOver = currentGetIdentityAndGetDraw_thisMutatesAndIsClosedOver();


            var rootNode = new DW<Node>(
                new Node(
                    new NodeCommon(
                        lockThisChain,
                        addAndGetKillAssumesLock,
                        () => // in chain lock
                            {
                            var newIdentityAndDraw = currentGetIdentityAndGetDraw_thisMutatesAndIsClosedOver();
                            if (currentIdentityAndGetDraw_thisMutatesAndIsClosedOver.Identity.Equals(newIdentityAndDraw.Identity))
                            {
                                    // no identity change - don't wipe
                                }
                            else
                            {
                                currentIdentityAndGetDraw_thisMutatesAndIsClosedOver = newIdentityAndDraw;
                                    // wipe them all
                                    wipeAssumingHaveChainLock();
                            }
                        }),
                    objectSlot,
                    () => currentIdentityAndGetDraw_thisMutatesAndIsClosedOver.Draw(),
                    isForMinor,
                    None<DAlterTotalMajorCountToParent>()),
                addAndGetKillAssumesLock(objectSlot));

            lock (MasterLock) // never take locks in reverse
            {
                ChainIDToPerChain.Add(
                    resourceChainID,
                    new PerResourceChain(
                        () =>
                        {
                            lock (lockThisChain)
                            {
                                foreach (var e in perSurvivor.Values)
                                {
                                    e.PossibleObjectAndChildRefsStillOwedValue = None<ObjectPresent>();
                                }
                            }
                        }));
            }
            return
                new NewResourceChain<DW<Node>>(
                    rootNode,
                    () =>
                    {
                        lock (lockThisChain)
                        {
                            wipeAssumingHaveChainLock();
                        }
                    });
        }


        // public delegate Func<IdentityAndDraw<T>> DResourceSourceDefinition<T>(Action<DResourceSourceDefinition<T>> sinkReplacementOnChange);

        // Ultimately need a different opener for providing R<T> vs Rm<T>
        public NewResourceChain<R<T>> SimpleRootCase<T>(OfResourceBank.DResourceSourceDefinition<T> getIdentityAndDraw) =>
            SimpleRootCase(getIdentityAndDraw, false).Let(
                r => new NewResourceChain<R<T>>(R<T>.FromContents(new Contents<T>(r.Value)), r.Wipe));

        public NewResourceChain<Rm<T>> SimpleRootCaseMinor<T>(OfResourceBank.DResourceSourceDefinition<T> getIdentityAndDraw) =>
            SimpleRootCase(getIdentityAndDraw, true).Let(
                r => new NewResourceChain<Rm<T>>(Rm<T>.FromContents(new ContentsMinor<T>(r.Value)), r.Wipe));


        private readonly object MasterLock = new string[] { "" };
        private int NextResourceChainOrdinal = 0;

        public void WipeAllValues()
        {
            lock (MasterLock)
            {
                ChainIDToPerChain.Values.ForEach(x => x.WipeAll());
            }
        }
        private readonly Dictionary<QID<ResourceChain>, PerResourceChain> ChainIDToPerChain = new Dictionary<QID<ResourceChain>, PerResourceChain>();
    }
#endif
}
