using DV.ServicePenalty.UI;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityModManagerNet;
using static UnityModManagerNet.UnityModManager;
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
            ModEntry ownershipModEntry = FindMod("DVProductionChains");
            if (ownershipModEntry != null && ownershipModEntry.Enabled)
            {
                return true;
            }
            else
            {
                DVCustomLogisticalHaulContractor.Log("Missing mod DVProductionChains");
                return false;
            }
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
    }
}