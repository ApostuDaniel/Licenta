using Assets.Scripts.Food;
using CsvHelper;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using UnityEditor;
using UnityEngine;

namespace Assets.Scripts.Neat
{
    public class NeatManager : MonoBehaviour
    {
        public GameObject creaturePrefab;
        public GameObject managerOversier;
        private GameObject[] allCreatures;
        private NeatNetwork[] allNetworks;
        private NeatGenome[] allGenomes;
        private List<Species> species;

        //private int synchronizationId;

        [Header("NEAT parameters")]
        public int inputNodes, outputNodes;

        public float initWeightRange = 1f;
        //public float elitismFactor = 0.05f;
        public float mutationProb = 0.8f;
        public float weightPerturbanceProb = 0.9f;
        public float newNodeProb = 0.05f;
        public float newConnectionProb = 0.07f;
        public float perturbanceRange = 0.1f;
        public float reassignmentRange = 1f;
        public float inheritDisabledGeneProb = 0.75f;
        public float chanceMutationNoCrossover = 0.25f;
        public float interSpeciesMatingRate = 0.001f;
        public int speciesDropoffAge = 40;
        public float survivalTreshold = 0.3f;
        public float minCompatibilityTreshold = 1.0f;
        public float compatibilityTreshold = 1.0f;
        public float maxCompatibilityTreshold = 5.0f;
        public float compatibilityModif = 0.2f;
        public int speciesTargetCount = 4;
        public float excessCoef = 1.0f;
        public float disjointCoef = 1.0f;
        public float weightDiffCoef = 0.4f;
        public int topMembersSelectionCutoff = 5;

        [Header("Energy Parameters")]
        public float startingEnergy = 15f;
        public float energyForFood = 4f;

        private InovationManger inovationManger;

        [Header("Population parameters")]
        public bool isPreyPopulation;
        public int startingPopulation;
        public int currentPopulation;
        public float timePerGeneration;
        [SerializeField] private float timeElapsed;
        [SerializeField] private int currentGeneration = 1;
        [SerializeField] private int numberOfSpecies = 1;

        [Header("Logging")]
        private string logFileName;
        public bool isLoggindEnabled = true;

        private int speciesId;


        private void Start()
        {
            allCreatures = new GameObject[startingPopulation];
            allNetworks = new NeatNetwork[startingPopulation];
            allGenomes = new NeatGenome[startingPopulation];
            species = new List<Species>();
            //synchronizationId = ManagerSynchronizer.AddManagerToSynchronizer();

            //For each genome, at initialization we make a connection between each input and each output,
            //as such there are inputNodes * outputNodes connections at the start, starting from 0, so the next innovation number will be the connection count
            inovationManger = new InovationManger(inputNodes * outputNodes);

            if(isLoggindEnabled)
            {
                string popType = isPreyPopulation ? "prey" : "predator";
                logFileName = $"Logs\\Fitness\\fitness_log_{popType}_{DateTime.Now.Ticks}.csv";
                using (var fs = File.Create(logFileName)) { }
                using var writer = new StreamWriter(logFileName);
                using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
                csv.WriteHeader<FitnessLog>();
                csv.NextRecord();
            }

            InitializeGenomes();
            GenerateNetworks();
            GenerateCreatures();
        }

        private void FixedUpdate()
        {
            var managerController = managerOversier.GetComponent<ManagerOversier>();
            timeElapsed += Time.deltaTime;
            if (currentPopulation <= 0 || timeElapsed > timePerGeneration)
            {
                managerController.StopManager(this.gameObject);
                if (!managerController.AllManagersStoped())
                {
                    return;
                }

                if (timeElapsed > timePerGeneration)
                {
                    int nrOfSurvivors = CleanUpAliveCreatures();
                    string popType = isPreyPopulation ? "prey" : "predators";
                    Debug.Log($"In generatia {currentGeneration} au supravetuit pana la final {nrOfSurvivors} {popType}");
                }

                RespawnFood();
                if(isLoggindEnabled) {
                    LogFitnessInfo();
                }
                
                ProduceNextGeneration();
                timeElapsed = 0;
                numberOfSpecies = species.Count;
            }
            else
            {
                managerController.StartManager(this.gameObject);
            }
        }

        private int CleanUpAliveCreatures()
        {
            int survivors = 0;
            foreach(var creature in allCreatures)
            {
                if(creature == null) continue;
                if (isPreyPopulation)
                {
                    creature.GetComponent<PreyController>().Die(CreatureController.DeathCause.TimeEnd);
                }
                else
                {
                    creature.GetComponent<PredatorController>().Die(CreatureController.DeathCause.TimeEnd);
                }
                survivors++;
            }

            return survivors;
        }

