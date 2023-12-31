﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DV;
using DV.Logic.Job;
using UnityEngine;
using HarmonyLib;
using System.Collections;
using DV.ThingTypes;
using DV.ThingTypes.TransitionHelpers;
using DV.Utils;
using JetBrains.Annotations;

namespace DVCustomLogisticalHaulContractor
{
    internal class JobGeneratorController : MonoBehaviour
    {
        private List<TrainCar> TrainCars = new List<TrainCar>();
        private StationController[] stationControllers = null;
        private StationController destination = null;
        private StationsChainData chainData = null;
        private StationController origin = null; 
        private Track departureTrack = null;
        private Track ArrivalTrack = null;
        private List<Car> cars = null;

        private EmptyHaulJobProceduralGenerator emptyHaulGenerator = null;


        public JobGeneratorController(StationController[] stationControllers, StationController destination,List<TrainCar> trainCars,Track departureTrack) 
        {
            DVCustomLogisticalHaulContractor.Log("Initializing Job Generator");
            this.emptyHaulGenerator = new EmptyHaulJobProceduralGenerator();
            this.stationControllers = stationControllers;
            this.TrainCars= trainCars;
            this.destination = destination;
            this.departureTrack = departureTrack;
            this.cars = new List<Car>();
            foreach(TrainCar car in trainCars)
            {
                this.cars.Add(car.logicCar);
            }
            DVCustomLogisticalHaulContractor.Log("Generating Job");
            DVCustomLogisticalHaulContractor.Log("Destination Accept Cars :" + isStationAcceptingCargoType().ToString());
        }
        public String generateLogisticalJob()
        {
            if(!(isStationAcceptingCargoType()))
            {
                return "Destination Does not Accept Cars";
            }
            this.origin = this.getOrigin();
            DVCustomLogisticalHaulContractor.Log("Origin SET " + this.origin.name);
            this.ArrivalTrack = this.pickADestinationTrack();
            if(this.ArrivalTrack == null)
            {
                return "No Arrival Track Available Try Smaller Train";
            }
            DVCustomLogisticalHaulContractor.Log("Arrival Track set " + this.ArrivalTrack.ID);
            DVCustomLogisticalHaulContractor.Log("Departure Track set " + this.departureTrack.ID);
            this.chainData = this.GetStationsChainData();
            DVCustomLogisticalHaulContractor.Log("Chains DATA SET");
            DVCustomLogisticalHaulContractor.Log("Generating logistical Job at Station " + this.origin.name);
            var gameObject = new GameObject($"ChainJob[{JobType.EmptyHaul}]: {this.origin.logicStation.ID}");
            gameObject.transform.SetParent(this.origin.transform);

            var jobChainController = new JobChainControllerWithEmptyHaulGeneration(gameObject);
            jobChainController.trainCarsForJobChain = getTrainCarsFromCar(this.cars).ToList();
            var jobDefinition = getJobDefinition(gameObject, this.origin.logicStation, this.departureTrack, this.ArrivalTrack, this.cars, getTimeLimit(), this.getJobPayment(), this.chainData, this.getRequiredLicences());
            jobChainController.AddJobDefinitionToChain(jobDefinition);
            jobChainController.FinalizeSetupAndGenerateFirstJob();
            //JobsGenerator.CreateEmptyHaulJob(this.origin.logicStation, this.chainData, this.cars, this.departureTrack, this.ArrivalTrack, getTimeLimit(), this.getJobPayment(), null,this.getRequiredLicences());
            return "Job Created";
        }
        private StaticEmptyHaulJobDefinition getJobDefinition(GameObject chainJobGO, Station logicStation, Track startingTrack, Track destinationTrack, List<Car> logicCarsToHaul, float bonusTimeLimit, float baseWage, StationsChainData stationsChainData, JobLicenses requiredLicenses)
        {
            var jobDefinition = chainJobGO.AddComponent<StaticEmptyHaulJobDefinition>();
            jobDefinition.PopulateBaseJobDefinition(logicStation, bonusTimeLimit, baseWage, stationsChainData, requiredLicenses);
            jobDefinition.startingTrack = startingTrack;
            jobDefinition.trainCarsToTransport = logicCarsToHaul;
            jobDefinition.destinationTrack = destinationTrack;
            return jobDefinition;
        }
        private IEnumerable<TrainCar> getTrainCarsFromCar(IEnumerable<Car> cars)
        {
            var trainCars = new List<TrainCar>();

            if (cars == null || cars.Count() == 0) { return trainCars; }
            var allTrainCars = SingletonBehaviour<CarSpawner>.Instance.AllCars;
            var trainCarsByLogicCar = new Dictionary<Car, TrainCar>();

            foreach (var trainCar in allTrainCars)
            {
                trainCarsByLogicCar.Add(trainCar.logicCar, trainCar);
            }

            return from car in cars select trainCarsByLogicCar[car];
        }
        private float getJobPayment()
        {
            
            List<TrainCarLivery> lstTrainCarType = new List<TrainCarLivery>();
            Dictionary<TrainCarLivery, int> dictionary = new Dictionary<TrainCarLivery, int>();
            foreach (TrainCar TrainCar in this.TrainCars)
            {
                lstTrainCarType.Add(TrainCar.logicCar.carType);
            }
            foreach (TrainCarLivery trainCarType in lstTrainCarType)
            {
                if (!dictionary.ContainsKey(trainCarType))
                {
                    dictionary[trainCarType] = 0;
                }
                Dictionary<TrainCarLivery, int> dictionary2 = dictionary;
                TrainCarLivery key = trainCarType;
                int num = dictionary2[key];
                dictionary2[key] = num + 1;
            }
            Dictionary<CargoType, int> cargoData = new Dictionary<CargoType, int>();
            PaymentCalculationData pcd = new PaymentCalculationData(dictionary,cargoData);
            return JobPaymentCalculator.CalculateJobPayment(JobType.EmptyHaul, JobPaymentCalculator.GetDistanceBetweenStations(this.origin, this.destination),pcd);
        }
        private float getTimeLimit()
        {
            float bonusTimeLimit = 0f;
            float distanceBetweenStations = JobPaymentCalculator.GetDistanceBetweenStations(this.origin, this.destination);
            bonusTimeLimit = JobPaymentCalculator.CalculateHaulBonusTimeLimit(distanceBetweenStations, false);
            return bonusTimeLimit;
        }
        private JobLicenses getRequiredLicences()
        {
            //List<JobLicenses> jobLicenses = new List<JobLicenses>();
            DVObjectModel types = Globals.G.Types;
            HashSet<JobLicenseType_v2> licensesV2 = SingletonBehaviour<LicenseManager>.Instance.GetRequiredLicensesForJobType(JobType.EmptyHaul);
            licensesV2.Add(SingletonBehaviour<LicenseManager>.Instance.GetRequiredLicenseForNumberOfTransportedCars(this.TrainCars.Count));
            DVCustomLogisticalHaulContractor.Log("Here ");
            JobLicenses required = JobLicenses.Basic;
            foreach (JobLicenseType_v2 license in licensesV2)
            {
                required |= types.JobLicenses_to_v2.FirstOrDefault(x => x.Value == license).Key;
            }
            DVCustomLogisticalHaulContractor.Log(required);
            return required; 
        }
        private StationController getOrigin()
        {
            StationController station = null;
            StationJobGenerationRange[] lstStationRange = FindObjectsOfType<StationJobGenerationRange>();
            for (int i = 0; i < lstStationRange.Length; i++)
            {
                if (lstStationRange[i].IsPlayerInJobGenerationZone(lstStationRange[i].PlayerSqrDistanceFromStationCenter))
                {
                    DVCustomLogisticalHaulContractor.Log(lstStationRange[i].GetComponent<StationController>().logicStation.name);
                    station = lstStationRange[i].GetComponent<StationController>();
                }
            }
            return station;
        }
        private Track pickADestinationTrack()
        {
            YardTracksOrganizer yto = FindObjectOfType<YardTracksOrganizer>();
            CarSpawner carSpawner = FindObjectOfType<CarSpawner>();
            if(yto != null)
            {
                DVCustomLogisticalHaulContractor.Log("YTO FOUND !");
            }
            List<Track> lstTracks = yto.FilterOutTracksWithoutRequiredFreeSpace(this.destination.logicStation.yard.StorageTracks, carSpawner.GetSeparationLengthBetweenCars(this.cars.Count));
            DVCustomLogisticalHaulContractor.Log("Number of potential Tracks " + lstTracks.Count);
            System.Random rdm = new System.Random();
            if(lstTracks.Count == 0)
            {
                return null;
            }
            return lstTracks[rdm.Next(0, lstTracks.Count()-1)];
        }
        private StationsChainData GetStationsChainData()
        {
            return new StationsChainData(this.departureTrack.ID.yardId,this.ArrivalTrack.ID.yardId);
        }
        private bool isStationAcceptingCargoType()
        {
           HashSet<CargoType> cargoTypes = new HashSet<CargoType>();
            HashSet<TrainCarType_v2> lstCars = new HashSet<TrainCarType_v2>();
            foreach (TrainCar trainCar in this.TrainCars)
            {
                lstCars.Add(trainCar.carType.ToV2().parentType);
            }
            List<StationController> stationController = SingletonBehaviour<LogicController>.Instance.GetStationsThatUseCarTypes(lstCars, null);
            foreach(StationController station in stationController)
            {
                if((this.origin != this.destination) && this.destination == station)
                {
                    return true;
                }
            }
            return false;
            /*foreach (CargoType cargo in Enum.GetValues(typeof(CargoType)))
            {
                foreach(TrainCar trainCar in this.TrainCars)
                {
                    if(cargo.ToV2().IsLoadableOnCarType(trainCar.carType.ToV2().parentType))
                    {
                        cargoTypes.Add(cargo);
                    }
                }
            }
            if (cargoTypes.Count == 0)
            {
                return false;
            }
           List<StationController> StationAcceptingCar = LogicController.Instance.GetStationsThatUseCarTypes(lstCars, null);
            foreach(StationController controller in StationAcceptingCar)
            {
                if(controller == this.destination)
                {
                    return true;
                }
            }
            return false;*/
        }
    }
}
