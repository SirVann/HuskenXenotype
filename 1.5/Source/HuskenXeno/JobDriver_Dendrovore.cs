using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;

namespace HuskenXeno
{
    public class JobDriver_Dendrovore : JobDriver
    {
        private bool eatingFromInventory;

        private Thing IngestibleSource => job.GetTarget(TargetIndex.A).Thing;

        private const float nutritionPerLog = Gene_Dendrovore.nutritionPerLog;

        private float ChewDurationMultiplier
        {
            get
            {
                Thing ingestibleSource = IngestibleSource;
                if (ingestibleSource.def.ingestible?.useEatingSpeedStat == false) // Unlikely, but pretty sure a food could also technically be used as stuffing if coded right
                    return 1f;

                return 1f / pawn.GetStatValue(StatDefOf.EatingSpeed);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref eatingFromInventory, "eatingFromInventory", false);
        }

        public override string GetReport()
        {
            return "Husken_Consuming".Translate(job.targetA.Thing.LabelShort);
        }

        public override void Notify_Starting()
        {
            base.Notify_Starting();
            eatingFromInventory = pawn.inventory != null && pawn.inventory.Contains(IngestibleSource);
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (pawn.Faction != null)
            {
                if (IngestibleSource.def.plant?.IsTree != true)
                {
                    int count = Mathf.FloorToInt(pawn.needs.food.NutritionWanted / nutritionPerLog);
                    count = Mathf.Max(count, 1);
                    count = Mathf.Min(IngestibleSource.stackCount, count);
                    return pawn.Reserve(IngestibleSource, job, 10, count, null, errorOnFailed);
                }
                return pawn.Reserve(IngestibleSource, job, 10, 1, null, errorOnFailed);
            }
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            Toil chew = ChewIngestible(pawn, ChewDurationMultiplier, TargetIndex.B).FailOn((Toil x) => !IngestibleSource.Spawned && (pawn.carryTracker == null || pawn.carryTracker.CarriedThing != IngestibleSource)).FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch);
            foreach (Toil item in PrepareToIngestToils(chew))
                yield return item;
            yield return chew;
            yield return FinalizeIngest(pawn);
        }

