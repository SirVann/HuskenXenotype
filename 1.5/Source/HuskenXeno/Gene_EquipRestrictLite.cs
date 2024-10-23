using RimWorld;
using Verse;
using System.Collections.Generic;

namespace HuskenXeno
{
    public class Gene_EquipRestrictLite : Gene
    {
        public override void PostAdd()
        {
            base.PostAdd();
            if (def.HasModExtension<EquipRestrictLiteExtension>() && pawn.apparel?.WornApparel.NullOrEmpty() == false)
            {
                EquipRestrictLiteExtension extension = def.GetModExtension<EquipRestrictLiteExtension>();

                List<Apparel> apparels = new List<Apparel>(pawn.apparel.WornApparel);
                foreach (Apparel apparel in apparels)
                {
                    if (!extension.layerEquipExceptions.NullOrEmpty() && extension.layerEquipExceptions.Contains(apparel.def)) continue;
                    foreach (ApparelLayerDef layer in extension.restrictedLayers)
                        if (apparel.def.apparel.layers.Contains(layer))
                        {
                            pawn.apparel.TryMoveToInventory(apparel);
                            break;
                        }
                }
            }
        }
    }
}
