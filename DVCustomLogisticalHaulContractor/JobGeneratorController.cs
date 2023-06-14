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
using UnityEditor.VersionControl;

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
            JobsGenerator.CreateEmptyHaulJob(this.origin.logicStation, this.chainData, this.cars, this.departureTrack, this.ArrivalTrack, getTimeLinit(), this.getJobPayment(), null,this.getRequiredLicences());
            return "Job Created";
        }
        private float getJobPayment()
        {
            
            List<TrainCarType> lstTrainCarType = new List<TrainCarType>();
            Dictionary<TrainCarType, int> dictionary = new Dictionary<TrainCarType, int>();
            foreach (TrainCar TrainCar in this.TrainCars)
            {
                lstTrainCarType.Add(TrainCar.logicCar.carType);
            }
            foreach (TrainCarType trainCarType in lstTrainCarType)
            {
                if (!dictionary.ContainsKey(trainCarType))
                {
                    dictionary[trainCarType] = 0;
                }
                Dictionary<TrainCarType, int> dictionary2 = dictionary;
                TrainCarType key = trainCarType;
                int num = dictionary2[key];
                dictionary2[key] = num + 1;
            }
            Dictionary<CargoType, int> cargoData = new Dictionary<CargoType, int>();
            PaymentCalculationData pcd = new PaymentCalculationData(dictionary,cargoData);
            return JobPaymentCalculator.CalculateJobPayment(JobType.EmptyHaul, JobPaymentCalculator.GetDistanceBetweenStations(this.origin, this.destination),pcd);
        }
        private float getTimeLinit()
        {
            float bonusTimeLimit = 0f;
            float distanceBetweenStations = JobPaymentCalculator.GetDistanceBetweenStations(this.origin, this.destination);
            bonusTimeLimit = JobPaymentCalculator.CalculateHaulBonusTimeLimit(distanceBetweenStations, false);
            return bonusTimeLimit;
        }
        private JobLicenses getRequiredLicences()
        {
            return LicenseManager.GetRequiredLicensesForJobType(JobType.EmptyHaul) | LicenseManager.GetRequiredLicenseForNumberOfTransportedCars(this.TrainCars.Count);
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
            if(yto != null)
            {
                DVCustomLogisticalHaulContractor.Log("YTO FOUND !");
            }
            List<Track> lstTracks = yto.FilterOutTracksWithoutRequiredFreeSpace(this.destination.logicStation.yard.StorageTracks,yto.GetSeparationLengthBetweenCars(this.cars.Count));
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
            HashSet<CargoContainerType> containerTypes = new HashSet<CargoContainerType>();
            List<StationController> stationController = new List<StationController>();
            foreach(TrainCar trainCar in this.TrainCars)
            {
                containerTypes.Add(CargoTypes.CarTypeToContainerType[trainCar.carType]);
            }
            if (containerTypes.Count == 0)
            {
                return false;
            }
            foreach (KeyValuePair<StationController, HashSet<CargoContainerType>> keyValuePair in SingletonBehaviour<LogicController>.Instance.stationToSupportedContainerTypes)
            {
                if (!(keyValuePair.Key == this.origin) && keyValuePair.Value.IsSupersetOf(containerTypes))
                {
                    stationController.Add(keyValuePair.Key);
                }
            }
            foreach(StationController controller in stationController)
            {
                if(controller == this.destination)
                {
                    return true;
                }
            }
            return false;
        }
    }
}