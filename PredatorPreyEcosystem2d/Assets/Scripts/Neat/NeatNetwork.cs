using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting.Dependencies.NCalc;
using UnityEditor.MemoryProfiler;
using UnityEngine;

namespace Assets.Scripts.Neat
{
    public class NeatNetwork
    {
        public NeatGenome neatGenome;
        private readonly List<List<Node>> layers;
        public float fitness;
        public int index;

        public NeatNetwork(NeatGenome neatGenome)
        {
            this.neatGenome = neatGenome;
            layers = new List<List<Node>>();
            ConstructNetwork();
        }

        private void ConstructNetwork()
        {
            var nodes = TranslateGenomeToNetworkNodes();
            int inNodesNr = neatGenome.InNodes;
            int outNodesNr = neatGenome.OutNodes;

            layers.Add(new List<Node>());
            for (int i = 0; i < inNodesNr; i++)
            {
                if(nodes[i] != null)
                {
                    layers[0].Add(nodes[i]);
                    nodes[i] = null;
                }
            }

            int hiddenNodesNr = neatGenome.nodeGenes.Count - (inNodesNr + outNodesNr);
            List<int> hiddenNodesToBeAssigned = nodes.GetRange(inNodesNr + outNodesNr, hiddenNodesNr).Where(x => x != null).Select(x => x.id).ToList();

            while(hiddenNodesToBeAssigned.Count > 0)
            {
                var newLayer = new List<Node>();
                for (int i = 0; i < hiddenNodesToBeAssigned.Count; i++)
                {
                    bool allInputsAssigned = true;
                    foreach (var inputConnection in nodes[hiddenNodesToBeAssigned[i]].inputConnections)
                    {

                        if (nodes[inputConnection.inNode.id] != null)
                        {
                            allInputsAssigned = false;
                            break;
                        }
                    }

                    if (allInputsAssigned)
                    {
                        newLayer.Add(nodes[hiddenNodesToBeAssigned[i]]);
                        hiddenNodesToBeAssigned.RemoveAt(i);
                        --i;
                    }
                }
                
                foreach(var node in newLayer)
                {
                    nodes[node.id] = null;
                }
            }

            var lastLayer = new List<Node>(nodes.GetRange(inNodesNr, outNodesNr));

            layers.Add(lastLayer);
        }


        private List<Node> TranslateGenomeToNetworkNodes()
        {
            var nodes = new Dictionary<int, Node>();

            foreach (var nodeGene in neatGenome.nodeGenes.GetRange(0, neatGenome.InNodes + neatGenome.OutNodes))
            {
                nodes.Add(nodeGene.id, new Node(nodeGene.id));
            }

            Node addNodeAndReturn(int nodeIndex) { nodes.Add(nodeIndex, new Node(nodeIndex)); return nodes[nodeIndex]; }

            foreach (var connectionGene in neatGenome.connectionGenes.FindAll(con => con.isEnabled))
            {
                var inIndex = connectionGene.inNode.id;
                var outIndex = connectionGene.outNode.id;
                Node nodeIn = nodes.ContainsKey(inIndex) ? nodes[inIndex] : addNodeAndReturn(inIndex);
                Node nodeOut = nodes.ContainsKey(outIndex) ? nodes[outIndex] : addNodeAndReturn(outIndex);

                var connection = new Connection(nodeIn, nodeOut)
                {
                    weight = connectionGene.weight
                };
                nodeIn.outputConnections.Add(connection);
                nodeOut.inputConnections.Add(connection);
            }

            return Enumerable.Range(0, neatGenome.nodeGenes.Count).Select(id => nodes.ContainsKey(id) ? nodes[id] : null).ToList();
        }

        public float[] FeedForward(float[] inputs)
        {
            if(neatGenome.InNodes != inputs.Length)
            {
                throw new ArgumentOutOfRangeException("The number of inputs must match the number of input nodes");
            }

            for (int i = 0; i < inputs.Length; i++)
            {
                layers[0][i].value = inputs[i];
            }

            for(int i = 1; i < layers.Count - 1; i++) {
                //ComputeLayerNodeValues(i, ModifiedSigmoidActivation);
                ComputeLayerNodeValues(i, TanhActivation);
            }

            ComputeLayerNodeValues(layers.Count - 1, TanhActivation);

            var outputs = (from node in layers[^1]
                        select node.value).ToArray();

            return outputs;
        }

        private float ComputeNodeValue(Node node, Func<float, float> activation)
        {
            float sum = 0f;
            foreach(var conection in node.inputConnections)
            {
                sum += conection.weight * conection.inNode.value;
            }

            node.value = activation(sum);
            return node.value;
        }

        private void ComputeLayerNodeValues(int layerIndex, Func<float, float> activation)
        {
            foreach(var node in layers[layerIndex])
            {
                ComputeNodeValue(node, activation);
            }
        }

        private float ModifiedSigmoidActivation(float z)
        {
            return (float)(1 / (1 + Math.Pow(Math.E, -4.9f * z)));
        }

        private float TanhActivation(float z)
        {
            return (float)((Math.Pow(Math.E, z) - Math.Pow(Math.E, -z))/(Math.Pow(Math.E, z) + Math.Pow(Math.E, -z)));
        }


    }

    internal class Node
    {
        public int id;
        public float value;
        public List<Connection> inputConnections;
        public List<Connection> outputConnections;

        public Node(int id)
        {
            this.id = id;
            inputConnections = new List<Connection>();
            outputConnections = new List<Connection>();
        }
    }

    internal class Connection
    {
        public Node inNode;
        public Node outNode;
        public float weight;

        public Connection(Node inNode, Node outNode)
        {
            this.inNode = inNode;
            this.outNode = outNode;
        }
    }
}