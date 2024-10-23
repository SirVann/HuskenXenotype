using Verse;
using RimWorld;

namespace HuskenXeno
{
    public class HediffCompProperties_BovineImplant : HediffCompProperties
    {
        public bool lostOnDeath;

        public HediffCompProperties_BovineImplant()
        {
            compClass = typeof(HediffComp_BovineImplant);
        }
    }
}
