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
        private int[] networkRankings;

        [Header("NEAT parameters")]
        public int inputNodes, outputNodes;
        
        public float initWeightRange = 1f;
        public float elitismFactor = 0.05f;
        public float mutationProb = 0.8f;
        public float weightPerturbanceProb = 0.9f;
        public float newNodeProb = 0.03f;
        public float newConnectionProb = 0.05f;
        public float perturbanceRange = 0.1f;
        public float reassignmentRange = 1f;
        public float inheritDisabledGeneProb = 0.75f;
        public float chanceMutationNoCrossover = 0.25f;
        public float interSpeciesMatingRate = 0.001f;
        public int speciesDropoffAge = 15;
        public float survivalTreshold = 0.2f;
        public float compatibilityTreshold = 3.0f;
        public float compatibilityModif = 3.0f;
        public int speciesTargetCount = 10;
        public float excessCoef = 1.0f;
        public float disjointCoef = 1.0f;
        public float weightDiffCoef = 0.4f;

        [Header("Energy Parameters")]
        public float startingEnergy = 20f;

        public float energyForFood = 5f;

        [SerializeField] private int currentGeneration = 1;

        private InovationManger inovationManger;

        public int startingPopulation;

        public int currentPopulation;

        private string logFileName;

        private void Start()
        {
            allCreatures = new GameObject[startingPopulation];
            allNetworks = new NeatNetwork[startingPopulation];
            allGenomes = new NeatGenome[startingPopulation];
            networkRankings = new int[startingPopulation];

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
            RankNetworks();
            LogFitnessInfo();
            SelectGeneration();
            MutateGeneration();
            GenerateNetworks();
            GenerateCreatures();
            currentGeneration++;
        }

        private void LogFitnessInfo()
        {
            NeatNetwork bestNetwork = allNetworks[networkRankings[0]];
            float bestFitness = bestNetwork.fitness;
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

        private void SelectGeneration()
        {
            var maxEval = allNetworks[networkRankings[0]].fitness;

            //var fitneses = allNetworks.Select(n => (maxEval / (maxEval - n.fitness + 0.01f)) + 0.01f).ToArray();
            //var fitneses = allNetworks.Select(n => Mathf.Pow((n.fitness / maxEval) + float.Epsilon + 1, 1f)).ToArray();

            //Rank roullete
            var fitneses = new float[startingPopulation];
            for(int i = 0; i < startingPopulation; i++)
            {
                
                fitneses[networkRankings[i]] = startingPopulation - i;
            }

            float fSum = fitneses.Sum();

            var normalizedFitneses = fitneses.Select(f => f / fSum).ToArray();

            float[] selectionProb = new float[normalizedFitneses.Length];
            selectionProb[0] = 0;

            for (int i = 1; i < normalizedFitneses.Length; i++)
            {
                selectionProb[i] = selectionProb[i - 1] + normalizedFitneses[i];
            }

            selectionProb[^1] = 1;

            NeatGenome[] nextGenomes = new NeatGenome[startingPopulation];
            int nrElites = (int)(startingPopulation * elitismFactor);
            NeatGenome[] elites = networkRankings.Take(nrElites).Select(i => allGenomes[i]).ToArray();

            for (int i = 0; i < nrElites; i++) nextGenomes[i] = elites[i];

            for (int i = nrElites; i < startingPopulation; i++)
            {
                double r = UnityEngine.Random.value;
                bool chosen = false;

                for (int j = 0; j < selectionProb.Length; j++)
                {
                    if (r <= selectionProb[j])
                    {
                        nextGenomes[i] = allGenomes[j];
                        chosen = true;
                        break;
                    }
                }
                if (!chosen) nextGenomes[i] = (allGenomes[^1]);
            }

            allGenomes = nextGenomes;
        }

        private void RankNetworks()
        {
            networkRankings = allNetworks.OrderByDescending(x => x.fitness).Select(x => x.index).ToArray();
        }

        private void GenerateCreatures()
        {
            for (int i = 0; i < startingPopulation; i++)
            {
                var pos = new Vector2(UnityEngine.Random.value, UnityEngine.Random.value);
                pos = Camera.main.ViewportToWorldPoint(pos);

                allCreatures[i] = Instantiate(creaturePrefab, pos, transform.rotation);
                var creatureController = allCreatures[i].gameObject.GetComponent<PreyController>();
                currentPopulation++;

                creatureController.network = allNetworks[i];
                creatureController.inputNodes = inputNodes;
                creatureController.outputNodes = outputNodes;
                creatureController.totalEnergy = startingEnergy;
                creatureController.energyForFood = energyForFood;
            }
        }

        private void GenerateNetworks()
        {
            for (int i = 0; i < startingPopulation; i++)
            {
                networkRankings[i] = i;
                allNetworks[i] = new NeatNetwork(allGenomes[i])
                {
                    index = i
                };
            }
        }

        private void InitializeGenomes()
        {
            for (int i = 0; i < startingPopulation; i++)
            {
                allGenomes[i] = new NeatGenome(inputNodes, outputNodes, initWeightRange);
            }
        }

        public void RegisterDeath()
        {
            currentPopulation--;
        }
    }
}