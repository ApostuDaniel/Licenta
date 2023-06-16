using Assets.Scripts.Food;
using CsvHelper;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Assets.Scripts.Neat
{
    public class NeatManager : MonoBehaviour
    {
        public GameObject creaturePrefab;

        private GameObject[] allCreatures;
        private NeatNetwork[] allNetworks;
        private NeatGenome[] allGenomes;
        private List<Species> species;

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
        public int topMemberSelectionCutoff = 5;

        [Header("Energy Parameters")]
        public float startingEnergy = 15f;

        public float energyForFood = 4f;

        [SerializeField] private int currentGeneration = 1;

        private InovationManger inovationManger;

        public int startingPopulation;

        public int currentPopulation;

        private string logFileName;

        private static int speciesId;

        private void Start()
        {
            allCreatures = new GameObject[startingPopulation];
            allNetworks = new NeatNetwork[startingPopulation];
            allGenomes = new NeatGenome[startingPopulation];
            species = new List<Species>();

            //For each genome, at initialization we make a connection between each input and each output,
            //as such there are inputNodes * outputNodes connections at the start, starting from 0, so the next innovation number will be the connection count
            inovationManger = new InovationManger(inputNodes * outputNodes);

            logFileName = $"Logs\\Fitness\\fitness_log_{DateTime.Now.Ticks}.csv";
            using (var fs = File.Create(logFileName)) { }
            using var writer = new StreamWriter(logFileName);
            using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
            csv.WriteHeader<FitnessLog>();
            csv.NextRecord();

            InitializeGenomes();
            GenerateNetworks();
            GenerateCreatures();
        }

        private void FixedUpdate()
        {
            if (currentPopulation <= 0)
            {
                RespawnFood();
                LogFitnessInfo();
                ProduceNextGeneration();
            }
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
                var distanceFromSpecies = potentialSpecies == null ? 100f : potentialSpecies.SpeciesRepresentative.ComputeDistance(genome, excessCoef, disjointCoef, weightDiffCoef);
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

                //If a species has more organisms than topMemberSelectionCutoff keep the top member unchnaged in the next gen
                if (s.MembersIdx.Count > topMemberSelectionCutoff)
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
            var totalAverageAdjustedFitness = species.Sum(s => s.MembersIdx.Sum(m => allNetworks[m].fitness));

            var nonEmptySpecies = species.Where(s => s.MembersIdx.Count > 0).ToList();
            foreach (var s in nonEmptySpecies)
            {
                var speciesTotalAdjustedFitness = s.MembersIdx.Select(m => allNetworks[m].fitness).Sum();
                s.PotentialMembersCount = (int)Math.Floor((speciesTotalAdjustedFitness / totalAverageAdjustedFitness) * startingPopulation);
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
            foreach (int memeber in s.MembersIdx)
            {
                allNetworks[memeber].fitness /= s.MembersIdx.Count;
            }
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
            NeatNetwork bestNetwork = allNetworks.First(n => n.fitness >= bestFitness);
            float averageFitness = allNetworks.Sum(x => x.fitness) / allNetworks.Length;
            int bestHiddenNodes = bestNetwork.neatGenome.nodeGenes.Count - (bestNetwork.neatGenome.InNodes + bestNetwork.neatGenome.OutNodes);
            int bestConnectionGenes = bestNetwork.neatGenome.connectionGenes.Count;

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

        private void MutateGeneration()
        {
            foreach (var genome in allGenomes)
            {
                genome.Mutate(inovationManger, mutationProb, weightPerturbanceProb, newNodeProb, newConnectionProb, perturbanceRange, reassignmentRange);
            }
        }

        //private void SelectGeneration()
        //{
        //    var maxEval = allNetworks[networkRankings[0]].fitness;

        //    //var fitneses = allNetworks.Select(n => (maxEval / (maxEval - n.fitness + 0.01f)) + 0.01f).ToArray();
        //    //var fitneses = allNetworks.Select(n => Mathf.Pow((n.fitness / maxEval) + float.Epsilon + 1, 1f)).ToArray();

        //    //Rank roullete
        //    var fitneses = new float[startingPopulation];
        //    for(int i = 0; i < startingPopulation; i++)
        //    {
        //        fitneses[networkRankings[i]] = startingPopulation - i;
        //    }

        //    float fSum = fitneses.Sum();

        //    var normalizedFitneses = fitneses.Select(f => f / fSum).ToArray();

        //    float[] selectionProb = new float[normalizedFitneses.Length];
        //    selectionProb[0] = 0;

        //    for (int i = 1; i < normalizedFitneses.Length; i++)
        //    {
        //        selectionProb[i] = selectionProb[i - 1] + normalizedFitneses[i];
        //    }

        //    selectionProb[^1] = 1;

        //    NeatGenome[] nextGenomes = new NeatGenome[startingPopulation];
        //    int nrElites = (int)(startingPopulation * elitismFactor);
        //    NeatGenome[] elites = networkRankings.Take(nrElites).Select(i => allGenomes[i]).ToArray();

        //    for (int i = 0; i < nrElites; i++) nextGenomes[i] = elites[i];

        //    for (int i = nrElites; i < startingPopulation; i++)
        //    {
        //        double r = UnityEngine.Random.value;
        //        bool chosen = false;

        //        for (int j = 0; j < selectionProb.Length; j++)
        //        {
        //            if (r <= selectionProb[j])
        //            {
        //                nextGenomes[i] = allGenomes[j];
        //                chosen = true;
        //                break;
        //            }
        //        }
        //        if (!chosen) nextGenomes[i] = (allGenomes[^1]);
        //    }

        //    allGenomes = nextGenomes;
        //}

        //private void RankNetworks()
        //{
        //    networkRankings = allNetworks.OrderByDescending(x => x.fitness).Select(x => x.index).ToArray();
        //}

        private void GenerateCreatures()
        {
            foreach (var s in species)
            {
                foreach (int member in s.MembersIdx)
                {
                    var pos = new Vector2(UnityEngine.Random.value, UnityEngine.Random.value);
                    pos = Camera.main.ViewportToWorldPoint(pos);

                    allCreatures[member] = Instantiate(creaturePrefab, pos, transform.rotation);
                    var creatureController = allCreatures[member].gameObject.GetComponent<PreyController>();
                    currentPopulation++;

                    creatureController.network = allNetworks[member];
                    creatureController.inputNodes = inputNodes;
                    creatureController.outputNodes = outputNodes;
                    creatureController.totalEnergy = startingEnergy;
                    creatureController.energyForFood = energyForFood;
                    (var r, var g, var b) = s.RgbColor;
                    creatureController.spriteColor = new Color32(r, g, b, 255);
                }
            }
        }

        private void GenerateNetworks()
        {
            for (int i = 0; i < startingPopulation; i++)
            {
                allNetworks[i] = new NeatNetwork(allGenomes[i])
                {
                    index = i
                };
            }
        }

        private void InitializeGenomes()
        {
            species.Add(new Species(speciesId++));
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

        public void RegisterDeath()
        {
            currentPopulation--;
        }
    }
}