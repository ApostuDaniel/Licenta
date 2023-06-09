using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Assets.Scripts.Neat
{
    public class NeatGenome
    {
        public enum TYPE { Input,  Output, Hidden }

        public List<NodeGene> nodeGenes;
        public List<ConnectionGene> connectionGenes;

        public int InNodes { get;}
        public int OutNodes { get;}

        public NeatGenome(List<NodeGene> nodeGenes, List<ConnectionGene> connectionGenes)
        {
            this.nodeGenes = nodeGenes; this.connectionGenes = connectionGenes;

            foreach (NodeGene nodeGene in nodeGenes)
            {

                switch (nodeGene.type)
                {
                    case TYPE.Input: InNodes++; break;
                    case TYPE.Output: OutNodes++; break;
                    default: break;
                }
            }
        }

        public NeatGenome()
        {
            nodeGenes = new List<NodeGene>();
            connectionGenes = new List<ConnectionGene>();
        }

        public NeatGenome(int inputNodes,  int outputNodes, float weightRange = 1f) : this()
        {
            InNodes = inputNodes; OutNodes = outputNodes;

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
                                inovationNr = inovManager.GetInv()
                            };
                            connectionGenes.Add(newConnToHidden);
                            return true;
                        case TYPE.Output:
                            var newConnToOutput = new ConnectionGene(node1, node2, Random.Range(-reassignmentRange, reassignmentRange), true, 0)
                            {
                                inovationNr = inovManager.GetInv()
                            };
                            connectionGenes.Add(newConnToOutput);
                            return true;
                        case TYPE.Hidden:
                            ConnectionGene newConnHiddenToHidden;
                            if (IsAncestorOf(node1, node2))
                            {
                                newConnHiddenToHidden = new ConnectionGene(node2, node1, Random.Range(-reassignmentRange, reassignmentRange), true, 0)
                                {
                                    inovationNr = inovManager.GetInv()
                                };
                            }
                            else
                            {
                                newConnHiddenToHidden = new ConnectionGene(node1, node2, Random.Range(-reassignmentRange, reassignmentRange), true, 0)
                                {
                                    inovationNr = inovManager.GetInv()
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
            conn1.inovationNr = inovManager.GetInv();
            conn2.inovationNr = inovManager.GetInv();

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