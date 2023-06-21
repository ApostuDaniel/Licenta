using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Assets.Scripts.Neat
{
    public class ManagerOversier : MonoBehaviour
    {
        public GameObject[] managers;
        private bool[] stopedState;


        // Use this for initialization
        void Start()
        {
            stopedState = new bool[managers.Length];
        }
        
        public bool AllManagersStoped()
        {
            return stopedState.All(state => state);
        }

        public void StopManager(GameObject manager)
        {
            stopedState[Array.IndexOf(managers, manager)] = true;
        }

        public void StartManager(GameObject manager)
        {
            stopedState[Array.IndexOf(managers, manager)] = false;
        }
    }
}