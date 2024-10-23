using Verse;
using System.Collections.Generic;

namespace HuskenXeno
{
    public class Gene_HediffAdder : Gene
    {
        public override void PostAdd()
        {
            if (!Active || Overridden || !def.HasModExtension<HediffAdderExtension>()) return;
            base.PostAdd();
            HediffAdderExtension extension = def.GetModExtension<HediffAdderExtension>();
            if (extension != null && !extension.hediffsToApply.NullOrEmpty())
            {
                Dictionary<BodyPartDef, int> foundParts = new Dictionary<BodyPartDef, int>();
                foreach (HediffsToParts hediffToParts in extension.hediffsToApply)
                {
                    foundParts.Clear();
                    if (!hediffToParts.bodyParts.NullOrEmpty())
                    {
                        foreach (BodyPartDef bodyPartDef in hediffToParts.bodyParts)
                        {
                            if (pawn.RaceProps.body.GetPartsWithDef(bodyPartDef).NullOrEmpty()) continue;
                            if (foundParts.NullOrEmpty() || !foundParts.ContainsKey(bodyPartDef))
                                foundParts.Add(bodyPartDef, 0);

                            AddHediffToPart(pawn, pawn.RaceProps.body.GetPartsWithDef(bodyPartDef).ToArray()[foundParts[bodyPartDef]], hediffToParts.hediff, hediffToParts.severity, hediffToParts.severity);
                            foundParts[bodyPartDef]++;
                        }
                    }
                    else
                    {
                        if (HasHediff(pawn, hediffToParts.hediff))
                            continue;
                        AddOrAppendHediffs(pawn, hediffToParts.severity, 0, hediffToParts.hediff);
                    }
                }
            }
        }

        public override void PostRemove()
        {
            base.PostRemove();
            if (!def.HasModExtension<HediffAdderExtension>()) return;

            HediffAdderExtension extension = def.GetModExtension<HediffAdderExtension>();
            if (extension != null && !extension.hediffsToApply.NullOrEmpty())
                RemoveHediffsFromParts(pawn, extension.hediffsToApply);
        }

        public static void RemoveHediffsFromParts(Pawn pawn, List<HediffsToParts> hediffs = null, HediffsToParts hediffToParts = null)
        {
            if (hediffToParts != null && HasHediff(pawn, hediffToParts.hediff))
            {
                if (hediffToParts.bodyParts.NullOrEmpty()) RemoveHediffs(pawn, hediffToParts.hediff);
                else
                {
                    foreach (BodyPartDef bodyPart in hediffToParts.bodyParts)
                    {
                        Hediff firstHediffOfDef = null;
                        Hediff testHediff = pawn.health.hediffSet.GetFirstHediffOfDef(hediffToParts.hediff);

                        if (testHediff.Part.def == bodyPart) firstHediffOfDef = testHediff;
                        else
                            foreach (Hediff hediff in pawn.health.hediffSet.hediffs) // Go through all the hediffs to try to find the hediff on the specified part
                                if (hediff.Part.def == bodyPart && hediff.def == hediffToParts.hediff)
                                {
                                    firstHediffOfDef = hediff;
                                    break;
                                }

                        if (firstHediffOfDef != null) pawn.health.RemoveHediff(firstHediffOfDef);
                    }
                }
            }
            if (!hediffs.NullOrEmpty())
            {
                foreach (HediffsToParts hediffPart in hediffs)
                {
                    if (!HasHediff(pawn, hediffPart.hediff)) continue;
                    if (hediffPart.bodyParts.NullOrEmpty()) RemoveHediffs(pawn, hediffPart.hediff);
                    else
                    {
                        foreach (BodyPartDef bodyPart in hediffPart.bodyParts)
                        {
                            Hediff firstHediffOfDef = null;
                            Hediff testHediff = pawn.health.hediffSet.GetFirstHediffOfDef(hediffPart.hediff);

                            if (testHediff.Part.def == bodyPart) firstHediffOfDef = testHediff;
                            else
                                foreach (Hediff hediff in pawn.health.hediffSet.hediffs) // Go through all the hediffs to try to find the hediff on the specified part
                                    if (hediff.Part.def == bodyPart && hediff.def == hediffPart.hediff)
                                    {
                                        firstHediffOfDef = hediff;
                                        break;
                                    }

                            if (firstHediffOfDef != null) pawn.health.RemoveHediff(firstHediffOfDef);
                        }
                    }
                }
            }
        }

