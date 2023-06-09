using UnityEngine;

namespace Assets.Scripts.Food
{
    public class FoodManager : MonoBehaviour
    {
        public GameObject foodPrefab;
        public int totalFood;
        public int currentFood;
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
            var foodControllerComponent = foods[id].GetComponent<FoodController>();
            foodControllerComponent.foodManger = this;
            foodControllerComponent.foodId = id;
            currentFood++;
        }

        public void DestroyFood()
        {
            FoodController[] localFoods = FindObjectsOfType<FoodController>();
            foreach (FoodController food in localFoods)
            {
                Destroy(food.gameObject);
                currentFood--;
            }
        }
    }
}