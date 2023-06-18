using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Unity.VisualScripting;
using Unity.VisualScripting.Dependencies.NCalc;
using UnityEditor.MemoryProfiler;
using UnityEngine;

namespace Assets.Scripts.Neat
{
    public class NeatNetwork
    {
        public float fitness;
        public int index;
        private enum CycleDetection { Unverified, Verified, Cycle }
        private readonly List<int> evalOrder;
        private  List<int> inputs;
        private  List<int> outputs;
        private readonly Dictionary<int, Node> nodes;

        public NeatNetwork(NeatGenome neatGenome)
        {
            evalOrder = new List<int>();
            nodes = new Dictionary<int, Node>();
            inputs = new List<int>();
            outputs = new List<int>();
            ConstructNetwork(neatGenome);
        }

        private void ConstructNetwork(NeatGenome neatGenome)
        {
            List<Connection> connections = GetEnabledConnectionsAndNodes(neatGenome);
            //if(connections.Exists(con => con.inNode == con.outNode && inputs.Contains(con.outNode))){
            //    int i = 0;
            //}

            //This dictionary will be used to identify which connections form a cycle, and also which one has been marked in establishing order

            var verificationDictionary = DetectCycles(connections);

            //Remove disconnected structures
            RemoveExtraStructures(verificationDictionary);

            EstablishEvalOrder(verificationDictionary);
        }

        private void RemoveExtraStructures(Dictionary<Connection, (CycleDetection verification, bool mark)> verificationDictionary)
        {
            var extraConnections = verificationDictionary.Where(pair => pair.Value.verification == CycleDetection.Unverified).Select(pair => pair.Key).ToList();
            
            foreach (var connection in extraConnections)
            {
                nodes[connection.inNode].outputConnections.Remove(connection);
                nodes[connection.outNode].inputConnections.Remove(connection);
                verificationDictionary.Remove(connection);
            }

            foreach(var nodeId in nodes.Keys.ToList())
            {
                if (!inputs.Contains(nodeId) && !outputs.Contains(nodeId) && nodes[nodeId].inputConnections.Count == 0)
                {
                    nodes.Remove(nodeId);
                }
            }

        }

        private void EstablishEvalOrder(Dictionary<Connection, (CycleDetection verification, bool mark)> verificationDictionary)
        {
            var remaining = nodes.Keys.ToList();

            var currOrder = inputs.Where(nodeId => nodes[nodeId].inputConnections.All(conn => verificationDictionary[conn].verification == CycleDetection.Cycle)).ToList();
            foreach (var node in currOrder)
            {
                remaining.Remove(node);
            }

            while (remaining.Count > 0)
            {
                var next = new List<int>();
                foreach (var nodeId in currOrder)
                {
                    foreach (var con in nodes[nodeId].outputConnections)
                    {
                        if (verificationDictionary[con].verification == CycleDetection.Verified)
                        {
                            verificationDictionary[con] = (CycleDetection.Verified, true);
                        }
                    }
                }

                for(int i = 0; i < remaining.Count; i++)
                {
                    var nodeId = remaining[i];
                    if (nodes[nodeId].inputConnections.All(conn => verificationDictionary[conn].mark || verificationDictionary[conn].verification == CycleDetection.Cycle))
                    {
                        next.Add(nodeId);
                        remaining.RemoveAt(i);
                        i--;
                    }
                }

                evalOrder.AddRange(currOrder);
                currOrder = next;
            }

            evalOrder.AddRange(currOrder);
        }

        private Dictionary<Connection, (CycleDetection verification, bool mark)> DetectCycles(List<Connection> connections)
        {
            Dictionary<Connection, (CycleDetection verification, bool mark)> verificationDictionary = new();
            foreach (Connection conn in connections)
            {
                verificationDictionary.Add(conn, (CycleDetection.Unverified, false));
            }

            foreach(var nodeId in inputs)
            {
                var currRoute = new List<int>() { nodeId };
                ExploreRouteCiclicity(currRoute, verificationDictionary);
            }

            return verificationDictionary;
        }

        private void ExploreRouteCiclicity(List<int> route, Dictionary<Connection, (CycleDetection verification, bool mark)> verificationDictionary)
        {
            foreach(var conn in nodes[route[^1]].outputConnections)
            {
                if(verificationDictionary[conn].verification == CycleDetection.Unverified)
                {
                    if (route.Contains(conn.outNode))
                    {
                        verificationDictionary[conn] = (CycleDetection.Cycle, false);
                    }
                    else
                    {
                        verificationDictionary[conn] = (CycleDetection.Verified, false);
                        ExploreRouteCiclicity(new List<int>(route) { conn.outNode }, verificationDictionary);
                    }
                }
            }
        }

