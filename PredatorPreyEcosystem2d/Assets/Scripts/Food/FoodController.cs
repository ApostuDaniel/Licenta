using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Food
{
    public class FoodController : MonoBehaviour
    {
        public FoodManager foodManger;
        public int foodId;

        public void DieAndRespawn()
        {
            Destroy(gameObject);
            foodManger.MakeSingleFood(foodId);
        }
    }
}