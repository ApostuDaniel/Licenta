using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Unity.VisualScripting;
using UnityEngine;

namespace Assets.Scripts.Neat
{
    public class NeatGenome
    {
        public enum TYPE { Input,  Output, Hidden }

        public List<NodeGene> nodeGenes;
        public List<ConnectionGene> connectionGenes;
        public int speciesHint;

        public int InNodes { get; }
        public int OutNodes { get; }

        //public NeatGenome(List<NodeGene> nodeGenes, List<ConnectionGene> connectionGenes)
        //{
        //    this.nodeGenes = nodeGenes; this.connectionGenes = connectionGenes;

        //    foreach (NodeGene nodeGene in nodeGenes)
        //    {

        //        switch (nodeGene.type)
        //        {
        //            case TYPE.Input: InNodes++; break;
        //            case TYPE.Output: OutNodes++; break;
        //            default: break;
        //        }
        //    }
        //}

        public NeatGenome(int inputNodes, int outputNodes)
        {
            InNodes = inputNodes;
            OutNodes = outputNodes;
            nodeGenes = new List<NodeGene>(inputNodes + outputNodes);
            connectionGenes = new List<ConnectionGene>(inputNodes * outputNodes);
        }

        public NeatGenome(NeatGenome genome) : this(genome.InNodes, genome.OutNodes)
        {
            speciesHint = genome.speciesHint;

            CopyNodes(genome);

            foreach(var conGene in genome.connectionGenes)
            {
                var inNode = nodeGenes[conGene.inNode.id];
                var outNode = nodeGenes[conGene.outNode.id];
                connectionGenes.Add(new ConnectionGene(inNode, outNode, conGene.weight, conGene.isEnabled, conGene.inovationNr));
            }
        }

        public NeatGenome(int inputNodes,  int outputNodes, float weightRange = 1f) : this(inputNodes, outputNodes)
        {
            InitiateRandomWeightsGenome(weightRange);
        }

        public void Mutate(InovationManger inovManager, float mutationProb, float weightPerturbanceProb, float newNodeProb, float newConnectionProb, float perturbanceRange, float reassignmentRange)
        {

            var shouldMutate = Random.value;
            var shouldAddNode = Random.value;
            var shouldAddConnection = Random.value;

            if (shouldMutate <= mutationProb)
            {
                foreach(var gene in connectionGenes)
                {
                    var shouldPerturb = Random.value;

                    if (shouldPerturb <= weightPerturbanceProb)
                    {
                        gene.weight += Random.Range(-perturbanceRange, perturbanceRange);
                    }
                    else
                    {
                        gene.weight = Random.Range(-reassignmentRange, reassignmentRange);
                    }
                }
            }

            if(shouldAddNode <= newNodeProb)
            {
                InnovateNode(inovManager);
            }
            if(shouldAddConnection <= newConnectionProb)
            {
                InnovateConnection(inovManager, reassignmentRange);
            }
        }

        public NeatGenome CrossOver(NeatGenome parent2, float p1Fitness, float p2Fitness, float inheritDisabledGeneProb)
        {
            var child = new NeatGenome(this.InNodes, this.OutNodes);
            if (p1Fitness > p2Fitness || (p1Fitness == p2Fitness && this.nodeGenes.Count >= parent2.nodeGenes.Count)) child.CopyNodes(this);
            else child.CopyNodes(parent2);

            int idx1 = 0, idx2 = 0;
            while (idx1 < this.connectionGenes.Count && idx2 < parent2.connectionGenes.Count)
            {
                var p1Gene = this.connectionGenes[idx1];
                var p2Gene = parent2.connectionGenes[idx2];

                if (p1Gene.inovationNr == p2Gene.inovationNr)
                {
                    child.AddMatchingGene(p1Gene, p2Gene, inheritDisabledGeneProb);
                    idx1++;
                    idx2++;
                }
                else if(p1Gene.inovationNr < p2Gene.inovationNr)
                {
                    if(p1Fitness >= p2Fitness)
                    {
                        child.AddCopyOfGene(p1Gene);
                    }
                    idx1++;
                }
                else
                {
                    if (p2Fitness > p1Fitness)
                    {
                        child.AddCopyOfGene(p2Gene);
                    }
                    idx2++;
                }
            }

            if(idx1 < this.connectionGenes.Count && p1Fitness >= p2Fitness) {
                for ( int i = idx1; i < this.connectionGenes.Count; i++)
                {
                    child.AddCopyOfGene(this.connectionGenes[i]);
                }
            }
            else if(idx2 < parent2.connectionGenes.Count && p2Fitness > p1Fitness)
            {
                for (int i = idx2; i < parent2.connectionGenes.Count; i++)
                {
                    child.AddCopyOfGene(parent2.connectionGenes[i]);
                }
            }

            return child;
        }

