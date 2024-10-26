using System;
using System.Collections.Generic;
using UnityEngine;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;

namespace HuskenXeno
{
    [StaticConstructorOnStartup]
    public static class HarmonyPatches
    {
        private static readonly Type patchType = typeof(HarmonyPatches);

        public static List<GeneDef> equipRestrictingGenes = new List<GeneDef>();

        public static List<GeneDef> desiccatedHeadGenes = new List<GeneDef>();

        public static List<GeneDef> desiccatedBodyGenes = new List<GeneDef>();

        public static List<GeneDef> specializedProtection = new List<GeneDef>();

        public static List<ThingDef> cachedWoodyFood = new List<ThingDef>();

        public const float nutritionPerLog = Gene_Dendrovore.nutritionPerLog;

        static HarmonyPatches()
        {
            Harmony harmony = new Harmony("Rimworld.Alite.Husken");

            foreach (GeneDef gene in DefDatabase<GeneDef>.AllDefsListForReading)
            {
                if (gene.HasModExtension<EquipRestrictLiteExtension>())
                    equipRestrictingGenes.Add(gene);
                if (gene.HasModExtension<DesiccatedOverrideExtension>())
                {
                    DesiccatedOverrideExtension extension = gene.GetModExtension<DesiccatedOverrideExtension>();
                    if (extension.head != null || extension.childHead != null)
                        desiccatedHeadGenes.Add(gene);
                    if (extension.male != null || extension.female != null || extension.child != null || extension.fat != null || extension.thin != null || extension.hulk != null)
                        desiccatedBodyGenes.Add(gene);
                }
                if (gene.HasModExtension<LocalizedArmorExtension>())
                    specializedProtection.Add(gene);
            }

            foreach (ThingDef thing in DefDatabase<ThingDef>.AllDefsListForReading)
                if (thing.stuffProps?.categories?.Contains(StuffCategoryDefOf.Woody) == true)
                    cachedWoodyFood.Add(thing);

            // Removes this because it's always the first check due to how common it is
            if (cachedWoodyFood.Contains(ThingDefOf.WoodLog))
                cachedWoodyFood.Remove(ThingDefOf.WoodLog);

            if (!cachedWoodyFood.NullOrEmpty()) // If this isn't empty at this point, there's probably mods involved, and less valuable logs should move to the top
                cachedWoodyFood.SortBy((arg) => arg.BaseMarketValue);

            harmony.Patch(AccessTools.Method(typeof(EquipmentUtility), nameof(EquipmentUtility.CanEquip), new[] { typeof(Thing), typeof(Pawn), typeof(string).MakeByRefType(), typeof(bool) }),
                postfix: new HarmonyMethod(patchType, nameof(CanEquipPostfix)));
            harmony.Patch(AccessTools.Method(typeof(PawnGenerator), "TryGenerateNewPawnInternal"),
                postfix: new HarmonyMethod(patchType, nameof(TryGenerateNewPawnInternalPostfix)));
            harmony.Patch(AccessTools.Method(typeof(FoodUtility), "WillEat", new[] { typeof(Pawn), typeof(ThingDef), typeof(Pawn), typeof(bool), typeof(bool) }),
                postfix: new HarmonyMethod(patchType, nameof(WillEatPostfix)));
            harmony.Patch(AccessTools.Method(typeof(FoodUtility), "GetMaxAmountToPickup"),
                postfix: new HarmonyMethod(patchType, nameof(GetMaxAmountToPickupPostfix)));
            harmony.Patch(AccessTools.Method(typeof(FloatMenuMakerMap), "AddHumanlikeOrders"),
                postfix: new HarmonyMethod(patchType, nameof(AddHumanlikeOrdersPostfix)));
            harmony.Patch(AccessTools.Method(typeof(JobGiver_GetFood), "TryGiveJob"),
                postfix: new HarmonyMethod(patchType, nameof(GetFoodJobPostfix)));
            harmony.Patch(AccessTools.Method(typeof(Caravan_NeedsTracker), "TrySatisfyFoodNeed"),
                prefix: new HarmonyMethod(patchType, nameof(CaravanTrySatisfyFoodPrefix)));
            harmony.Patch(AccessTools.Method(typeof(DaysWorthOfFoodCalculator), "ApproxDaysWorthOfFood", new[] { typeof(Caravan) }),
                postfix: new HarmonyMethod(patchType, nameof(ApproxDaysWorthOfFoodPostfix)));
            harmony.Patch(AccessTools.Method(typeof(Caravan_NeedsTracker), "AnyPawnOutOfFood"),
                postfix: new HarmonyMethod(patchType, nameof(AnyPawnOutOfFoodPostfix)));
            harmony.Patch(AccessTools.Method(typeof(WorkGiver_Warden_DeliverFood), "JobOnThing"),
                postfix: new HarmonyMethod(patchType, nameof(WardenFoodJobOnThingPostfix)));
            harmony.Patch(AccessTools.Method(typeof(WorkGiver_FeedPatient), "HasJobOnThing"),
                postfix: new HarmonyMethod(patchType, nameof(PatientHasJobOnThingPostfix)));
            harmony.Patch(AccessTools.Method(typeof(WorkGiver_FeedPatient), "JobOnThing"),
                postfix: new HarmonyMethod(patchType, nameof(PatientJobOnThingPostfix)));
            harmony.Patch(AccessTools.Method(typeof(WorkGiver_FeedPatient), "JobInfo"),
                prefix: new HarmonyMethod(patchType, nameof(PatientJobInfoPrefix)));
            harmony.Patch(AccessTools.Method(typeof(PawnRenderNode_Head), "GraphicFor"),
                postfix: new HarmonyMethod(patchType, nameof(GraphicForHeadPostfix)));
            harmony.Patch(AccessTools.Method(typeof(PawnRenderNode_Body), "GraphicFor"),
                postfix: new HarmonyMethod(patchType, nameof(GraphicForBodyPostfix)));
            harmony.Patch(AccessTools.Method(typeof(DamageWorker_AddInjury), "GetExactPartFromDamageInfo"),
                postfix: new HarmonyMethod(patchType, nameof(GetExactPartFromDamageInfoPostfix)));
            harmony.Patch(AccessTools.Method(typeof(MentalState_SocialFighting), "PostEnd"),
                postfix: new HarmonyMethod(patchType, nameof(SocialFightEndPostfix)));
            harmony.Patch(AccessTools.Method(typeof(PawnGenerator), "GeneratePawnRelations"),
                prefix: new HarmonyMethod(patchType, nameof(GeneratePawnRelationsPrefix)));
        }

