using RimWorld;
using Verse;
using System.Collections.Generic;

namespace HuskenXeno
{
    public class CompProperties_AbilityBovineImplant : CompProperties_AbilityGiveHediff
    {
        public List<PawnKindDef> validPawnKinds;

        public CompProperties_AbilityBovineImplant()
        {
            compClass = typeof(CompAbilityEffect_BovineImplant);
        }
    }
}