        private void AddMatchingGene(ConnectionGene p1Gene, ConnectionGene p2Gene, float inheritDisabledGeneProb)
        {
            var isEnabled = (p1Gene.isEnabled && p2Gene.isEnabled) || Random.value > inheritDisabledGeneProb;

            var choiceGene = Random.value <= 0.5f ? p1Gene : p2Gene;

            this.AddCopyOfGene(choiceGene);
            this.connectionGenes[^1].isEnabled = isEnabled;
        }

        public float ComputeDistance(NeatGenome genome, float excessCoef, float disjointCoef, float weightDiffCoef)
        {
            int matchingCount = 0, disjointCount = 0, excessCount = 0, maxGeneCount;
            float totaWeightDifference = 0;

            int idx1 = 0, idx2 = 0;
            while (idx1 < this.connectionGenes.Count && idx2 < genome.connectionGenes.Count)
            {
                var p1Gene = this.connectionGenes[idx1];
                var p2Gene = genome.connectionGenes[idx2];

                if (p1Gene.inovationNr == p2Gene.inovationNr)
                {
                    totaWeightDifference += Mathf.Abs(p1Gene.weight - p2Gene.weight);
                    matchingCount++;
                    idx1++;
                    idx2++;
                }
                else if (p1Gene.inovationNr < p2Gene.inovationNr)
                {
                    disjointCount++;
                    idx1++;
                }
                else
                {
                    disjointCount++;
                    idx2++;
                }
            }

            if (idx1 < this.connectionGenes.Count)
            {
                excessCount = this.connectionGenes.Count - idx1;
            }
            else if (idx2 < genome.connectionGenes.Count)
            {
                excessCount = this.connectionGenes.Count - idx1;
            }

            maxGeneCount = Mathf.Max(this.connectionGenes.Count, genome.connectionGenes.Count);
            maxGeneCount = maxGeneCount > 30 ? maxGeneCount : 1;

            return ((excessCoef * excessCount) / maxGeneCount) + ((disjointCoef * disjointCount) / maxGeneCount) + (weightDiffCoef * (totaWeightDifference / matchingCount));
        }

        private void CopyNodes(NeatGenome genome)
        {
            this.nodeGenes.Clear();
            foreach (NodeGene nodeGene in genome.nodeGenes)
            {
                nodeGenes.Add(new NodeGene(nodeGene.id, nodeGene.type));
            }
        }

        private void AddCopyOfGene(ConnectionGene gene)
        {
            var inNodeId = gene.inNode.id;
            var outNodeId = gene.outNode.id;
            this.connectionGenes.Add(new ConnectionGene(this.nodeGenes[inNodeId], this.nodeGenes[outNodeId], gene.weight, gene.isEnabled, gene.inovationNr));
        }

