using Unity.VisualScripting;
using UnityEngine;
using static UnityEngine.EventSystems.StandaloneInputModule;

namespace Assets.Scripts.Food
{
    public class FoodManager : MonoBehaviour
    {
        public GameObject foodPrefab;
        public int totalFood;
        public int currentFood;
        public bool fromPrey;
        private GameObject[] foods;
        //private bool repopulate = false;

        private void Awake()
        {
            currentFood = totalFood;
            foods = new GameObject[totalFood];
            GenerateFood();
        }

        public void GenerateFood()
        {
            for (int i = 0; i < totalFood; i++)
            {
                MakeSingleFood(i);
            }
        }

        public void MakeSingleFood(int id)
        {
            var pos = new Vector2(Random.value, Random.value);
            pos = Camera.main.ViewportToWorldPoint(pos);

            foods[id] = Instantiate(foodPrefab, pos, transform.rotation);

            if(fromPrey)
            {
                var preyControllerComponent = foods[id].GetComponent<PreyController>();
                preyControllerComponent.useFoW = false;
                preyControllerComponent.inputNodes = 7;
                preyControllerComponent.outputNodes = 2;
                preyControllerComponent.network = new Neat.NeatNetwork(new Neat.NeatGenome(7, 2, 2f))
                {
                    index = id
                };
                preyControllerComponent.foodManager = this;
                preyControllerComponent.totalEnergy = 10000;
                preyControllerComponent.energyForFood = 0;
                preyControllerComponent.energyOffering = (preyControllerComponent) => 0f;
                preyControllerComponent.speedConsumptionMultiplier = -1.5f;

                preyControllerComponent.fowTargetsMask = 0;
                preyControllerComponent.wallRayDistance = 20f;
                //preyControllerComponent.viewDistance = 20f;
                //preyControllerComponent.viewAngle = 90f;
                //preyControllerComponent.angleSplits = 3;
                preyControllerComponent.spriteColor = new Color32(0, 255, 0, 255);
            }
            else
            {
                var foodControllerComponent = foods[id].GetComponent<FoodController>();
                foodControllerComponent.foodManger = this;
                foodControllerComponent.foodId = id;
            }
            currentFood++;
        }

        public void DestroyFood()
        {
            if (fromPrey)
            {
                PreyController[] preys = FindObjectsOfType<PreyController>();
                foreach (PreyController prey in preys)
                {
                    Destroy(prey.gameObject);
                    currentFood--;
                }
            }
            FoodController[] localFoods = FindObjectsOfType<FoodController>();
            foreach (FoodController food in localFoods)
            {
                Destroy(food.gameObject);
                currentFood--;
            }
        }
    }
}