        private void RespawnFood()
        {
            GameObject.FindObjectOfType<FoodManager>().DestroyFood();
            GameObject.FindObjectOfType<FoodManager>().GenerateFood();
        }

        private void ProduceNextGeneration()
        {
            inovationManger.ResetForNextGeneration();
            AdjustCompatibiltyTreshold();
            ComputeAllSpeciesAdjustedFitness();
            AdjustAllSpeciesPotentialMemeberCount();
            GenerateNextGenGenomes();
            SpeciesRepartition();
            GenerateNetworks();
            GenerateCreatures();
            currentGeneration++;
        }

        private void AdjustCompatibiltyTreshold()
        {
            if (currentGeneration > 1)
            {
                if (species.Count < speciesTargetCount && compatibilityTreshold > minCompatibilityTreshold) compatibilityTreshold -= compatibilityModif;
                else if (species.Count > speciesTargetCount && compatibilityTreshold < maxCompatibilityTreshold) compatibilityTreshold += compatibilityModif;

                if (compatibilityTreshold < minCompatibilityTreshold) compatibilityTreshold = minCompatibilityTreshold;
                if (compatibilityTreshold > maxCompatibilityTreshold) compatibilityTreshold = maxCompatibilityTreshold;
            }
        }

        private void SpeciesRepartition()
        {
            for (int i = 0; i < allGenomes.Length; i++)
            {
                var genome = allGenomes[i];
                var potentialSpecies = species.Find(s => s.Id == genome.speciesHint);
                var distanceFromSpecies = potentialSpecies == null ? 1000f : potentialSpecies.SpeciesRepresentative.ComputeDistance(genome, excessCoef, disjointCoef, weightDiffCoef);
                if (potentialSpecies != null && distanceFromSpecies < compatibilityTreshold)
                {
                    potentialSpecies.MembersIdx.Add(i);
                }
                else
                {
                    bool speciesFound = false;
                    foreach (var s in species)
                    {
                        distanceFromSpecies = s.SpeciesRepresentative.ComputeDistance(genome, excessCoef, disjointCoef, weightDiffCoef);
                        if (distanceFromSpecies < compatibilityTreshold)
                        {
                            speciesFound = true;
                            s.MembersIdx.Add(i);
                            break;
                        }
                    }

                    if (!speciesFound) CreateNewSpecies(i);
                }
            }

            foreach(var s in species)
            {
                if(s.MembersIdx.Count > 0) s.GenerationsSinceMemebersAssigned = 0;
                else s.GenerationsSinceMemebersAssigned++;
            }

            RemoveRedundantSpecies();
        }

        private void RemoveRedundantSpecies()
        {
            int deadline = 3;
            for (int i = 0; i < species.Count; i++)
            {
                if (species[i].GenerationsSinceMemebersAssigned >= deadline)
                {
                    Debug.Log($"<color=#{species[i].GetStringColor()}>Species numbered {species[i].Id} dropped, no members assigned to the species for {deadline} generations: best fitness = {species[i].LastBestFitness}</color>");
                    species.RemoveAt(i);
                    i--;
                }
            }
        }

        private void CreateNewSpecies(int genomeIdx)
        {
            var s = new Species(speciesId++);
            if (!isPreyPopulation)
            {
                //so we can differentiate predator populations also by color, not just by shape
                (var r, var g, var b) = species[0].RgbColor;
                species[0].RgbColor = (b, r, g);
            }
            s.MembersIdx.Add(genomeIdx);
            s.SpeciesRepresentative = allGenomes[genomeIdx];
            species.Add(s);
            Debug.Log($"<color=#{s.GetStringColor()}>New Species created with id {s.Id}</color>");
        }

