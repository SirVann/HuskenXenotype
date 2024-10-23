using RimWorld;
using Verse;

namespace HuskenXeno
{
    public class Gene_AlwaysMale : Gene
    {
        public override void PostAdd()
        {
            pawn.gender = Gender.Male;
            if (def.bodyType == null && pawn.story?.bodyType == BodyTypeDefOf.Female)
            {
                pawn.story.bodyType = BodyTypeDefOf.Male;
                pawn.Drawer.renderer.SetAllGraphicsDirty();
            }
        }
    }
}
