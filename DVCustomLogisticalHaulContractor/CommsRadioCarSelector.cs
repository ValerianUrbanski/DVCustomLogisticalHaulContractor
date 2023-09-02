using DV;
using DV.Garages;
using DV.Teleporters;
using DV.Logic.Job;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using System.Collections;
using DV.InventorySystem;
using DV.ThingTypes.TransitionHelpers;
using DV.ThingTypes;
using DV.Utils;

namespace DVCustomLogisticalHaulContractor
{
    public class CommsRadioCarSelector : MonoBehaviour, ICommsRadioMode
    {
        private const float SIGNAL_RANGE = 100f;
        private const float PRICE_PER_KG = 0.25f;
        private const float MAX_DELETE_PRICE = 5000f;
        private static Vector3 HIGHLIGHT_BOUNDS_EXTENSION = new Vector3(0.25f, 0.8f, 0f);
        private static Color laserColor = new Color(0.09f, 0.46f, 1f, 1f);

        private const string CONFIRM_TEXT = "confirm";
        private const string CANCEL_TEXT = "cancel";
        public Transform signalOrigin;
        public CommsRadioDisplay display;
        public Material selectionMaterial;

        [Header("Sounds")]
        public AudioClip hoverOverCar;
        public AudioClip selectedCarSound;
        public AudioClip removeCarSound;
        public AudioClip confirmSound;
        public AudioClip cancelSound;
        public AudioClip warningSound;
        public AudioClip moneyRemovedSound;

        [Header("Highlighters")]
        public GameObject trainHighlighter;
        private MeshRenderer trainHighlighterRender;

        private TrainCar pointedCar;
        private TrainCar SelectedCar;

        private Job jobOfCar;
        private RaycastHit hit;
        private LayerMask trainCarMask;
        private float removePrice = float.PositiveInfinity;
        private CommsRadioCarSelector.State state;
        private CommsRadioCarSelector.Action action;

        private StationController[] lstStationControllers =null;
        private List<String> stationsNames = new List<String>();
        private int currentStationIndex = 0;

        private Track departureTrack = null;


        private List<Station> lstSation = new List<Station>();

        private List<TrainCar> lstCars = new List<TrainCar>();

        private String Error = "";

        private bool displayingError = false;

        public ButtonBehaviourType ButtonBehaviour { get; private set; }

        
        public Color GetLaserBeamColor() { return laserColor; }
        
        [Header("Strings")]
        private const string MODE_NAME = "CUSTOM HAUL";
        private const string CONTENT_MAINMENU = "AIM at the Cars you wich to select to Haul";
        //private const string ACTION_RETURN = "Return";
       
        protected enum State
        {
            ScanCarToAdd,
            Confirm,
            Cancel,
            CreatingJob
        }
        protected enum Action
        {
            Increase,
            Decrease
        }

