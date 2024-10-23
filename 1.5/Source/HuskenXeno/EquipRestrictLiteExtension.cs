using Verse;
using System.Collections.Generic;

namespace HuskenXeno
{
    public class EquipRestrictLiteExtension : DefModExtension
    {
        public List<ApparelLayerDef> restrictedLayers; // Stops any equipment from being placed on this layer
        public List<ThingDef> layerEquipExceptions; // Exceptions to the restrictedLayers tag
    }
}
