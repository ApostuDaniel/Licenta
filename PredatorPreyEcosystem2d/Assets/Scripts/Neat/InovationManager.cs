namespace Assets.Scripts.Neat
{
    public class InovationManger
    {
        private long inv;
        public InovationManger(long latestInovation) {
            this.inv = latestInovation;
        }

        public long GetInv()
        {
            return inv++;
        }
    }
}