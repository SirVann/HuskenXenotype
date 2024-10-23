using UnityEngine;
using Verse;

namespace HuskenXeno
{
    public class PawnRenderNode_BetterFurNode : PawnRenderNode_Fur
    {
        public PawnRenderNode_BetterFurNode(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree) : base(pawn, props, tree)
        {
        }

        public override Color ColorFor(Pawn pawn)
        {
            if (props.colorType == PawnRenderNodeProperties.AttachmentColorType.Hair)
                return pawn.story.HairColor;
            if (props.colorType == PawnRenderNodeProperties.AttachmentColorType.Skin)
                return pawn.story.SkinColor;
            if (props.color != null)
                return (Color)props.color;
            return Color.magenta;
        }
    }
}