        public void Awake()
        {
            CheckForStartingCondition();
            var summoner = controller.deleteControl;
            //this.ButtonBehaviour = ButtonBehaviourType.Regular;
            if(this.lstStationControllers == null)
            {
                this.lstStationControllers = FindObjectsOfType<StationController>();
                DVCustomLogisticalHaulContractor.Log("Loaded " + lstStationControllers.Length + " Station Controllers");
                if(this.lstStationControllers.Length>0)
                {
                    for(int i = 0; i < this.lstStationControllers.Length; i++) 
                    {
                        this.stationsNames.Add(this.lstStationControllers[i].stationInfo.Name);
                        lstStationControllers[i].OverridePlayerEnteredJobGenerationZoneFlag();
                    }
                    this.stationsNames.Add("RETURN");
                }
            }
            if (summoner == null) { throw new Exception("Crew vehicle radio mode could not be found!"); }

            this.signalOrigin = summoner.signalOrigin;
            this.display = summoner.display;

            this.selectionMaterial = summoner.selectionMaterial;
            this.trainHighlighter = summoner.trainHighlighter;

            this.confirmSound = summoner.confirmSound;
            this.cancelSound = summoner.cancelSound;
            this.SelectedCar = null;
            this.lstCars = new List<TrainCar>();

            /*if (!this.signalOrigin)
            {
                signalOrigin = base.transform;
            }*/
            DVCustomLogisticalHaulContractor.Log("Contractor AWAKE");
             if (!this.signalOrigin)
             {
                 DVCustomLogisticalHaulContractor.Log("signalOrigin on CommsRadioCarDeleter isn't set, using this.transform!");
                 this.signalOrigin = base.transform;
             }
             if (this.display == null)
             {
                 DVCustomLogisticalHaulContractor.Log("display not set, can't function properly!");
             }
             if (this.selectionMaterial == null)
             {
                 DVCustomLogisticalHaulContractor.Log("Some of the required materials isn't set. Visuals won't be correct.");
             }
             if (this.trainHighlighter == null)
             {
                 DVCustomLogisticalHaulContractor.Log("trainHighlighter not set, can't function properly!!");
             }
             this.trainCarMask = LayerMask.GetMask(new string[]
             {
                 "Train_Big_Collider"
             });
             this.trainHighlighterRender = this.trainHighlighter.GetComponentInChildren<MeshRenderer>(true);
             this.trainHighlighter.SetActive(false);
             this.trainHighlighter.transform.SetParent(null);
            this.setState(CommsRadioCarSelector.State.ScanCarToAdd);
            this.SetStartingDisplay();
        }
        private void CheckForStartingCondition()
        {
            LicenseManager lm = MonoBehaviour.FindObjectOfType<LicenseManager>();
            if (!lm.IsGeneralLicenseAcquired(TransitionHelpers.ToV2(GeneralLicenseType.ConcurrentJobs1)) || !lm.IsJobLicenseAcquired(TransitionHelpers.ToV2(JobLicenses.LogisticalHaul)))
            {
                lm.AcquireGeneralLicense(TransitionHelpers.ToV2(GeneralLicenseType.ConcurrentJobs1));
                lm.AcquireJobLicense(TransitionHelpers.ToV2(JobLicenses.LogisticalHaul));
            }
        }
        private void HighlightCar(TrainCar car, Material highlightMaterial)
        {
            if (car == null)
            {
                DVCustomLogisticalHaulContractor.Log("Highlight car is null. Ignoring request.");
                return;
            }
            this.trainHighlighterRender.material = highlightMaterial;
            this.trainHighlighter.transform.localScale = car.Bounds.size + CommsRadioCarSelector.HIGHLIGHT_BOUNDS_EXTENSION;
            Vector3 b = car.transform.up * (this.trainHighlighter.transform.localScale.y / 2f);
            Vector3 b2 = car.transform.forward * car.Bounds.center.z;
            Vector3 position = car.transform.position + b + b2;
            this.trainHighlighter.transform.SetPositionAndRotation(position, car.transform.rotation);
            this.trainHighlighter.SetActive(true);
            this.trainHighlighter.transform.SetParent(car.transform, true);
        }
        private void PointToCar(TrainCar car)
        {
            if (this.pointedCar != car)
            {
                if (car != null)
                {
                    this.pointedCar = car;
                    this.HighlightCar(this.pointedCar, this.selectionMaterial);
                    CommsRadioController.PlayAudioFromRadio(this.hoverOverCar, base.transform);
                    return;
                }
                this.pointedCar = null;
                this.ClearHighlightCar();
            }
        }
        private void ClearHighlightCar()
        {
            this.trainHighlighter.SetActive(false);
            this.trainHighlighter.transform.SetParent(null);
        }
        private void ClearFlags()
        {
            this.lstCars = new List<TrainCar>();
            this.SelectedCar = null;
            this.pointedCar = null;
            this.departureTrack = null;
            this.updateContent();
            this.ClearHighlightCar();
            this.setState(CommsRadioCarSelector.State.ScanCarToAdd);
            this.SetStartingDisplay();
        }
        private void setState(CommsRadioCarSelector.State newState)
        {
           // DVCustomLogisticalHaulContractor.Log($"Entering state : {this.state.ToString()}");
            if (this.state == newState)
            {
                return;
            }
            CommsRadioCarSelector.State state = this.state;
            this.state = newState;
            switch (this.state)
            {
                case CommsRadioCarSelector.State.ScanCarToAdd:
                    {
                        DVCustomLogisticalHaulContractor.Log("Scan car state");
                        this.display.SetAction(null);
                        this.ButtonBehaviour = ButtonBehaviourType.Regular;
                        return;
                    }
                case CommsRadioCarSelector.State.Confirm:
                    {
                        this.display.SetAction("Confirm");
                        break;
                    }
                case CommsRadioCarSelector.State.Cancel:
                    {
                        this.display.SetAction("Cancel");
                        break;
                    }
                case CommsRadioCarSelector.State.CreatingJob:
                    {
                        this.display.SetContent("Select a station using the wheel scroll");
                        this.display.SetAction("Confirm");
                        this.ButtonBehaviour = ButtonBehaviourType.Override;
                        StopAllCoroutines();
                        return;
                    }
                default:
                    {
                        return;
                    }
            }
        }
        public void Enable()
        {
            this.Awake();
            this.setState(CommsRadioCarSelector.State.ScanCarToAdd);
        }