        private void GenerateNextGenGenomes()
        {
            var newGenomes = new List<NeatGenome>(startingPopulation);
            foreach (var s in species)
            {
                if (s.MembersIdx.Count == 0) continue;
                //Choose a a random representative organism for the species
                (s.SpeciesRepresentative, _) = ChooseRandOrganism(s);

                //If a species has more organisms than topMembersSelectionCutoff keep the top member unchnaged in the next gen
                if (s.MembersIdx.Count > topMembersSelectionCutoff)
                {
                    newGenomes.Add(allGenomes[s.MembersIdx[0]]);
                    allGenomes[s.MembersIdx[0]].speciesHint = s.Id;
                    s.PotentialMembersCount--;
                }

                while (s.PotentialMembersCount > 0)
                {
                    NeatGenome child;
                    var (parent1, p1Fitness) = ChooseRandOrganism(s);
                    if (UnityEngine.Random.value <= chanceMutationNoCrossover)
                    {
                        child = new NeatGenome(parent1)
                        {
                            speciesHint = s.Id
                        };
                    }
                    else
                    {
                        NeatGenome parent2; float p2Fitness;
                        if (UnityEngine.Random.value <= interSpeciesMatingRate)
                        {
                            //Choose a random organism from a random species
                            Species choosenSpecies;
                            do
                            {
                                choosenSpecies = species[UnityEngine.Random.Range(0, species.Count)];
                            } while (choosenSpecies.MembersIdx.Count == 0);
                            (parent2, p2Fitness) = ChooseRandOrganism(choosenSpecies);
                        }
                        else
                        {
                            (parent2, p2Fitness) = ChooseRandOrganism(s);
                        }

                        child = parent1.CrossOver(parent2, p1Fitness, p2Fitness, inheritDisabledGeneProb);
                    }

                    child.Mutate(inovationManger, mutationProb, weightPerturbanceProb, newNodeProb, newConnectionProb, perturbanceRange, reassignmentRange);
                    newGenomes.Add(child);
                    s.PotentialMembersCount--;
                }
            }

            foreach (var s in species)
            {
                s.MembersIdx.Clear();
            }
            allGenomes = newGenomes.ToArray();
        }

        private (NeatGenome genome, float fitness) ChooseRandOrganism(Species s)
        {
            var randIdx = s.MembersIdx[UnityEngine.Random.Range(0, s.MembersIdx.Count)];
            return (allGenomes[randIdx], allNetworks[randIdx].fitness);
        }

        private void AdjustAllSpeciesPotentialMemeberCount()
        {
            var totalPotentialPopulation = 0;
            var totalAverageAdjustedFitness = species.Sum(s => s.SpeciesAdjustedFitness);

            var nonEmptySpecies = species.Where(s => s.MembersIdx.Count > 0).ToList();
            foreach (var s in nonEmptySpecies)
            {
                s.PotentialMembersCount = (int)Math.Floor((s.SpeciesAdjustedFitness / totalAverageAdjustedFitness) * startingPopulation);
                totalPotentialPopulation += s.PotentialMembersCount;

                KeepSpeciesTopMemebers(s);
            }

            //To make sure that we keep the same number of individuals in the population
            totalPotentialPopulation -= nonEmptySpecies[^1].PotentialMembersCount;
            nonEmptySpecies[^1].PotentialMembersCount = startingPopulation - totalPotentialPopulation;
        }

        private void KeepSpeciesTopMemebers(Species s)
        {
            s.MembersIdx = s.MembersIdx.GetRange(0, (int)Math.Ceiling(s.MembersIdx.Count * survivalTreshold));
        }

        private void ComputeAllSpeciesAdjustedFitness()
        {
            for (int i = 0; i < species.Count; i++)
            {
                var s = species[i];
                SortSpeciesMembers(s);

                //Adjust the last best fitness of the current species and remove it
                //if no improvement found over the last few generations
                if (s.MembersIdx.Count == 0 || allNetworks[s.MembersIdx[0]].fitness <= s.LastBestFitness)
                {
                    s.GenerationsSinceImprovement++;
                    if (s.GenerationsSinceImprovement >= speciesDropoffAge && species.Count > 1)
                    {
                        Debug.Log($"<color=#{s.GetStringColor()}>Species numbered {s.Id} dropped, no improvement for {s.GenerationsSinceImprovement} generations: best fitness = {s.LastBestFitness}</color>");
                        species.RemoveAt(i);
                        i--;
                        continue;
                    }
                }
                else
                {
                    s.LastBestFitness = allNetworks[s.MembersIdx[0]].fitness;
                    s.GenerationsSinceImprovement = 0;
                }

                ComputeSpeciesAdjustedFitness(s);
            }
        }

        private void ComputeSpeciesAdjustedFitness(Species s)
        {
            s.SpeciesAdjustedFitness = 0;
            foreach (int memeber in s.MembersIdx)
            {
                s.SpeciesAdjustedFitness += allNetworks[memeber].fitness;
            }
            s.SpeciesAdjustedFitness /= s.MembersIdx.Count == 0 ? 1 : s.MembersIdx.Count;
        }

