using System.Collections.Generic;
using System.Linq;

namespace Assets.Scripts.Neat
{
    public static class ManagerSynchronizer
    {
        private static int nextIdx;
        private static readonly object accessLock = new ();
        private static List<bool> synchronizedManagers;
        
        public static int AddManagerToSynchronizer()
        {
            int id = 0;
            lock (accessLock)
            {
                synchronizedManagers.Add(true);
                id = nextIdx++;
            }
            return id;
        }

        public static bool AreAllManagersStoped()
        {
            return synchronizedManagers.All(b => b);
        }

        public static void StopManager(int idx)
        {
            synchronizedManagers[idx] = false;
        }

        public static void StartManager(int idx)
        {
            synchronizedManagers[idx] = true;
        }
    }
}