using DV.ServicePenalty.UI;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityModManagerNet;
using static UnityModManagerNet.UnityModManager;
using DVOwnership.Patches;
using DVCustomLogisticalHaulContractor.Patches;

namespace DVCustomLogisticalHaulContractor
{
    public static class DVCustomLogisticalHaulContractor
    {
        public static ModEntry mod;

        private static Harmony harmony;


        public static Version Version => mod.Version;

        public static List<StationController> stationControllers = new List<StationController>();

        static bool Load(ModEntry modEntry)
        {
            //TODO : Comment the code
            mod = modEntry;
            harmony = new Harmony(mod.Info.Id);
            harmony.PatchAll();
           ModEntry ownershipModEntry = FindMod("DVOwnership");
            if (ownershipModEntry != null && ownershipModEntry.Enabled)
            {
                try
                {
                    JobChainController_Patches.Setup();
                }
                catch (Exception exception5)
                {
                    OnCriticalFailure(exception5, "patching JobChainController");
                }
                return true;
            }
            else
            {
                DVCustomLogisticalHaulContractor.Log("Missing mod DVOwnership");
                return false;
            }
        }
        public static DynamicMethod Patch(MethodBase original, HarmonyMethod prefix = null, HarmonyMethod postfix = null, HarmonyMethod transpiler = null)
        {
            return (DynamicMethod)harmony.Patch(original, prefix, postfix, transpiler);
        }
        public static void Log(object message)
        {
            if (message is string) { mod.Logger.Log(message as string); }
            else
            {
                mod.Logger.Log("Logging object via UnityEngine.Debug...");
                Debug.Log(message);
            }
        }
        public static void OnCriticalFailure(Exception exception, string action)
        {
            Debug.Log(exception);
            mod.Logger.Critical("This happened while " + action + ".");
            mod.Logger.Critical("You can reactivate DVOwnership by restarting the game, but this failure type likely indicates an incompatibility between the mod and a recent game update. Please search the mod's Github issue tracker for a relevant report. If none is found, please open one and include this log file.");
            //Application.Quit();
        }
    }
}