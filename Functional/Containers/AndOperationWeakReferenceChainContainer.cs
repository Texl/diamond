using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Core;
using static PlayStudios.Functional.Alg;

namespace PlayStudios.Functional.Containers
{

    
    // This defines a weak web of sequential keys, the 'and' relationship means that, along any chain from one end to the final result-bearing node,
    // If any one goes missing/is reclaimed, you'll nevern make it to the end and the item will not be there.
    public sealed class AndOperationWeakReferenceChainContainer<TMechanicalResourceKey, TInterestedHoldingParty, TResourceObject>
        where TMechanicalResourceKey : class
        where TInterestedHoldingParty : class
    {

        public sealed class ResourceHolder
        {
            internal ResourceHolder(
                TResourceObject resource,
                KeyWeakSharedSoleItemContainer<TMechanicalResourceKey, Unit> allComponentMechanicalsThisResource)
            {
                InterestedHoldingPartyToResource = new KeyWeakSharedSoleItemContainer<TInterestedHoldingParty, FSharpList<TResourceObject>>(ListModule.Singleton(resource));
                AllComponentMechanicalsThisResource = allComponentMechanicalsThisResource;
            }

            private readonly KeyWeakSharedSoleItemContainer<TInterestedHoldingParty, FSharpList<TResourceObject>> InterestedHoldingPartyToResource;

            private readonly KeyWeakSharedSoleItemContainer<TMechanicalResourceKey, Unit> AllComponentMechanicalsThisResource;


            public TMechanicalResourceKey[] GetAllMechanicalResourcesBehindThisResult() => AllComponentMechanicalsThisResource.GetLiveKeys();

            // Probably a bit goofy taking the 'add if missing' as a lambda, as the caller technically has the true value, which should match what he'd just put in here
            // Maybe to get rid of ambiguity the action should be ResourceHolder.Build... or something and just take it all private in here.
            internal void AddKeyRebuildingHeldResourceFromPassedInOnlyIfItSomehowWasMissing(TInterestedHoldingParty ask, Func<TResourceObject> getResourceObject) => InterestedHoldingPartyToResource.AddUnrepresentedKeyWithRecreateIfMissing(ask, () => ListModule.Singleton(getResourceObject()));

            internal void AddKeyIfValueAvailable(TInterestedHoldingParty ask) => InterestedHoldingPartyToResource.AddKeyIfValueAvailable(ask);

            public void RemoveKeyIfStillThere(TInterestedHoldingParty ask) => InterestedHoldingPartyToResource.RemoveKeyIfStillThere(ask);

            public Option<TResourceObject> GetContainedIfAvailable() => InterestedHoldingPartyToResource.GetContainedIfAvailable().Select(ListModule.ExactlyOne);
        }

        private sealed class WeakLinkNode
        {
            public readonly KeyWeakDictionary<TMechanicalResourceKey, Either<ResourceHolder, WeakLinkNode>> KeyToTerminateOrNext = new KeyWeakDictionary<TMechanicalResourceKey, Either<ResourceHolder, WeakLinkNode>>();
        }


        public sealed class KeyChainDownToValue
        {
            public KeyChainDownToValue(Either<Tuple<TMechanicalResourceKey, Func<KeyChainDownToValue>>, Func<Tuple<TResourceObject, TInterestedHoldingParty[]>>> entry)
            {
                Entry = entry;
            }

            public readonly Either<Tuple<TMechanicalResourceKey, Func<KeyChainDownToValue>>, Func<Tuple<TResourceObject, TInterestedHoldingParty[]>>> Entry;
        }


        private readonly Action<Action> mExclusivity = NewLock();
        private readonly WeakLinkNode MechanicalsDownToResource = new WeakLinkNode();

        // Just to fish for keys
        private readonly KeyWeakSharedSoleItemContainer<ResourceHolder, Unit> AllResourceHoldersThisCache = new KeyWeakSharedSoleItemContainer<ResourceHolder, Unit>(UnitValue);

        // private static ResourceHolder NewResourceHolder(TResourceObject r) => new ResourceHolder(r);

        public IEnumerable<Action<TInterestedHoldingParty>> GetLiveKeyListOfAsks() =>
            AllResourceHoldersThisCache.GetLiveKeys().Select(x => new Action<TInterestedHoldingParty>(x.AddKeyIfValueAvailable));

        public ResourceHolder GetOrCreateResourceHolderFromMechanicalsSearch(
            Tuple<TMechanicalResourceKey, Func<KeyChainDownToValue>> firstMechanicalAndDown)
        {
            var allComponentMechanicalsTraversedSoFar_Accumulator = new KeyWeakSharedSoleItemContainer<TMechanicalResourceKey, Unit>(UnitValue);

            bool addToAccumulatorReturningTrueIfWentInNew(TMechanicalResourceKey mechanical) => allComponentMechanicalsTraversedSoFar_Accumulator.AddKeyIfNotAlreadyPresentWithRecreateIfMissing(mechanical, () => UnitValue).AddedWeakOwningKeyAsNew;

            // ** HERE ** is the rationale for everything below. We are building out, or traversing existing, WeakReference nodes. Key with the accumulator is no node is made for a mechanical already found
            // A combination of Rs can get us, say, mechanicals in the shape A A A B B A A C B C D C B A. Old version would build a list of WeakLinkNodes for all of those.
            // New version would make this into A B C D, as each one seen would never be repeated. This reduces the number of links that persist and the number that have to
            // be walked through, though ignoring them probably doesn't save that much time I guess.
            // So to consider - traversal-to-find and traversal-to-add follow the same algorithm here

            // Remember what WeakLinkNodes are ultimately for, to kill the ResourceHolder at the end of the chain if one goes away. If this is done right, there is still one node
            // per mechanical (rather than dupes), so the GC collection of one of them still cuts the chain.

            Trampoline<InjectAndOuterReturn<Either<ResourceHolder, WeakLinkNode>>> directlyBuildOutAbsent(
                KeyChainDownToValue next,
                Option<Tuple<WeakLinkNode, Action<Either<ResourceHolder, WeakLinkNode>>>> ifAnyBeforeThisIsFirstAndSinkForThis) =>
                Trampoline.Build(
                    next.Entry.Match(
                        hereAndNext =>
                        {
                            var mechanicalHere = hereAndNext.Item1;
                            bool thisMechanicalWentInNew = addToAccumulatorReturningTrueIfWentInNew(mechanicalHere);

                            if (thisMechanicalWentInNew)
                            {
                                var newLinkNodeHere = new WeakLinkNode();

                                var sinkNewToThis = Action((Either<ResourceHolder, WeakLinkNode> e) => newLinkNodeHere.KeyToTerminateOrNext.Add(mechanicalHere, e));

                                return
                                    Either<InjectAndOuterReturn<Either<ResourceHolder, WeakLinkNode>>, Func<Trampoline<InjectAndOuterReturn<Either<ResourceHolder, WeakLinkNode>>>>>.FromRight(
                                        () =>
                                            directlyBuildOutAbsent(
                                                hereAndNext.Item2(),
                                                //Some(Tuple.Create())
                                                Some( // if there wasn't a first, there is now
                                                    Tuple.Create(
                                                        ifAnyBeforeThisIsFirstAndSinkForThis.Match(
                                                            prior =>
                                                            {
                                                                prior.Item2(Either<ResourceHolder, WeakLinkNode>.FromRight(newLinkNodeHere));
                                                                return prior.Item1; // perpetuate known 'first'
                                                            },
                                                            () => // the one we created here is the first now.
                                                                newLinkNodeHere),
                                                        sinkNewToThis))));
                            }
                            else
                            {
                                return
                                    // continue; but no new node to sink.
                                    Either<InjectAndOuterReturn<Either<ResourceHolder, WeakLinkNode>>, Func<Trampoline<InjectAndOuterReturn<Either<ResourceHolder, WeakLinkNode>>>>>.FromRight(
                                        () =>
                                            directlyBuildOutAbsent(
                                                hereAndNext.Item2(), // go to next
                                                ifAnyBeforeThisIsFirstAndSinkForThis // perpetuate any existing tree perception
                                            ));
                            }
                        },
                        getObjectAndInterestedParties => // the very end - we return a value, and sink it to whatever node precedes
                        {
                            var objectAndInterestedParties = getObjectAndInterestedParties();
                            // return injector for value
                            var theEndValue_OnlyUsedHereInImmediateSense = objectAndInterestedParties.Item1;

                            // Put it in the ResourceHolder, where weakref behavior will track it
                            var newHolder = new ResourceHolder(theEndValue_OnlyUsedHereInImmediateSense, allComponentMechanicalsTraversedSoFar_Accumulator);


                            // (I think at time of writing) to notify any new subscriber as we transform off.
                            AllResourceHoldersThisCache.AddUnrepresentedKeyWithRecreateIfMissing(newHolder, () => UnitValue);


                            // Note - the closure on the right capturing theEndValue_... is using it immediately, the closure isn't itself captured long term by ResourceHolder's inner container;
                            // But the value will go straight in if needed
                            objectAndInterestedParties.Item2.ForEach(interestedHoldingParty => newHolder.AddKeyRebuildingHeldResourceFromPassedInOnlyIfItSomehowWasMissing(interestedHoldingParty, () => theEndValue_OnlyUsedHereInImmediateSense));
                            var e = Either<ResourceHolder, WeakLinkNode>.FromLeft(newHolder);
                            return
                                // return trampoline final result
                                Either<InjectAndOuterReturn<Either<ResourceHolder, WeakLinkNode>>, Func<Trampoline<InjectAndOuterReturn<Either<ResourceHolder, WeakLinkNode>>>>>.FromLeft(
                                    ifAnyBeforeThisIsFirstAndSinkForThis.Match(
                                        veryFirstAndSinkForThis => // there was a node before this we can sink to
                                        {
                                            veryFirstAndSinkForThis.Item2(e);
                                            return
                                                new InjectAndOuterReturn<Either<ResourceHolder, WeakLinkNode>>(
                                                    toInject: Either<ResourceHolder, WeakLinkNode>.FromRight(veryFirstAndSinkForThis.Item1), // caller needs to inject this, the very first
                                                    toReturn: e);
                                        },
                                        () => // there was no prior WeakNode created - just needed this result node - to both inject, and return as value
                                            // this could be a bit weird - not sure how you could get in to directlyBuildOutAbsent and not either build a weaknode, or have one passed in
                                            new InjectAndOuterReturn<Either<ResourceHolder, WeakLinkNode>>(
                                                toInject: e, // caller needs to inject this, the result holder node only.
                                                toReturn: e)));
                        }));

            Trampoline<ResourceHolder> discovered(
                WeakLinkNode currentNode,
                Tuple<TMechanicalResourceKey, Func<KeyChainDownToValue>> currentMechanicalAndDown)
            {
                var mechanicalHere = currentMechanicalAndDown.Item1;
                bool thisMechanicalIsNewToOurTraversal = addToAccumulatorReturningTrueIfWentInNew(mechanicalHere);
                return
                    Trampoline.Build(
                        thisMechanicalIsNewToOurTraversal
                            ? currentNode.KeyToTerminateOrNext.GetAndIfAbsentBuildSplitInjectAndOuterReturnInsideTheLock(
                                mechanicalHere,
                                () =>
                                    // short-circuiting 
                                    Trampoline.RunToEnd(
                                        directlyBuildOutAbsent
                                        (
                                            currentMechanicalAndDown.Item2(),
                                            None<Tuple<WeakLinkNode, Action<Either<ResourceHolder, WeakLinkNode>>>>())) // sending 'next' in for sub's use, the inject returned will be sent to currentMechanical.Value by virtue of being in inject predicate

                            ).Map(
                                x => x, // found an existing, which already contains what's needed
                                node => // found a matching node - proceed with next stage of discovery
                                    Func(
                                        () =>
                                            currentMechanicalAndDown.Item2().Entry.Match( // dig out the next in the path
                                                t => discovered(node, t), // another mechanical - search around again
                                                end =>
                                                    // we're at a value-point, 
                                                    throw new Exception("Contradiction from source - found a full node from prior mechanical; but source claims it's a value. That is, a prior mechanical leading to another mechanical/junction now represents termination point")
                                                //discovered(node, currentMechanical.Next)
                                            )))
                            : Either<ResourceHolder, Func<Trampoline<ResourceHolder>>>.FromRight(
                                () =>
                                    Trampoline.Build(
                                        Either<ResourceHolder, Func<Trampoline<ResourceHolder>>>.FromRight(
                                            () => currentMechanicalAndDown.Item2().Entry.Match(
                                                t => discovered(currentNode, t), end =>
                                                    throw new Exception("Contradiction from source - found a full node from prior mechanical; but source claims it's a value. That is, a prior mechanical leading to another mechanical/junction now represents termination point"))))));
            }

            // Theory on Exclusivity here is sound - anyone attempting discovery could cause resource calculation, any +1
            // getting past here could end up paying twice.
            // Locking just at the root is fine, as the WeakNodes never get out of this class into others.
            return With(mExclusivity, () => Trampoline.RunToEnd(discovered(MechanicalsDownToResource, firstMechanicalAndDown)));

        }
    }
}
