using DV.Logic.Job;
using DV.Utils;
using HarmonyLib;
using DV.ThingTypes;
using DVOwnership;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection.Emit;
using DVOwnership.Patches;

namespace DVCustomLogisticalHaulContractor.Patches
{
    internal class JobChainController_Patches
    {

        private static bool isSetup = false;

        public static void Setup()
        {
            if (isSetup)
            {
                DVCustomLogisticalHaulContractor.Log("Trying to set up JobChainController patches, but they've already been set up!");
                return;
            }

            DVCustomLogisticalHaulContractor.Log("Setting up JobChainController patches.");
            isSetup = true;
            var JCC_JobCompleted = AccessTools.Method(typeof(JobChainController), "OnJobCompleted");
            var JCC_JobCompleted_postfix = AccessTools.Method(typeof(JobChainController_Patches), nameof(OnJobCompleted_postfix));
            //var JCWEHG_OnLastJobInChainCompleted_Transpiler = AccessTools.Method(typeof(JobChainControllerWithEmptyHaulGeneration_Patches), nameof(OnLastJobInChainCompleted_Transpiler));
            DVCustomLogisticalHaulContractor.Patch(JCC_JobCompleted, postfix: new HarmonyMethod(JCC_JobCompleted_postfix));
        }
        static void OnJobCompleted_postfix(JobChainController __instance, Job completedJob)
        {
            DVCustomLogisticalHaulContractor.Log("Method Called");
            var jobType = completedJob.jobType;
            if (jobType == JobType.EmptyHaul)
            {
                DVCustomLogisticalHaulContractor.Log("Job done try to generate new jobs");
                foreach(var controller in MonoBehaviour.FindObjectsOfType<StationProceduralJobsController>())
                {
                    controller.TryToGenerateJobs();
                }
            }

        }
    }
}