        public void Disable()
        {
            this.ClearFlags();
        }
        public void OverrideSignalOrigin(Transform signalOrigin)
        {
            this.signalOrigin = signalOrigin;
        }

        public void OnUse()
        {
            //try { 
            DVCustomLogisticalHaulContractor.Log("Use Btn");
                switch (this.state)
                {
                    case CommsRadioCarSelector.State.ScanCarToAdd:
                        {
                            if (this.checkIfInList(this.pointedCar))
                            {
                                this.setState(CommsRadioCarSelector.State.Confirm);
                                return;
                            }
                            if (this.pointedCar != null && JobsManager.Instance.GetJobOfCar(this.pointedCar) == null)
                            {
                                this.SelectedCar = this.pointedCar;
                                this.jobOfCar = JobsManager.Instance.GetJobOfCar(this.pointedCar);
                                if (!(this.checkIfInList(this.SelectedCar)))
                                {
                                    if (this.lstCars.Count == 0 && this.departureTrack == null)
                                    {
                                        this.departureTrack = this.SelectedCar.logicCar.CurrentTrack;
                                    }
                                    else if (this.lstCars.Count > 1)
                                    {
                                        if (this.SelectedCar.logicCar.CurrentTrack != this.departureTrack)
                                        {
                                            DVCustomLogisticalHaulContractor.Log("Cars are not on the same Track");
                                            this.Error = "The selected Car is not on the Departure Track";
                                            StartCoroutine("ErrorDisplay");
                                            return;
                                        }
                                    }
                                    this.lstCars.Add(this.SelectedCar);
                                    this.SelectedCar = null;
                                }
                                this.pointedCar = null;
                                this.updateContent();
                                CommsRadioController.PlayAudioFromRadio(this.confirmSound, base.transform);
                                return;
                            }
                            else if (JobsManager.Instance.GetJobOfCar(this.pointedCar) != null)
                            {
                                DVCustomLogisticalHaulContractor.Log("The car already implied into a job");
                                this.display.SetContent("The Car is currently into a job");
                                return;
                            }
                            else
                            {
                                DVCustomLogisticalHaulContractor.Log("Cannot get pointed car");
                            }
                            break;
                        }
                    case CommsRadioCarSelector.State.Confirm:
                        {
                            if (!(this.checkIfInList(this.pointedCar)))
                            {
                                this.setState(CommsRadioCarSelector.State.ScanCarToAdd);
                                return;
                            }
                            if (this.checkIfInList(this.pointedCar))
                            {
                                this.setState(CommsRadioCarSelector.State.CreatingJob);
                                CommsRadioController.PlayAudioFromRadio(confirmSound, base.transform);
                                return;
                            }
                            break;
                        }
                    case CommsRadioCarSelector.State.Cancel:
                        {
                            this.ClearFlags();
                            break;
                        }
                    case CommsRadioCarSelector.State.CreatingJob:
                        {
                            if (this.stationsNames[this.currentStationIndex].Equals("RETURN"))
                            {
                                DVCustomLogisticalHaulContractor.Log("RETURN SELECTED");
                                this.setState(CommsRadioCarSelector.State.ScanCarToAdd);
                                this.updateContent();
                                return;
                            }
                            PlayerManager.GetWorldAbsolutePlayerPosition();
                            JobGeneratorController jobGeneratorController = new JobGeneratorController(this.lstStationControllers, this.lstStationControllers[this.currentStationIndex], this.lstCars, this.departureTrack);
                            String error = jobGeneratorController.generateLogisticalJob();
                            this.Error = error;
                            StartCoroutine("ErrorDisplay");
                            this.ClearFlags();
                            return;
                        }
                    default:
                        {
                            this.ClearFlags();
                            break;
                        }
                }
            /*}
            catch (Exception e) {
                DVCustomLogisticalHaulContractor.Log($"Exception Occured {e.Message}");
            }*/
        }
        IEnumerator ErrorDisplay()
        {
            this.displayingError = true;
            DVCustomLogisticalHaulContractor.Log("Displaying Error");
            this.display.SetContent(this.Error);
            yield return new WaitForSeconds(3.0f);
            this.displayingError = false;
            this.updateContent();
            this.Awake();
        }
        public void OnUpdate()
        { 
            switch (this.state)
            {
                case CommsRadioCarSelector.State.ScanCarToAdd:
                    {
                        if (!(this.SelectedCar == null))
                        {
                            DVCustomLogisticalHaulContractor.Log("Invalid setup for current state, reseting flags!");
                            this.ClearFlags();
                            return;
                        }
                        if (!Physics.Raycast(this.signalOrigin.position, this.signalOrigin.forward, out this.hit, 100f, this.trainCarMask))
                        {
                            this.PointToCar(null);
                            return;
                        }
                        TrainCar trainCar = TrainCar.Resolve(this.hit.transform.root);
                        if (trainCar.IsLoco)
                        {
                            this.setState(CommsRadioCarSelector.State.Cancel);
                            this.ClearHighlightCar();
                            return;
                        }
                        if (trainCar == null || trainCar == PlayerManager.Car)
                        {
                            this.setState(CommsRadioCarSelector.State.Cancel);
                            this.ClearHighlightCar();
                            this.PointToCar(null);
                            return;
                        }
                        this.PointToCar(trainCar);
                        if(this.checkIfInList(this.pointedCar))
                        {
                            this.setState(CommsRadioCarSelector.State.Confirm);
                            return;
                        }
                        else
                        {
                            this.setState(CommsRadioCarSelector.State.ScanCarToAdd);
                        }
                        return;
                    }
                case CommsRadioCarSelector.State.CreatingJob:
                    {
                        return;
                    }
                default:
                    {
                        if (!Physics.Raycast(this.signalOrigin.position, this.signalOrigin.forward, out this.hit, 100f, this.trainCarMask))
                        {
                            this.setState(CommsRadioCarSelector.State.Cancel);
                            return;
                        }
                        TrainCar trainCar = TrainCar.Resolve(this.hit.transform.root);
                        if (trainCar.IsLoco)
                        {
                            this.ClearHighlightCar();
                            this.setState(CommsRadioCarSelector.State.Cancel);
                            return;
                        }
                        if (trainCar == null || trainCar == PlayerManager.Car)
                        {
                            this.setState(CommsRadioCarSelector.State.Cancel);
                            this.ClearHighlightCar();
                            this.PointToCar(null);
                            return;
                        }
                        this.PointToCar(trainCar);
                        if (this.checkIfInList(this.pointedCar))
                        {
                            this.setState(CommsRadioCarSelector.State.Confirm);
                            return;
                        }
                        else
                        {
                            this.setState(CommsRadioCarSelector.State.ScanCarToAdd);
                        }
                        return;
                    }
            }
        }
        private bool checkIfInList(TrainCar car)
        {
            for(int i = 0;i<this.lstCars.Count; i++)
            {
                if (this.lstCars[i] == car)
                {
                    return true;
                }
            }
            return false;
        }
        public bool ButtonACustomAction()
        {
            if(this.state != CommsRadioCarSelector.State.CreatingJob)
            {
                return false;
            }
            TransitionUpdate(CommsRadioCarSelector.Action.Increase);
            return true;
        }

