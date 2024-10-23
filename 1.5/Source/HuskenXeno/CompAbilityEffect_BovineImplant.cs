using RimWorld;
using Verse;

namespace HuskenXeno
{
    public class CompAbilityEffect_BovineImplant : CompAbilityEffect_GiveHediff
    {
        public new CompProperties_AbilityBovineImplant Props => (CompProperties_AbilityBovineImplant)props;

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

            return base.Valid(target, throwMessages);
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            if (target.Pawn != null)
            {
                Hediff hediff = target.Pawn.health.hediffSet.GetFirstHediffOfDef(Props.hediffDef);
                if (hediff.TryGetComp<HediffComp_BovineImplant>() != null)
                    hediff.TryGetComp<HediffComp_BovineImplant>().caster = parent.pawn;
            }
        }
    }
}
