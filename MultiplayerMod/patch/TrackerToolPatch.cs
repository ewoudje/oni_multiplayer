using System.Collections.Generic;
using HarmonyLib;
using MultiplayerMod.oni;

namespace MultiplayerMod.patch
{
    public class TrackerToolPatch
    {
        [HarmonyPatch(typeof(TrackerTool), "AddNewWorldTrackers")]
        public class ColonyDiagnosticUtilityPatch
        {
            public static void Postfix(TrackerTool __instance, int worldID)
            {
                __instance.GetPrivateField<List<WorldTracker>>("worldTrackers")
                    .Add(new MultiplayerErrorsTracker(worldID));
            }
        }
    }
}