        private Toil ChewIngestible(Pawn chewer, float durationMultiplier, TargetIndex eatSurfaceInd = TargetIndex.None)
        {
            Toil toil = ToilMaker.MakeToil("ChewIngestible");
            int tickCost = 0;

            if (IngestibleSource.def.ingestible != null)
                tickCost = Mathf.RoundToInt((float)IngestibleSource.def.ingestible.baseIngestTicks * durationMultiplier);
            else
                tickCost = Mathf.RoundToInt(500f * durationMultiplier);

            toil.initAction = delegate
            {
                Pawn actor = toil.actor;

                if (IngestibleSource.IsBurning())
                    chewer.jobs.EndCurrentJob(JobCondition.Incompletable);
                else
                {
                    toil.actor.pather.StopDead();
                    actor.jobs.curDriver.ticksLeftThisToil = tickCost;

                    if (IngestibleSource.Spawned)
                        IngestibleSource.Map.physicalInteractionReservationManager.Reserve(chewer, actor.CurJob, IngestibleSource);
                }
            };
            toil.tickAction = delegate
            {
                if (chewer != toil.actor)
                    toil.actor.rotationTracker.FaceCell(chewer.Position);
                else
                {
                    if (IngestibleSource?.Spawned == true)
                        toil.actor.rotationTracker.FaceCell(IngestibleSource.Position);
                    else if (eatSurfaceInd != 0 && toil.actor.CurJob.GetTarget(eatSurfaceInd).IsValid)
                        toil.actor.rotationTracker.FaceCell(toil.actor.CurJob.GetTarget(eatSurfaceInd).Cell);
                }
                toil.actor.GainComfortFromCellIfPossible();
            };
            toil.WithProgressBar(TargetIndex.A, delegate
            {
                Thing thing2 = IngestibleSource;
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

                return (eatSurfaceInd != 0 && toil.actor.CurJob.GetTarget(eatSurfaceInd).IsValid) ? toil.actor.CurJob.GetTarget(eatSurfaceInd) : ((LocalTargetInfo)IngestibleSource);
            });
            toil.PlaySustainerOrSound(delegate
            {
                if (!chewer.RaceProps.Humanlike)
                    return chewer.RaceProps.soundEating;

                return IngestibleSource.def.ingestible?.ingestSound ?? HuskenDefOf.Herbivore_Eat;
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
                if (ingester.needs.mood != null && IngestibleSource.def.plant?.IsTree != true)
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
                float num = ingester.needs?.food?.NutritionWanted ?? IngestibleSource.def.ingestible?.CachedNutrition ?? nutritionPerLog * (float)IngestibleSource.stackCount;
                if (curJob.overeat)
                    num = Mathf.Max(num, 0.75f);

                if (!ingester.Dead && ingester.needs?.food != null)
                {
                    if (IngestibleSource.def.plant?.IsTree == true)
                        num = IngestibleSource.Ingested(ingester, num);
                    else
                    {
                        int num2 = Mathf.CeilToInt(num / nutritionPerLog); // Calculating stack count to remove
                        num2 = Mathf.Min(num2, IngestibleSource.stackCount); // Can't exceed stack count
                        num2 = Mathf.Max(num2, 1); // And must be at least 1
                        num = num2 * nutritionPerLog;

                        if (num2 == IngestibleSource.stackCount)
                        {
                            actor.carryTracker.innerContainer.Remove(IngestibleSource);
                            IngestibleSource.Destroy();
                        }
                        else
                            IngestibleSource.stackCount -= num2;
                    }

                    ingester.needs.food.CurLevel += num;
                    ingester.records.AddTo(RecordDefOf.NutritionEaten, num);
                }
            };
            toil.defaultCompleteMode = ToilCompleteMode.Instant;
            return toil;
        }

        private IEnumerable<Toil> PrepareToIngestToils(Toil chewToil)
        {
            if (pawn.RaceProps.ToolUser && !pawn.IsMutant && IngestibleSource.def.plant?.IsTree != true)
                return PrepareToIngestToils_ToolUser(chewToil);

            return PrepareToIngestToils_NonToolUser();
        }

        private IEnumerable<Toil> PrepareToIngestToils_ToolUser(Toil chewToil)
        {
            if (eatingFromInventory)
            {
                yield return Toils_Misc.TakeItemFromInventoryToCarrier(pawn, TargetIndex.A);
            }
            else
            {
                yield return ReserveFood();
                Toil gotoToPickup = Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch).FailOnDespawnedNullOrForbidden(TargetIndex.A);
                yield return Toils_Jump.JumpIf(gotoToPickup, () => pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation));
                yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch).FailOnDespawnedNullOrForbidden(TargetIndex.A);
                yield return Toils_Jump.Jump(chewToil);
                yield return gotoToPickup;
                yield return Toils_Ingest.PickupIngestible(TargetIndex.A, pawn);
            }
            if (!pawn.Drafted)
                yield return CarryIngestibleToChewSpot().FailOnDestroyedOrNull(TargetIndex.A);

            yield return Toils_Ingest.FindAdjacentEatSurface(TargetIndex.B, TargetIndex.A);
        }

        private Toil CarryIngestibleToChewSpot()
        {
            Toil toil = ToilMaker.MakeToil("CarryIngestibleToChewSpot");
            toil.initAction = delegate
            {
                Pawn actor = toil.actor;
                IntVec3 cell = IntVec3.Invalid;
                Thing thing = GenClosest.ClosestThingReachable(actor.Position, actor.Map, ThingRequest.ForGroup(ThingRequestGroup.BuildingArtificial), PathEndMode.OnCell, TraverseParms.For(actor), 25f, (Thing t) => BaseChairValidator(t) && t.Position.GetDangerFor(pawn, t.Map) == Danger.None);

                if (thing == null)
                {
                    cell = RCellFinder.SpotToChewStandingNear(actor, actor.CurJob.GetTarget(TargetIndex.A).Thing, (IntVec3 c) => actor.CanReserveSittableOrSpot(c));
                    Danger chewSpotDanger = cell.GetDangerFor(pawn, actor.Map);
                    if (chewSpotDanger != Danger.None)
                        thing = GenClosest.ClosestThingReachable(actor.Position, actor.Map, ThingRequest.ForGroup(ThingRequestGroup.BuildingArtificial), PathEndMode.OnCell, TraverseParms.For(actor), 25f, (Thing t) => BaseChairValidator(t) && (int)t.Position.GetDangerFor(pawn, t.Map) <= (int)chewSpotDanger);
                }

                if (thing != null && !Toils_Ingest.TryFindFreeSittingSpotOnThing(thing, actor, out cell))
                    Log.Error("Could not find sitting spot on chewing chair!");

                actor.ReserveSittableOrSpot(cell, actor.CurJob);
                actor.Map.pawnDestinationReservationManager.Reserve(actor, actor.CurJob, cell);
                actor.pather.StartPath(cell, PathEndMode.OnCell);

                bool BaseChairValidator(Thing t)
                {
                    if (t.def.building == null || !t.def.building.isSittable)
                        return false;

                    if (!Toils_Ingest.TryFindFreeSittingSpotOnThing(t, actor, out var cell2))
                        return false;

                    if (t.IsForbidden(pawn))
                        return false;

                    if (actor.IsColonist && t.Position.Fogged(t.Map))
                        return false;

                    if (!actor.CanReserve(t))
                        return false;

                    if (!t.IsSociallyProper(actor))
                        return false;

                    if (t.IsBurning())
                        return false;

                    if (t.HostileTo(pawn))
                        return false;

                    bool flag = false;
                    for (int i = 0; i < 4; i++)
                    {
                        Building edifice = (cell2 + GenAdj.CardinalDirections[i]).GetEdifice(t.Map);
                        if (edifice != null && edifice.def.surfaceType == SurfaceType.Eat)
                        {
                            flag = true;
                            break;
                        }
                    }
                    if (!flag)
                        return false;
                    return true;
                }
            };
            toil.defaultCompleteMode = ToilCompleteMode.PatherArrival;
            return toil;
        }

        private IEnumerable<Toil> PrepareToIngestToils_NonToolUser()
        {
            yield return ReserveFood();
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
        }

        private Toil ReserveFood()
        {
            Toil toil = ToilMaker.MakeToil("ReserveFood");
            toil.initAction = delegate
            {
                if (pawn.Faction != null)
                {
                    Thing thing = job.GetTarget(TargetIndex.A).Thing;
                    if (thing.def.plant?.IsTree == true)
                    {
                        if (!pawn.Reserve(thing, job, 10, 1))
                        {
                            Log.Error(string.Concat("Pawn food reservation for ", pawn, " on job ", this, " failed, because trees are hard to reserve."));
                            pawn.jobs.EndCurrentJob(JobCondition.Errored);
                        }
                        job.count = 1;
                    }
                    else if (pawn.carryTracker.CarriedThing != thing)
                    {
                        int maxAmountToPickup = FoodUtility.GetMaxAmountToPickup(thing, pawn, job.count); // 0.05 per log
                        if (maxAmountToPickup != 0)
                        {
                            if (!pawn.Reserve(thing, job, 10, maxAmountToPickup))
                            {
                                Log.Error(string.Concat("Pawn food reservation for ", pawn, " on job ", this, " failed, because it could not register food from ", thing, " - amount: ", maxAmountToPickup));
                                pawn.jobs.EndCurrentJob(JobCondition.Errored);
                            }
                            job.count = maxAmountToPickup;
                        }
                    }
                }
            };
            toil.defaultCompleteMode = ToilCompleteMode.Instant;
            toil.atomicWithPrevious = true;
            return toil;
        }

        public override bool ModifyCarriedThingDrawPos(ref Vector3 drawPos, ref bool flip)
        {
            IntVec3 cell = job.GetTarget(TargetIndex.B).Cell;
            return ModifyCarriedThingDrawPosWorker(ref drawPos, ref flip, cell, pawn);
        }

        public static bool ModifyCarriedThingDrawPosWorker(ref Vector3 drawPos, ref bool flip, IntVec3 placeCell, Pawn pawn)
        {
            if (pawn.pather.Moving)
                return false;

            Thing carriedThing = pawn.carryTracker.CarriedThing;
            if (carriedThing == null || !carriedThing.IsBurning())
                return false;

            if (placeCell.IsValid && placeCell.AdjacentToCardinal(pawn.Position) && placeCell.HasEatSurface(pawn.Map) && carriedThing.def.ingestible?.ingestHoldUsesTable == true)
            {
                drawPos = new Vector3((float)placeCell.x + 0.5f, drawPos.y, (float)placeCell.z + 0.5f);
                return true;
            }
            HoldOffset holdOffset = carriedThing.def.ingestible?.ingestHoldOffsetStanding?.Pick(pawn.Rotation);
            if (holdOffset != null)
            {
                drawPos += holdOffset.offset;
                flip = holdOffset.flip;
                return true;
            }
            return false;
        }
    }
}