        private bool InnovateConnection(InovationManger inovManager, float reassignmentRange)
        {
            
            List<int> hiddenNodes = Enumerable.Range(InNodes + OutNodes, nodeGenes.Count - (InNodes + OutNodes)).ToList();
            Utils.Shuffle(hiddenNodes);

            foreach(var hiddenNodeIdx in hiddenNodes)
            {
                NodeGene node1 = nodeGenes[hiddenNodeIdx];

                List<int> allNodes = Enumerable.Range(0, nodeGenes.Count).ToList();
                //Avoid Self Connections
                allNodes.Remove(hiddenNodeIdx);
                Utils.Shuffle(allNodes);

                foreach (var nodeIdx in allNodes)
                {
                    NodeGene node2 = nodeGenes[nodeIdx];
         
                    if (HasConnection(node1, node2)) continue;

                    switch(node2.type) {
                        case TYPE.Input:
                            var newConnToHidden = new ConnectionGene(node2, node1, Random.Range(-reassignmentRange, reassignmentRange), true, 0)
                            {
                                inovationNr = inovManager.GetInv(node2.id, node1.id)
                            };
                            connectionGenes.Add(newConnToHidden);
                            return true;
                        case TYPE.Output:
                            var newConnToOutput = new ConnectionGene(node1, node2, Random.Range(-reassignmentRange, reassignmentRange), true, 0)
                            {
                                inovationNr = inovManager.GetInv(node1.id, node2.id)
                            };
                            connectionGenes.Add(newConnToOutput);
                            return true;
                        case TYPE.Hidden:
                            ConnectionGene newConnHiddenToHidden;
                            if (IsAncestorOf(node1, node2))
                            {
                                newConnHiddenToHidden = new ConnectionGene(node2, node1, Random.Range(-reassignmentRange, reassignmentRange), true, 0)
                                {
                                    inovationNr = inovManager.GetInv(node2.id, node1.id)
                                };
                            }
                            else
                            {
                                newConnHiddenToHidden = new ConnectionGene(node1, node2, Random.Range(-reassignmentRange, reassignmentRange), true, 0)
                                {
                                    inovationNr = inovManager.GetInv(node1.id, node2.id)
                                };
                            }
                            connectionGenes.Add(newConnHiddenToHidden);
                            return true;
                        default:
                            throw new System.Exception("Encountered NodeGene that is neither type Input, Output or Hidden");
                    }
                }
            }

            return false;
        }

        private bool IsAncestorOf(NodeGene origin, NodeGene possibleAncestor)
        {
            var originInputs = connectionGenes.FindAll(gene => gene.outNode == origin);
            return originInputs.Any(originInput => originInput.inNode == possibleAncestor)
                || originInputs.Any(originInput => originInput.inNode.type == TYPE.Hidden && IsAncestorOf(originInput.inNode, possibleAncestor));
        }

        private bool HasConnection(NodeGene node1, NodeGene node2)
        {
            return connectionGenes.Any(con => (con.inNode == node1 && con.outNode == node2) || (con.inNode == node2 && con.outNode == node1));
        }

        private void InnovateNode(InovationManger inovManager)
        {
            //Find random enabled connection
            var enabledConnections = connectionGenes.FindAll(gene => gene.isEnabled);
            if (enabledConnections.Count == 0) return;

            var geneToSplit = enabledConnections[Random.Range(0, enabledConnections.Count)];
            geneToSplit.isEnabled = false;

            NodeGene newNode = new(nodeGenes[^1].id + 1, TYPE.Hidden);
            nodeGenes.Add(newNode);

            var conn1 = new ConnectionGene(geneToSplit.inNode, newNode, 1f, true, 0);
            var conn2 = new ConnectionGene(newNode, geneToSplit.outNode, geneToSplit.weight, true, 0);
            conn1.inovationNr = inovManager.GetInv(geneToSplit.inNode.id, newNode.id);
            conn2.inovationNr = inovManager.GetInv(newNode.id, geneToSplit.outNode.id);

            connectionGenes.Add(conn1);
            connectionGenes.Add(conn2);
        }

        private void InitiateRandomWeightsGenome(float weightRange)
        {
            for (int i = 0; i < InNodes; i++)
            {
                nodeGenes.Add(new NodeGene(i, TYPE.Input));
            }
            for (int i = InNodes; i < InNodes + OutNodes; i++)
            {
                nodeGenes.Add(new NodeGene(i, TYPE.Output));
            }

            long inovation = 0;
            for (int i = 0; i < InNodes; i++)
            {
                for (int j = InNodes; j < InNodes + OutNodes; j++)
                {
                    connectionGenes.Add(new ConnectionGene(nodeGenes[i], nodeGenes[j], Random.Range(-weightRange, weightRange), true, inovation));
                    inovation++;
                }
            }
        }
    }

    public class ConnectionGene
    {
        public NodeGene inNode;
        public NodeGene outNode;
        public float weight;
        public bool isEnabled;
        public long inovationNr;

        public ConnectionGene(NodeGene inNode, NodeGene outNode, float weight, bool isEnabled, long inovationNr)
        {
            this.inNode = inNode;
            this.outNode = outNode;
            this.weight = weight;
            this.isEnabled = isEnabled;
            this.inovationNr = inovationNr;
        }
    }

    public class NodeGene
    {
        public int id;
        public NeatGenome.TYPE type;

        public NodeGene(int id, NeatGenome.TYPE type)
        {
            this.id = id;
            this.type = type;
        }
    }


}