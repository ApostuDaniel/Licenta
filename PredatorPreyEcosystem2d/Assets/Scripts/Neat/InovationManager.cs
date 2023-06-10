using System.Collections.Generic;

namespace Assets.Scripts.Neat
{
    public class InovationManger
    {
        private long inv;
        public Dictionary<(int, int), long> currentConectionList;
        public InovationManger(long latestInovation) {
            this.inv = latestInovation;
            currentConectionList = new Dictionary<(int, int), long>();
        }

        public void ResetForNextGeneration()
        {
            currentConectionList.Clear();
        }

        public long GetInv(int inNode, int outNode)
        {
            bool inovationExists = currentConectionList.TryGetValue((inNode, outNode), out long inovationNumber);

            if (!inovationExists)
            {
                inovationNumber = inv++;
                currentConectionList.Add((inNode, outNode), inovationNumber);
            }
            return inovationNumber;
        }
    }
}