        public static void RemoveHediffs(Pawn pawn, HediffDef hediff = null, List<HediffDef> hediffs = null)
        {
            if (pawn?.health?.hediffSet == null) return;
            if (hediff != null)
            {
                Hediff hediffToRemove = pawn.health.hediffSet.GetFirstHediffOfDef(hediff);
                if (hediffToRemove != null) pawn.health.RemoveHediff(hediffToRemove);
            }

            if (!hediffs.NullOrEmpty())
            {
                foreach (HediffDef hediffDef in hediffs)
                {
                    Hediff hediffToRemove = pawn.health.hediffSet.GetFirstHediffOfDef(hediffDef);
                    if (hediffToRemove != null) pawn.health.RemoveHediff(hediffToRemove);
                }
            }
        }

        public static Hediff AddHediffToPart(Pawn pawn, BodyPartRecord bodyPart, HediffDef hediffDef, float initialSeverity = 1, float severityAdded = 0)
        {
            Hediff firstHediffOfDef = null;
            if (HasHediff(pawn, hediffDef))
            {
                Hediff testHediff = pawn.health.hediffSet.GetFirstHediffOfDef(hediffDef);
                if (testHediff.Part == bodyPart) firstHediffOfDef = testHediff;
                else
                {
                    foreach (Hediff hediff in pawn.health.hediffSet.hediffs) // Go through all the hediffs to try to find the hediff on the specified part
                    {
                        if (hediff.Part == bodyPart && hediff.def == hediffDef) firstHediffOfDef = hediff;
                        break;
                    }
                }
            }

            if (firstHediffOfDef != null) pawn.health.RemoveHediff(firstHediffOfDef);

            firstHediffOfDef = pawn.health.AddHediff(hediffDef, bodyPart);
            firstHediffOfDef.Severity = initialSeverity;

            return firstHediffOfDef;
        }

        public static void AddOrAppendHediffs(Pawn pawn, float initialSeverity = 1, float severityIncrease = 0, HediffDef hediff = null, List<HediffDef> hediffs = null)
        {
            if (hediff != null)
            {
                if (HasHediff(pawn, hediff))
                {
                    pawn.health.hediffSet.GetFirstHediffOfDef(hediff).Severity += severityIncrease;
                }
                else if (initialSeverity > 0)
                {
                    Hediff newHediff = HediffMaker.MakeHediff(hediff, pawn);
                    newHediff.Severity = initialSeverity;
                    pawn.health.AddHediff(newHediff);
                }
            }
            if (!hediffs.NullOrEmpty())
            {
                foreach (HediffDef hediffDef in hediffs)
                {
                    if (HasHediff(pawn, hediffDef))
                    {
                        pawn.health.hediffSet.GetFirstHediffOfDef(hediffDef).Severity += severityIncrease;
                    }
                    else if (initialSeverity > 0)
                    {
                        Hediff newHediff = HediffMaker.MakeHediff(hediffDef, pawn);
                        newHediff.Severity = initialSeverity;
                        pawn.health.AddHediff(newHediff);
                    }
                }
            }
        }

        public static bool HasHediff(Pawn pawn, HediffDef hediff) // Only made this to make checking for null hediffSets require less work
        {
            if (pawn?.health?.hediffSet == null || hediff == null) return false;
            if (pawn.health.hediffSet.HasHediff(hediff)) return true;
            return false;
        }
    }
}