        public static bool GeneratePawnRelationsPrefix(Pawn pawn)
        {
            if (pawn.genes?.GetFirstGeneOfType<Gene_AlwaysMale>() != null)
                return false;
            return true;
        }

        public static void SocialFightEndPostfix(Pawn ___pawn, Pawn ___otherPawn)
        {
            if (___pawn.genes?.GetFirstGeneOfType<Gene_SocialFightLover>() != null)
            {
                int fightCount = ___pawn.needs.mood.thoughts.memories.NumMemoriesOfDef(ThoughtDefOf.HadAngeringFight);
                if (fightCount > 1)
                {
                    List<Thought_Memory> memories = new List<Thought_Memory>(___pawn.needs.mood.thoughts.memories.Memories);
                    memories.Reverse();
                    foreach (Thought_Memory memory in memories)
                        if (memory.def == ThoughtDefOf.HadAngeringFight && memory.otherPawn == ___otherPawn)
                        {
                            ___pawn.needs.mood.thoughts.memories.RemoveMemory(memory);
                            ___pawn.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOf.HadCatharticFight, ___otherPawn);
                            // Removes harmed me because this pawn loves the fight
                            ___pawn.needs.mood.thoughts.memories.RemoveMemoriesOfDefWhereOtherPawnIs(ThoughtDefOf.HarmedMe, ___otherPawn);
                            break;
                        }
                }
                else if (fightCount == 1)
                {
                    Thought_Memory memory = ___pawn.needs.mood.thoughts.memories.GetFirstMemoryOfDef(ThoughtDefOf.HadAngeringFight);
                    if (memory != null && memory.otherPawn == ___otherPawn)
                    {
                        ___pawn.needs.mood.thoughts.memories.RemoveMemory(memory);
                        ___pawn.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOf.HadCatharticFight, ___otherPawn);
                        // Removes harmed me because this pawn loves the fight
                        ___pawn.needs.mood.thoughts.memories.RemoveMemoriesOfDefWhereOtherPawnIs(ThoughtDefOf.HarmedMe, ___otherPawn);
                    }
                }
                ___pawn.needs.mood.thoughts.memories.TryGainMemory(HuskenDefOf.Husken_SocialFightLover);
            }
        }

        public static void GetExactPartFromDamageInfoPostfix(BodyPartRecord __result, ref DamageInfo dinfo, Pawn pawn)
        {
            if (__result != null && pawn.genes != null && !specializedProtection.NullOrEmpty())
            {
                foreach (GeneDef gene in specializedProtection)
                {
                    if (dinfo.Amount == 0) break;
                    if (!pawn.genes.HasActiveGene(gene)) continue;
                    LocalizedArmorExtension extension = gene.GetModExtension<LocalizedArmorExtension>();
                    if (extension.bodyPartGroups.NullOrEmpty()) continue;
                    bool flag = true; // Checks to make sure the part is covered
                    foreach (BodyPartGroupDef group in extension.bodyPartGroups)
                        if (__result.groups.Contains(group))
                        {
                            flag = false;
                            break;
                        }
                    if (flag) continue;

                    dinfo.SetAmount(dinfo.Amount * extension.damageFactor);
                }
            }
        }

        public static void GraphicForHeadPostfix(Pawn pawn, ref Graphic __result)
        {
            Shader shader = ShaderUtility.GetSkinShader(pawn);
            if (shader == null) return;
            if (pawn.Drawer.renderer.CurRotDrawMode == RotDrawMode.Dessicated && pawn.genes != null && !desiccatedHeadGenes.NullOrEmpty())
                foreach (GeneDef gene in desiccatedHeadGenes)
                {
                    if (!pawn.genes.HasActiveGene(gene)) continue;
                    DesiccatedOverrideExtension extension = gene.GetModExtension<DesiccatedOverrideExtension>();

                    if ((pawn.DevelopmentalStage == DevelopmentalStage.Baby || pawn.DevelopmentalStage == DevelopmentalStage.Child) && extension.childHead != null)
                    {
                        __result = (Graphic_Multi)GraphicDatabase.Get<Graphic_Multi>(extension.childHead, shader, Vector2.one, Color.white);
                        return;
                    }
                    if (extension.head != null)
                    {
                        __result = (Graphic_Multi)GraphicDatabase.Get<Graphic_Multi>(extension.head, shader, Vector2.one, Color.white);
                        return;
                    }
                }
        }

        public static void GraphicForBodyPostfix(Pawn pawn, ref Graphic __result)
        {
            Shader shader = ShaderUtility.GetSkinShader(pawn);
            if (shader == null) return;
            if (pawn.Drawer.renderer.CurRotDrawMode == RotDrawMode.Dessicated && pawn.genes != null && !desiccatedBodyGenes.NullOrEmpty())
                foreach (GeneDef gene in desiccatedBodyGenes)
                {
                    if (!pawn.genes.HasActiveGene(gene)) continue;
                    DesiccatedOverrideExtension extension = gene.GetModExtension<DesiccatedOverrideExtension>();

                    if ((pawn.DevelopmentalStage == DevelopmentalStage.Baby || pawn.DevelopmentalStage == DevelopmentalStage.Child) && extension.child != null)
                    {
                        __result = (Graphic_Multi)GraphicDatabase.Get<Graphic_Multi>(extension.child, shader, Vector2.one, Color.white);
                        return;
                    }
                    if (pawn.story?.bodyType == BodyTypeDefOf.Male && extension.male != null)
                    {
                        __result = (Graphic_Multi)GraphicDatabase.Get<Graphic_Multi>(extension.male, shader, Vector2.one, Color.white);
                        return;
                    }
                    if (pawn.story?.bodyType == BodyTypeDefOf.Female && extension.female != null)
                    {
                        __result = (Graphic_Multi)GraphicDatabase.Get<Graphic_Multi>(extension.female, shader, Vector2.one, Color.white);
                        return;
                    }
                    if (pawn.story?.bodyType == BodyTypeDefOf.Fat && extension.fat != null)
                    {
                        __result = (Graphic_Multi)GraphicDatabase.Get<Graphic_Multi>(extension.fat, shader, Vector2.one, Color.white);
                        return;
                    }
                    if (pawn.story?.bodyType == BodyTypeDefOf.Hulk && extension.hulk != null)
                    {
                        __result = (Graphic_Multi)GraphicDatabase.Get<Graphic_Multi>(extension.hulk, shader, Vector2.one, Color.white);
                        return;
                    }
                    if (pawn.story?.bodyType == BodyTypeDefOf.Thin && extension.thin != null)
                    {
                        __result = (Graphic_Multi)GraphicDatabase.Get<Graphic_Multi>(extension.thin, shader, Vector2.one, Color.white);
                        return;
                    }
                }
        }

        public static void CanEquipPostfix(ref bool __result, Thing thing, Pawn pawn, ref string cantReason)
        {
            if (!__result || equipRestrictingGenes.NullOrEmpty() || pawn.genes?.GenesListForReading.NullOrEmpty() != false || thing.def.apparel?.layers.NullOrEmpty() != false) return;

            foreach (GeneDef gene in equipRestrictingGenes)
            {
                if (!pawn.genes.HasActiveGene(gene)) continue;
                EquipRestrictLiteExtension extension = gene.GetModExtension<EquipRestrictLiteExtension>();
                if (!extension.layerEquipExceptions.NullOrEmpty() && extension.layerEquipExceptions.Contains(thing.def)) continue;
                foreach (ApparelLayerDef layer in thing.def.apparel.layers)
                    if (extension.restrictedLayers.Contains(layer))
                    {
                        cantReason = "Husken_RestrictedLayer".Translate(gene.LabelCap, layer.LabelCap);
                        __result = false;
                        break;
                    }
                if (!__result) break;
            }
        }

        public static void TryGenerateNewPawnInternalPostfix(ref Pawn __result)
        {
            if (__result != null && !equipRestrictingGenes.NullOrEmpty() && __result.genes?.GenesListForReading.NullOrEmpty() == false && __result.apparel?.WornApparel.NullOrEmpty() == false)
            {
                List<ApparelLayerDef> forbiddenLayers = new List<ApparelLayerDef>();
                List<ThingDef> exceptions = new List<ThingDef>();
                foreach (GeneDef gene in equipRestrictingGenes)
                {
                    if (!__result.genes.HasActiveGene(gene)) continue;
                    EquipRestrictLiteExtension extension = gene.GetModExtension<EquipRestrictLiteExtension>();
                    if (!extension.restrictedLayers.NullOrEmpty())
                        forbiddenLayers.AddRange(extension.restrictedLayers);
                    if (!extension.layerEquipExceptions.NullOrEmpty())
                        exceptions.AddRange(extension.layerEquipExceptions);
                }

                if (!forbiddenLayers.NullOrEmpty())
                {
                    List<Apparel> apparels = new List<Apparel>(__result.apparel.WornApparel);
                    foreach (Apparel apparel in apparels)
                    {
                        if (!exceptions.NullOrEmpty() && exceptions.Contains(apparel.def)) continue;
                        foreach (ApparelLayerDef layer in forbiddenLayers)
                            if (apparel.def.apparel.layers.Contains(layer))
                            {
                                __result.apparel.Remove(apparel);
                                break;
                            }
                    }
                }
            }
        }

        public static void WillEatPostfix(ref bool __result, Pawn p, ThingDef food, Pawn getter, bool careIfNotAcceptableForTitle, bool allowVenerated)
        {
            if (__result && p.genes?.GetFirstGeneOfType<Gene_Dendrovore>() != null && food.stuffProps?.categories?.Contains(StuffCategoryDefOf.Woody) != true)
                __result = false;
        }

        public static void GetMaxAmountToPickupPostfix(ref int __result, Thing food, Pawn pawn, int wantedCount)
        {
            if (__result != 0 && pawn.genes?.GetFirstGeneOfType<Gene_Dendrovore>() != null && food.def.stuffProps?.categories?.Contains(StuffCategoryDefOf.Woody) != true)
                __result = 0;
        }

        public static void AddHumanlikeOrdersPostfix(Vector3 clickPos, Pawn pawn, ref List<FloatMenuOption> opts)
        {
            if (pawn.genes?.GetFirstGeneOfType<Gene_Dendrovore>() != null)
            {
                IntVec3 clickCell = IntVec3.FromVector3(clickPos);
                if (clickCell.GetThingList(pawn.Map).NullOrEmpty()) return;

                foreach (Thing thing in clickCell.GetThingList(pawn.Map))
                {
                    if (thing.def.stuffProps?.categories?.Contains(StuffCategoryDefOf.Woody) == true || thing.def.plant?.IsTree == true)
                    {
                        if (thing.IsForbidden(pawn))
                            opts.Add(new FloatMenuOption("Husken_CannotConsumeForbidden".Translate(thing.LabelShort).CapitalizeFirst(), null));
                        else if (thing.def.plant?.IsTree == true)
                            opts.Add(new FloatMenuOption("Husken_ConsumeWood".Translate(thing.LabelShort), delegate
                            {
                                Job job = JobMaker.MakeJob(HuskenDefOf.Husken_Dendrovore, thing);
                                job.count = 1;
                                pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                            }));
                        else
                            opts.Add(new FloatMenuOption("Husken_ConsumeWood".Translate(thing.LabelShort), delegate
                            {
                                Job job = JobMaker.MakeJob(HuskenDefOf.Husken_Dendrovore, thing);
                                // Gets the lower of stack count and nutrition wanted results, and if it's 0 due to the nutrition, sets it to 1
                                job.count = Mathf.Max(Mathf.Min(thing.stackCount, Mathf.FloorToInt(pawn.needs.food.NutritionWanted / nutritionPerLog)), 1);
                                pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                            }));
                    }
                }
            }
        }

        public static void WardenFoodJobOnThingPostfix(ref Job __result, Pawn pawn, Thing t, bool forced = false)
        {
            // Makes sure the target is a pawn that needs food and is a dendrovore
            if (__result == null && t is Pawn pawn2 && pawn2.IsPrisonerOfColony && pawn2.genes?.GetFirstGeneOfType<Gene_Dendrovore>() != null && pawn2.needs?.food != null &&
                pawn2.guest.CanBeBroughtFood && pawn2.needs.food.CurLevelPercentage < pawn2.needs.food.PercentageThreshHungry + 0.1f &&
                pawn2.Position.IsInPrisonCell(pawn2.Map) && pawn2.carryTracker?.CarriedThing?.def.stuffProps?.categories?.Contains(StuffCategoryDefOf.Woody) != true)
            {
                // If there's already wood in the room, no need to do anything
                Room room = pawn2.GetRoom();
                if (room.ContainsThing(ThingDefOf.WoodLog)) return;
                if (!cachedWoodyFood.NullOrEmpty())
                    foreach (ThingDef def in cachedWoodyFood)
                        if (room.ContainsThing(def)) return;

                if (GetWoodNearPawn(pawn, out Thing thing))
                {
                    Job job = JobMaker.MakeJob(HuskenDefOf.Husken_DendrovoreDeliver, thing, pawn2);
                    job.count = Mathf.Max(Mathf.Min(thing.stackCount, Mathf.FloorToInt(pawn2.needs.food.NutritionWanted / nutritionPerLog)), 1);
                    job.targetC = RCellFinder.SpotToChewStandingNear(pawn2, thing);
                    __result = job;
                }
            }
        }

        private static bool GetWoodNearPawn(Pawn pawn, out Thing wood)
        {
            Predicate<Thing> Permitted = delegate (Thing l)
            {
                if (l.IsForbidden(pawn))
                    return false;
                return true;
            };

            wood = GenClosest.ClosestThingReachable(pawn.Position, pawn.Map, ThingRequest.ForDef(ThingDefOf.WoodLog), PathEndMode.ClosestTouch, TraverseParms.For(pawn), 9999f, Permitted);
            if (wood == null && !cachedWoodyFood.NullOrEmpty())
                foreach (ThingDef def in cachedWoodyFood)
                {
                    wood = GenClosest.ClosestThingReachable(pawn.Position, pawn.Map, ThingRequest.ForDef(def), PathEndMode.ClosestTouch, TraverseParms.For(pawn), 9999f, Permitted);
                    if (wood != null) break;
                }
            return wood != null;
        }

        public static void PatientHasJobOnThingPostfix(ref bool __result, WorkGiverDef ___def, Pawn pawn, Thing t, bool forced = false)
        {
            if (!__result && t is Pawn pawn2 && pawn2 != pawn && !___def.feedAnimalsOnly && pawn2.genes?.GetFirstGeneOfType<Gene_Dendrovore>() != null &&
                FeedPatientUtility.IsHungry(pawn2) && FeedPatientUtility.ShouldBeFed(pawn2) && pawn.CanReserve(t, 1, -1, null, forced) && GetWoodNearPawn(pawn, out var wood))
            {
                __result = true;
            }
        }

        public static void PatientJobOnThingPostfix(ref Job __result, Pawn pawn, Thing t)
        {
            if (__result == null && t is Pawn pawn2 && GetWoodNearPawn(pawn, out var wood))
            {
                Job job = JobMaker.MakeJob(HuskenDefOf.Husken_DendrovoreFeed);
                job.targetA = wood;
                job.targetB = pawn2;
                job.count = Mathf.Max(Mathf.Min(wood.stackCount, Mathf.FloorToInt(pawn2.needs.food.NutritionWanted / nutritionPerLog)), 1);
                __result = job;
            }
        }

        public static bool PatientJobInfoPrefix(ref string __result, Pawn pawn, Job job)
        {
            if ((job.targetB.Thing as Pawn)?.genes?.GetFirstGeneOfType<Gene_Dendrovore>() != null)
            {
                __result = string.Empty;
                return false;
            }
            return true;
        }

        public static void GetFoodJobPostfix(ref Job __result, Pawn pawn)
        {
            if (__result == null && pawn.genes?.GetFirstGeneOfType<Gene_Dendrovore>() != null && pawn.needs?.food != null)
            {
                if (GetWoodNearPawn(pawn, out var thing))
                {
                    // Gets the lower of stack count and nutrition wanted results, and if it's 0 due to the nutrition, sets it to 1
                    int count = Mathf.Max(Mathf.Min(thing.stackCount, Mathf.FloorToInt(pawn.needs.food.NutritionWanted / nutritionPerLog)), 1);
                    Pawn holder = (thing.ParentHolder as Pawn_InventoryTracker)?.pawn;
                    if (holder != null)
                    {
                        Job take = JobMaker.MakeJob(JobDefOf.TakeFromOtherInventory, thing, holder);
                        take.count = count;
                        __result = take;
                        return;
                    }

                    Job job = JobMaker.MakeJob(HuskenDefOf.Husken_Dendrovore, thing);
                    job.count = count;
                    __result = job;
                    return;
                }

                ThingRequest treeRequest = ThingRequest.ForGroup(ThingRequestGroup.NonStumpPlant);
                Predicate<Thing> Woody = delegate (Thing t)
                {
                    if (t.IsForbidden(pawn))
                        return false;
                    if (t.def.plant?.IsTree == true)
                        return true;
                    return false;
                };
                Thing tree = GenClosest.ClosestThingReachable(pawn.Position, pawn.Map, treeRequest, PathEndMode.ClosestTouch, TraverseParms.For(pawn), 9999f, Woody);

                if (tree != null)
                {
                    Job job = JobMaker.MakeJob(HuskenDefOf.Husken_Dendrovore, tree);
                    job.count = 1;
                    __result = job;
                    return;
                }
            }
        }

        public static void AnyPawnOutOfFoodPostfix(ref bool __result, Caravan ___caravan)
        {
            if (__result)
            {
                bool flag = false; // Is there a dendrovore?
                bool flag2 = false; // Is there a non-dendrovore?
                foreach (Pawn pawn in ___caravan.PawnsListForReading)
                {
                    if (pawn.genes?.GetFirstGeneOfType<Gene_Dendrovore>() != null)
                        flag = true;
                    else flag2 |= (pawn.genes != null && pawn.needs.food != null && pawn.genes.GetFirstGeneOfType<Gene_Dendrovore>() == null);
                    if (flag && flag2) break; // If we've found both a dendrovore and a non-dendrovore, then we don't need to look anymore
                }
                if (!flag) return; // If there isn't a dendrovore, then what are we even doing here

                // Checks if there's any nutrition item available when there's a non-dendrovore around. If there isn't, then blindly assume that's the reason for the alert
                if (flag2)
                {
                    bool food = false;
                    List<Thing> list = CaravanInventoryUtility.AllInventoryItems(___caravan);
                    for (int i = 0; i < list.Count; i++)
                        if (list[i].def.IsNutritionGivingIngestible)
                        {
                            food = true;
                            break;
                        }
                    if (!food) return;
                }

                // At this point, we assume that all non-dendrovores aren't causing issues, so we check if the dendrovore should be
                int curTile = ___caravan.Tile;
                if (VirtualPlantsUtility.EnvironmentAllowsEatingVirtualPlantsNowAt(curTile) && Find.WorldGrid[curTile].biome.TreeDensity > 0)
                    __result = false;
                else __result &= !GetWoodFromCaravan(___caravan);
            }
        }

        public static bool CaravanTrySatisfyFoodPrefix(Pawn pawn, Need_Food food, ref Caravan ___caravan)
        {
            if (pawn.genes?.GetFirstGeneOfType<Gene_Dendrovore>() != null)
            {
                if (food.CurLevelPercentage < 0.5f)
                {
                    int curTile = ___caravan.Tile;
                    if (VirtualPlantsUtility.EnvironmentAllowsEatingVirtualPlantsNowAt(curTile) && Find.WorldGrid[curTile].biome.TreeDensity > 0)
                        food.CurLevel += 0.5f;
                    else
                    {
                        if (!CaravanInventoryUtility.TryGetThingOfDef(___caravan, ThingDefOf.WoodLog, out Thing thing, out Pawn owner))
                            if (!cachedWoodyFood.NullOrEmpty())
                                foreach (ThingDef def in cachedWoodyFood)
                                    if (CaravanInventoryUtility.TryGetThingOfDef(___caravan, def, out thing, out owner))
                                        break;

                        if (thing != null)
                            if (thing.stackCount * nutritionPerLog > food.NutritionWanted)
                            {
                                int amountToTake = Mathf.Max(Mathf.FloorToInt(food.NutritionWanted / nutritionPerLog), 1);
                                thing.stackCount -= amountToTake;
                                food.CurLevel += amountToTake * nutritionPerLog;
                            }
                            else
                            {
                                food.CurLevel += thing.stackCount * nutritionPerLog;
                                thing.Destroy();
                                if (owner != null)
                                {
                                    owner.inventory.innerContainer.Remove(thing);
                                    ___caravan.RecacheImmobilizedNow();
                                }
                                if (!___caravan.notifiedOutOfFood && !GetWoodFromCaravan(___caravan))
                                {
                                    Messages.Message("Husken_MessageCaravanRanOutOfDendrovoreFood".Translate(___caravan.LabelCap), ___caravan, MessageTypeDefOf.ThreatBig);
                                    ___caravan.notifiedOutOfFood = true;
                                }
                            }
                    }
                }
                return false;
            }
            return true;
        }

        public static void ApproxDaysWorthOfFoodPostfix(Caravan caravan, ref float __result)
        {
            // Checks if there's even a dendrovore, and if so find out how many for approximations later.
            int dendroCount = 0;
            foreach (Pawn pawn in caravan.PawnsListForReading)
                if (pawn.genes?.GetFirstGeneOfType<Gene_Dendrovore>() != null)
                    dendroCount++;

            if (dendroCount == 0) return;

            bool flag2 = NonDendrovore();

            bool NonDendrovore()
            {
                foreach (Pawn pawn in caravan.PawnsListForReading)
                    if (pawn.genes != null && pawn.needs.food != null && pawn.genes.GetFirstGeneOfType<Gene_Dendrovore>() == null)
                        return true;
                return false;
            }

            int curTile = caravan.Tile;

            // If the pawn can graze and there's no non-dendrovore, then there is theoretically infinite food left
            if (VirtualPlantsUtility.EnvironmentAllowsEatingVirtualPlantsNowAt(curTile) && Find.WorldGrid[curTile].biome.TreeDensity > 0)
            {
                if (flag2) return;
                __result = 600f;
            }
            else
            {
                // If a quick check for wood finds nothing, then there isn't any food left for the dendrovore
                if (GetWoodFromCaravan(caravan)) __result = 0;

                // Otherwise we need to get a total to compare to the current result, breaking as soon as we've exceeded the result if there's a non-dendrovore in the caravan
                // Uses 1.6 per day for a rough approximation of food per day to avoid excess performance usage
                int logCount = 0;
                // I'm going through all items to be sure no wood is missed
                foreach (Thing thing in CaravanInventoryUtility.AllInventoryItems(caravan))
                    if (thing.def.stuffProps?.categories?.Contains(StuffCategoryDefOf.Woody) == true)
                    {
                        logCount += thing.stackCount;
                        if (flag2 && logCount * nutritionPerLog / (1.6 * dendroCount) > __result) return;
                    }
                __result = (float)(logCount * nutritionPerLog / (1.6 * dendroCount));
            }
        }

        private static bool GetWoodFromCaravan(Caravan caravan)
        {
            if (CaravanInventoryUtility.TryGetThingOfDef(caravan, ThingDefOf.WoodLog, out Thing thing, out Pawn owner))
                return true;
            if (!cachedWoodyFood.NullOrEmpty())
                foreach (ThingDef def in cachedWoodyFood)
                    if (CaravanInventoryUtility.TryGetThingOfDef(caravan, def, out thing, out owner))
                        return true;
            return false;
        }
    }
}
