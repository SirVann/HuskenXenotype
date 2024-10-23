using System.Collections.Generic;
using Verse;
using Verse.AI;
using RimWorld;

namespace HuskenXeno
{
    public class JobDriver_DendrovoreDeliver : JobDriver
    {
        private bool eatingFromInventory;

        private const TargetIndex FoodSourceInd = TargetIndex.A;

        private const TargetIndex DelivereeInd = TargetIndex.B;

        private Pawn Deliveree => (Pawn)job.targetB.Thing;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref eatingFromInventory, "eatingFromInventory", false);
        }

        public override void Notify_Starting()
        {
            base.Notify_Starting();
            eatingFromInventory = pawn.inventory != null && pawn.inventory.Contains(TargetThingA);
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(Deliveree, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.B);
            if (eatingFromInventory)
            {
                yield return Toils_Misc.TakeItemFromInventoryToCarrier(pawn, TargetIndex.A);
            }
            else
            {
                yield return Toils_Reserve.Reserve(TargetIndex.A, 10, job.count);
                yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch).FailOnForbidden(TargetIndex.A);
                yield return Toils_Haul.StartCarryThing(TargetIndex.A);
            }
            Toil toil = ToilMaker.MakeToil("MakeNewToils");
            toil.initAction = delegate
            {

                Pawn actor = toil.actor;
                Job curJob = actor.jobs.curJob;
                actor.pather.StartPath(curJob.targetC, PathEndMode.OnCell);
            };
            toil.defaultCompleteMode = ToilCompleteMode.PatherArrival;
            toil.FailOnDestroyedNullOrForbidden(TargetIndex.B);
            toil.AddFailCondition(delegate
            {

                if (!pawn.IsCarryingThing(job.GetTarget(TargetIndex.A).Thing))
                    return true;
                if (!Deliveree.IsPrisonerOfColony)
                    return true;
                return !Deliveree.guest.CanBeBroughtFood;
            });
            yield return toil;
            Toil toil2 = ToilMaker.MakeToil("MakeNewToils");
            toil2.initAction = delegate
            {
                pawn.carryTracker.TryDropCarriedThing(toil2.actor.jobs.curJob.targetC.Cell, ThingPlaceMode.Direct, out var _);
            };
            toil2.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return toil2;
        }
    }
}
