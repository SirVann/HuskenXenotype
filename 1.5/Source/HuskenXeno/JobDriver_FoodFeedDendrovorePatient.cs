using System;
using System.Collections.Generic;
using Verse;
using Verse.AI;
using RimWorld;
using UnityEngine;

namespace HuskenXeno
{
    public class JobDriver_FoodFeedDendrovorePatient : JobDriver
    {
        private const TargetIndex FoodSourceInd = TargetIndex.A;

        private const TargetIndex DelivereeInd = TargetIndex.B;

        private const TargetIndex FoodHolderInd = TargetIndex.C;

        public const float nutritionPerLog = Gene_Dendrovore.nutritionPerLog;

        private const float FeedDurationMultiplier = 1.5f;

        private const float MetalhorrorInfectionChance = 0.1f; // Lower than standard food because it's wood. Not 0 because there's still a pawn involved

        protected Thing Food => job.targetA.Thing;

        protected Pawn Deliveree => job.targetB.Pawn;

        protected Pawn_InventoryTracker FoodHolderInventory => Food?.ParentHolder as Pawn_InventoryTracker;

        protected Pawn FoodHolder => job.targetC.Pawn;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (!pawn.Reserve(Deliveree, job, 1, -1, null, errorOnFailed))
                return false;

            if (pawn.inventory?.Contains(TargetThingA) != true)
            {
                int maxAmountToPickup = Mathf.Min(job.count, Food.stackCount, Mathf.FloorToInt(Deliveree.needs.food.NutritionWanted / nutritionPerLog));
                if (!pawn.Reserve(Food, job, 10, maxAmountToPickup, null, errorOnFailed))
                    return false;
                job.count = maxAmountToPickup;
            }
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.B);
            this.FailOn(() => !FoodUtility.ShouldBeFedBySomeone(Deliveree));
            Toil carryFoodFromInventory = Toils_Misc.TakeItemFromInventoryToCarrier(pawn, TargetIndex.A);
            Toil goToFoodHolder = Toils_Goto.GotoThing(TargetIndex.C, PathEndMode.Touch).FailOn(() => FoodHolder != FoodHolderInventory?.pawn || FoodHolder.IsForbidden(pawn));
            Toil carryFoodToPatient = Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.Touch);
            yield return Toils_Jump.JumpIf(carryFoodFromInventory, () => pawn.inventory != null && pawn.inventory.Contains(TargetThingA));
            yield return Toils_Haul.CheckItemCarriedByOtherPawn(Food, TargetIndex.C, goToFoodHolder);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch).FailOnForbidden(TargetIndex.A);
            yield return Toils_Ingest.PickupIngestible(TargetIndex.A, Deliveree);
            yield return Toils_Jump.Jump(carryFoodToPatient);
            yield return goToFoodHolder;
            yield return Toils_General.Wait(25).WithProgressBarToilDelay(TargetIndex.C);
            yield return Toils_Haul.TakeFromOtherInventory(Food, pawn.inventory.innerContainer, FoodHolderInventory?.innerContainer, job.count, TargetIndex.A);
            yield return carryFoodFromInventory;
            yield return Toils_Jump.Jump(carryFoodToPatient);
            yield return carryFoodToPatient;
            yield return ChewIngestible(Deliveree, 1.5f).FailOnCannotTouch(TargetIndex.B, PathEndMode.Touch);
            Toil toil = FinalizeIngest(Deliveree);
            toil.finishActions = new List<Action>
        {
            delegate
            {
                if (ModsConfig.AnomalyActive && Rand.Chance(MetalhorrorInfectionChance) && MetalhorrorUtility.IsInfected(pawn))
                    MetalhorrorUtility.Infect(Deliveree, pawn, "FeedingImplant");
            }
        };
            yield return toil;
        }

        private Toil ChewIngestible(Pawn chewer, float durationMultiplier, TargetIndex eatSurfaceInd = TargetIndex.None)
        {
            Toil toil = ToilMaker.MakeToil("ChewIngestible");
            int tickCost = 0;

            if (Food.def.ingestible != null)
                tickCost = Mathf.RoundToInt((float)Food.def.ingestible.baseIngestTicks * durationMultiplier);
            else
                tickCost = Mathf.RoundToInt(500f * durationMultiplier);

            toil.initAction = delegate
            {
                Pawn actor = toil.actor;

                if (Food.IsBurning())
                    chewer.jobs.EndCurrentJob(JobCondition.Incompletable);
                else
                {
                    toil.actor.pather.StopDead();
                    actor.jobs.curDriver.ticksLeftThisToil = tickCost;

                    if (Food.Spawned)
                        Food.Map.physicalInteractionReservationManager.Reserve(chewer, actor.CurJob, Food);
                }
            };
            toil.tickAction = delegate
            {
                if (chewer != toil.actor)
                    toil.actor.rotationTracker.FaceCell(chewer.Position);
                else
                {
                    if (Food?.Spawned == true)
                        toil.actor.rotationTracker.FaceCell(Food.Position);
                    else if (eatSurfaceInd != 0 && toil.actor.CurJob.GetTarget(eatSurfaceInd).IsValid)
                        toil.actor.rotationTracker.FaceCell(toil.actor.CurJob.GetTarget(eatSurfaceInd).Cell);
                }
                toil.actor.GainComfortFromCellIfPossible();
            };
            toil.WithProgressBar(TargetIndex.A, delegate
            {
                Thing thing2 = Food;
                return (thing2 == null) ? 1f : (1f - (float)toil.actor.jobs.curDriver.ticksLeftThisToil / Mathf.Round((float)tickCost));
            });
            toil.defaultCompleteMode = ToilCompleteMode.Delay;
            toil.FailOnDestroyedOrNull(TargetIndex.A);
            toil.AddFinishAction(delegate
            {
                Thing thing = chewer?.CurJob?.GetTarget(TargetIndex.A).Thing;
                if (thing != null && chewer.Map.physicalInteractionReservationManager.IsReservedBy(chewer, thing))
                    chewer.Map.physicalInteractionReservationManager.Release(chewer, toil.actor.CurJob, thing);
            });
            toil.handlingFacing = true;
            AddIngestionEffects(toil, chewer, eatSurfaceInd);
            return toil;
        }

        private Toil AddIngestionEffects(Toil toil, Pawn chewer, TargetIndex eatSurfaceInd)
        {
            toil.WithEffect(delegate
            {
                LocalTargetInfo target2 = job.GetTarget(TargetIndex.A);
                if (!target2.HasThing)
                    return null;

                EffecterDef result = target2.Thing.def.ingestible?.ingestEffect;
                if ((int)chewer.RaceProps.intelligence < 1 && target2.Thing.def.ingestible?.ingestEffectEat != null)
                    result = target2.Thing.def.ingestible.ingestEffectEat;

                return result;
            }, delegate
            {
                if (!toil.actor.CurJob.GetTarget(TargetIndex.A).HasThing)
                    return null;

                if (chewer != toil.actor)
                    return chewer;

                return (eatSurfaceInd != 0 && toil.actor.CurJob.GetTarget(eatSurfaceInd).IsValid) ? toil.actor.CurJob.GetTarget(eatSurfaceInd) : ((LocalTargetInfo)Food);
            });
            toil.PlaySustainerOrSound(delegate
            {
                if (!chewer.RaceProps.Humanlike)
                    return chewer.RaceProps.soundEating;

                return Food.def.ingestible?.ingestSound ?? HuskenDefOf.Herbivore_Eat;
            });
            return toil;
        }

        private Toil FinalizeIngest(Pawn ingester)
        {
            Toil toil = ToilMaker.MakeToil("FinalizeIngest");
            toil.initAction = delegate
            {
                Pawn actor = toil.actor;
                Job curJob = actor.jobs.curJob;
                if (ingester.needs.mood != null && Food.def.plant?.IsTree != true)
                {
                    if (!(ingester.Position + ingester.Rotation.FacingCell).HasEatSurface(actor.Map) && ingester.GetPosture() == PawnPosture.Standing && !ingester.IsWildMan())
                        ingester.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOf.AteWithoutTable);

                    Room room = ingester.GetRoom();
                    if (room != null)
                    {
                        int scoreStageIndex = RoomStatDefOf.Impressiveness.GetScoreStageIndex(room.GetStat(RoomStatDefOf.Impressiveness));
                        if (ThoughtDefOf.AteInImpressiveDiningRoom.stages[scoreStageIndex] != null)
                            ingester.needs.mood.thoughts.memories.TryGainMemory(ThoughtMaker.MakeThought(ThoughtDefOf.AteInImpressiveDiningRoom, scoreStageIndex));
                    }
                }

                // Getting nutrition limits
                float num = ingester.needs?.food?.NutritionWanted ?? Food.def.ingestible?.CachedNutrition ?? nutritionPerLog * (float)Food.stackCount;
                if (curJob.overeat)
                    num = Mathf.Max(num, 0.75f);

                if (!ingester.Dead && ingester.needs?.food != null)
                {
                    if (Food.def.plant?.IsTree == true)
                        num = Food.Ingested(ingester, num);
                    else
                    {
                        int num2 = Mathf.CeilToInt(num / nutritionPerLog); // Calculating stack count to remove
                        num2 = Mathf.Min(num2, Food.stackCount); // Can't exceed stack count
                        num2 = Mathf.Max(num2, 1); // And must be at least 1
                        num = num2 * nutritionPerLog;

                        if (num2 == Food.stackCount)
                        {
                            actor.carryTracker.innerContainer.Remove(Food);
                            Food.Destroy();
                        }
                        else
                            Food.stackCount -= num2;
                    }

                    ingester.needs.food.CurLevel += num;
                    ingester.records.AddTo(RecordDefOf.NutritionEaten, num);
                }
            };
            toil.defaultCompleteMode = ToilCompleteMode.Instant;
            return toil;
        }
    }
}