        private void SortSpeciesMembers(Species s)
        {
            if (s.MembersIdx.Count > 0)
            {
                s.MembersIdx = s.MembersIdx
                .Select(idx => allNetworks[idx])
                .OrderByDescending(network => network.fitness).Select(network => network.index).ToList();
            }
        }

        private void LogFitnessInfo()
        {
            float bestFitness = allNetworks.Max(n => n.fitness);
            int bestIndex = Array.FindIndex(allNetworks, network => network.fitness == bestFitness);
            
            float averageFitness = allNetworks.Sum(x => x.fitness) / allNetworks.Length;
            int bestHiddenNodes = allGenomes[bestIndex].nodeGenes.Count - (allGenomes[bestIndex].InNodes + allGenomes[bestIndex].OutNodes);
            int bestConnectionGenes = allGenomes[bestIndex].connectionGenes.Count;

            using var writer = new StreamWriter(logFileName, true);
            using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
            csv.WriteRecord<FitnessLog>(new FitnessLog
            {
                Generation = currentGeneration,
                AverageFitness = averageFitness,
                BestFitness = bestFitness,
                BestNrConnections = bestConnectionGenes,
                HiddenNodes = bestHiddenNodes
            });
            csv.NextRecord();
        }

        private void GenerateCreatures()
        {
            var foodMask = LayerMask.GetMask("Food");
            var preyMask = LayerMask.GetMask("Prey");
            var predatorMask = LayerMask.GetMask("Predator");
            foreach (var s in species)
            {
                foreach (int member in s.MembersIdx)
                {
                    var pos = new Vector2(UnityEngine.Random.value, UnityEngine.Random.value);
                    pos = Camera.main.ViewportToWorldPoint(pos);

                    allCreatures[member] = Instantiate(creaturePrefab, pos, transform.rotation);
                    if (isPreyPopulation)
                    {
                        var creatureController = allCreatures[member].gameObject.GetComponent<PreyController>();
                        creatureController.network = allNetworks[member];
                        creatureController.inputNodes = inputNodes;
                        creatureController.outputNodes = outputNodes;
                        creatureController.totalEnergy = startingEnergy;
                        creatureController.energyForFood = energyForFood;
                        creatureController.energyOffering = (preyController) => 0f;
                        creatureController.manager = this;
                        creatureController.predatorMask = predatorMask;

                        creatureController.fowTargetsMask = foodMask;
                        creatureController.wallRayDistance = 20f;
                        creatureController.viewDistance = 20f;
                        creatureController.viewAngle = 90f;
                        creatureController.angleSplits = 3;
                        (var r, var g, var b) = s.RgbColor;
                        creatureController.spriteColor = new Color32(r, g, b, 255);
                    }
                    else
                    {
                        var creatureController = allCreatures[member].gameObject.GetComponent<PredatorController>();
                        creatureController.network = allNetworks[member];
                        creatureController.inputNodes = inputNodes;
                        creatureController.outputNodes = outputNodes;
                        creatureController.totalEnergy = startingEnergy;
                        creatureController.energyForFood = energyForFood;
                        creatureController.manager = this;

                        creatureController.fowTargetsMask = preyMask;
                        creatureController.wallRayDistance = 20f;
                        creatureController.viewDistance = 20f;
                        creatureController.viewAngle = 90f;
                        creatureController.angleSplits = 3;
                        (var r, var g, var b) = s.RgbColor;
                        creatureController.spriteColor = new Color32(r, g, b, 255);
                    }
                    currentPopulation++;
                }
            }
        }

        private void GenerateNetworks()
        {
            for (int i = 0; i < startingPopulation; i++)
            {
                var genome = allGenomes[i];
                allNetworks[i] = new NeatNetwork(genome)
                {
                    index = i
                };
            }
        }

        private void InitializeGenomes()
        {
            species.Add(new Species(speciesId++));
            if (!isPreyPopulation)
            {
                //so we can differentiate predator populations also by color, not just by shape
                (var r, var g, var b) = species[0].RgbColor;
                species[0].RgbColor = (b, r, g);
            }
            Debug.Log($"<color=#{species[0].GetStringColor()}>New Species created with id {species[0].Id}</color>");
            for (int i = 0; i < startingPopulation; i++)
            {
                allGenomes[i] = new NeatGenome(inputNodes, outputNodes, initWeightRange)
                {
                    speciesHint = species[0].Id
                };
                species[0].MembersIdx.Add(i);
            }
        }

        public void RegisterDeath(CreatureController.DeathCause deathCause)
        {
            currentPopulation--;
        }
    }
}