        private List<Connection> GetEnabledConnectionsAndNodes(NeatGenome neatGenome)
        {
            var inNodes = neatGenome.nodeGenes.Where(nodeGene => nodeGene.type == NeatGenome.TYPE.Input).ToList();
            var outNodes = neatGenome.nodeGenes.Where(nodeGene => nodeGene.type == NeatGenome.TYPE.Output).ToList();

            foreach (var nodeGene in inNodes) {
                nodes.Add(nodeGene.id, new Node(nodeGene.id));
                inputs.Add(nodeGene.id);
            }

            foreach (var nodeGene in outNodes)
            {
                nodes.Add(nodeGene.id, new Node(nodeGene.id));
                outputs.Add(nodeGene.id);
            }

            inputs = inputs.OrderBy(i => i).ToList();
            outputs = outputs.OrderBy(i => i).ToList();

            Node addNodeAndReturn(int nodeIndex) { nodes.Add(nodeIndex, new Node(nodeIndex)); return nodes[nodeIndex]; }

            var connections = new List<Connection>();
            foreach (var connectionGene in neatGenome.connectionGenes.FindAll(con => con.isEnabled))
            {
                var inIndex = connectionGene.inNode.id;
                var outIndex = connectionGene.outNode.id;
                Node nodeIn = nodes.ContainsKey(inIndex) ? nodes[inIndex] : addNodeAndReturn(inIndex);
                Node nodeOut = nodes.ContainsKey(outIndex) ? nodes[outIndex] : addNodeAndReturn(outIndex);

                var connection = new Connection(inIndex, outIndex)
                {
                    weight = connectionGene.weight
                };
                connections.Add(connection);
                nodeIn.outputConnections.Add(connection);
                nodeOut.inputConnections.Add(connection);
            }

            return connections;
        }

        public float[] FeedForward(float[] sensors)
        {
            if (inputs.Any(i => nodes[i].inputConnections.Any(con => con.inNode == i)) && sensors.Sum() > 3)
            {
                ;
            }

            if (inputs.Count != sensors.Length)
            {
                throw new ArgumentOutOfRangeException("The number of inputs must match the number of input nodes");
            }

            foreach(var nodeId in evalOrder)
            {
                ComputeNodeValue(nodes[nodeId], nodeId < sensors.Length ? sensors[nodeId] : 0f, nodeId < sensors.Length ? (x => x) : TanhActivation);
            }

            //for(int i = 1; i < layers.Count - 1; i++) {
            //    //ComputeLayerNodeValues(i, ModifiedSigmoidActivation);
            //    ComputeLayerNodeValues(i, TanhActivation);
            //}

            //ComputeLayerNodeValues(layers.Count - 1, TanhActivation);

            //var outputs = (from node in layers[^1]
            //            select node.value).ToArray();

            var results = outputs.Select(nodeId => nodes[nodeId].value).ToArray();
            //var hasNan = results.Any(x => float.IsNaN(x));
            //if (hasNan)
            //{
            //    ;
            //}
            return results;
        }

        private void ComputeNodeValue(Node node, float startValue, Func<float, float> activation)
        {
            var sum = startValue;
            foreach(var conn in node.inputConnections)
            {
                sum += conn.weight * nodes[conn.inNode].value;
            }

            node.value = activation(sum);
        }

        //private void ComputeLayerNodeValues(int layerIndex, Func<float, float> activation)
        //{
        //    foreach(var node in layers[layerIndex])
        //    {
        //        ComputeNodeValue(node, activation);
        //    }
        //}

        private float ModifiedSigmoidActivation(float z)
        {
            return (float)(1 / (1 + Math.Pow(Math.E, -4.9f * z)));
        }

        private float TanhActivation(float z)
        {
            float res = (float)((Math.Pow(Math.E, z) - Math.Pow(Math.E, -z)) / (Math.Pow(Math.E, z) + Math.Pow(Math.E, -z)));
            if (float.IsNaN(res))
            {
                if (Math.Abs(z) < 0.1) res = 0f;
                else res = z > 0? 1f : -1f;
            }

            return res;
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
        public int inNode;
        public int outNode;
        public float weight;

        public Connection(int inNode, int outNode)
        {
            this.inNode = inNode;
            this.outNode = outNode;
        }
    }
}