        public bool ButtonBCustomAction()
        {
            if (this.state != CommsRadioCarSelector.State.CreatingJob)
            {
                return false;
            }
            TransitionUpdate(CommsRadioCarSelector.Action.Decrease);
            return true;
        }
        private void TransitionUpdate(CommsRadioCarSelector.Action action)
        {
            switch(action)
            {
                case CommsRadioCarSelector.Action.Increase:
                    {
                        
                        if((this.currentStationIndex + 1) <  (this.stationsNames.Count))
                        {
                            this.currentStationIndex++;
                        }
                        TransitionToNextStationIndex();
                        return;
                    }
                case CommsRadioCarSelector.Action.Decrease:
                    {
                        if (this.currentStationIndex >0)
                        {
                            this.currentStationIndex --;
                        }
                        TransitionToNextStationIndex();
                        return;
                    }
            }
        }
        public void SetStartingDisplay()
        {
            if(this.displayingError)
            {
                return;
            }
            display.SetDisplay(MODE_NAME, CONTENT_MAINMENU);
        }
        private void DisplayMainMenu()
        {
            SetStartingDisplay();
        }
        private void OnDestroy()
        {
            if (UnloadWatcher.isUnloading)
            {
                return;
            }
            if (this.trainHighlighter != null)
            {
                UnityEngine.Object.Destroy(this.trainHighlighter.gameObject);
            }
        }
        private void TransitionToNextStationIndex()
        {
            display.SetContent(this.stationsNames[this.currentStationIndex]);
        }
        private void updateContent()
        {
            if(this.displayingError)
            {
                return;
            }
            if(this.lstCars.Count == 0)
            {
                this.SetStartingDisplay();
            }
            String str = "";
            int cpt = 0;
            foreach(TrainCar car in this.lstCars)
            {
                if (this.lstCars.Count <= 5)
                {
                    str += car.ID + "\n";
                    cpt++;
                }
                else if(this.lstCars.Count>5 && this.lstCars.Count <10)
                {
                    str += car.ID + " ";
                    if(cpt%2==0)
                    {
                        str += "\n";
                    }
                }
                else if(this.lstCars.Count>=10)
                {
                    str += car.ID + " ";
                    if (cpt % 3 == 0)
                    {
                        str += "\n";
                    }
                }
            }
            this.display.SetContent(str);
        }
        public static CommsRadioController controller;
        [HarmonyPatch(typeof(CommsRadioController), "Awake")]
        class CommsRadioCarSelector_Awake_Patch
        {

            public static CommsRadioCarSelector selector = null;

            static void Postfix(CommsRadioController __instance, List<ICommsRadioMode> ___allModes)
            {
                controller = __instance;
                if (selector == null) { selector = controller.gameObject.AddComponent<CommsRadioCarSelector>(); }
                if (!___allModes.Contains(selector))
                {
                    int spawnerIndex = ___allModes.FindIndex(mode => mode is CommsRadioCarSpawner);
                    if (spawnerIndex != -1) { ___allModes.Insert(spawnerIndex, selector); }
                    else { ___allModes.Add(selector); }
                    controller.ReactivateModes();
                }
            }
        }
    }
}
