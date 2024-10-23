using RimWorld;
using Verse;

namespace HuskenXeno
{
    public class CompAbilityEffect_RiseFromTheBovine : CompAbilityEffect
    {
        public new CompProperties_AbilityRiseFromTheBovine Props => (CompProperties_AbilityRiseFromTheBovine)props;

        public Pawn Caster => parent.pawn;

        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
        {
            return Valid(target, true);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (!base.Valid(target, throwMessages)) return false;

            string explanation = "CannotUseAbility".Translate(parent.def.label) + ": " + "Husken_MustBeBovine".Translate();

            if (target.Pawn == null || !Props.validPawnKinds.Contains(target.Pawn.kindDef))
            {
                if (throwMessages)
                    Messages.Message(explanation, MessageTypeDefOf.RejectInput, false);
                return false;
            }

            return true;
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            bool xenoFlag = Caster.genes?.Xenotype != null;
            PawnGenerationRequest request = new PawnGenerationRequest(Caster.kindDef ?? PawnKindDefOf.Colonist, Caster.Faction ?? Faction.OfPlayer, forceGenerateNewPawn: true,
                    fixedLastName: PawnNamingUtility.GetLastName(Caster), allowDowned: true, canGeneratePawnRelations: false, forceNoIdeo: true, fixedBiologicalAge: 3, fixedChronologicalAge: 3,
                    forcedXenotype: xenoFlag ? Caster.genes?.Xenotype : XenotypeDefOf.Baseliner, developmentalStages: DevelopmentalStage.Child)
            {
                DontGivePreArrivalPathway = true
            };

            Pawn pawn = PawnGenerator.GeneratePawn(request);
            pawn.relations.AddDirectRelation(PawnRelationDefOf.Parent, Caster);

            FilthMaker.TryMakeFilth(target.Pawn.Position, target.Pawn.MapHeld, ThingDefOf.Filth_Blood, 4);

            if (pawn.playerSettings != null && Caster.playerSettings != null)
                pawn.playerSettings.AreaRestrictionInPawnCurrentMap = Caster.playerSettings.AreaRestrictionInPawnCurrentMap;

            PawnUtility.TrySpawnHatchedOrBornPawn(pawn, target.Pawn);

            if (pawn.Faction == Faction.OfPlayer)
            {
                pawn.babyNamingDeadline = Find.TickManager.TicksGame + 60000;
                ChoiceLetter_BabyBirth birthLetter = (ChoiceLetter_BabyBirth)LetterMaker.MakeLetter("Husken_ChildFormed".Translate(),
                    "Husken_ChildFormedDescription".Translate(Caster.Label), LetterDefOf.BabyBirth, pawn);
                birthLetter.Start();
                Find.LetterStack.ReceiveLetter(birthLetter);
            }

            if (pawn.caller != null)
                pawn.caller.DoCall();

            target.Pawn.Kill(new DamageInfo(DamageDefOf.Cut, 99999f, 999f, -1f));
        }
    }
}
