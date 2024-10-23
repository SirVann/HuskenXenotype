using RimWorld;
using Verse;
using System.Collections.Generic;

namespace HuskenXeno
{
    public class HediffComp_BovineImplant : HediffComp
    {
        HediffCompProperties_BovineImplant Props => (HediffCompProperties_BovineImplant)props;

        private bool lost = false;

        public Pawn caster;

        public override void CompPostPostRemoved()
        {
            if (lost) return;

            List<GeneDef> xenogenes = new List<GeneDef>();
            foreach (Gene gene in caster.genes.Xenogenes)
                xenogenes.Add(gene.def);

            List<GeneDef> endoGenes = new List<GeneDef>();
            foreach (Gene gene in caster.genes.Endogenes)
                endoGenes.Add(gene.def);

            bool xenoFlag = caster.genes?.Xenotype != XenotypeDefOf.Baseliner;

            PawnGenerationRequest request = new PawnGenerationRequest(caster.kindDef ?? PawnKindDefOf.Colonist, caster.Faction ?? Faction.OfPlayer, forceGenerateNewPawn: true,
                    fixedLastName: PawnNamingUtility.GetLastName(caster), allowDowned: true, canGeneratePawnRelations: false, forceNoIdeo: true, fixedBiologicalAge: 3,
                    fixedChronologicalAge: 3, forcedXenotype: xenoFlag ? caster.genes?.Xenotype : XenotypeDefOf.Baseliner,
                    developmentalStages: DevelopmentalStage.Child)
            {
                DontGivePreArrivalPathway = true
            };

            if (!xenoFlag)
            {
                if (!xenogenes.NullOrEmpty())
                    request.ForcedXenogenes = xenogenes;
                if (!endoGenes.NullOrEmpty())
                    request.ForcedEndogenes = endoGenes;
            }

            Pawn pawn = PawnGenerator.GeneratePawn(request);
            pawn.relations.AddDirectRelation(PawnRelationDefOf.Parent, caster);

            FilthMaker.TryMakeFilth(Pawn.Position, Pawn.MapHeld, ThingDefOf.Filth_Blood, 4);

            if (pawn.playerSettings != null && caster.playerSettings != null)
                pawn.playerSettings.AreaRestrictionInPawnCurrentMap = caster.playerSettings.AreaRestrictionInPawnCurrentMap;

            PawnUtility.TrySpawnHatchedOrBornPawn(pawn, Pawn);

            if (pawn.Faction == Faction.OfPlayer)
            {
                pawn.babyNamingDeadline = Find.TickManager.TicksGame + 60000;
                ChoiceLetter_BabyBirth birthLetter = (ChoiceLetter_BabyBirth)LetterMaker.MakeLetter("Husken_ChildFormed".Translate(),
                    "Husken_ChildFormedDescription".Translate(caster.Label), LetterDefOf.BabyBirth, pawn);
                birthLetter.Start();
                Find.LetterStack.ReceiveLetter(birthLetter);
            }

            if (pawn.caller != null)
                pawn.caller.DoCall();

            Pawn.Kill(new DamageInfo(DamageDefOf.Cut, 99999f, 999f, -1f));
        }

        public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit = null)
        {
            base.Notify_PawnDied(dinfo, culprit);
            if (Props.lostOnDeath)
            {
                lost = true;
                Pawn.health.RemoveHediff(parent);
            }
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_References.Look(ref caster, "caster");
        }
    }
}
