using Verse;
using System.Collections.Generic;
using RimWorld;

namespace HuskenXeno
{
    public class CompProperties_AbilityRiseFromTheBovine : CompProperties_AbilityEffect
    {
        public List<PawnKindDef> validPawnKinds;

        public CompProperties_AbilityRiseFromTheBovine()
        {
            compClass = typeof(CompAbilityEffect_RiseFromTheBovine);
        }
    }
}
