using Assets.Scripts.Food;
using Assets.Scripts.Neat;
using System;
using UnityEngine;

public class PreyController : CreatureController
{
    public bool useFoW = true;
    public FoodManager foodManager;
    public Func<PreyController, float> energyOffering;
    public LayerMask predatorMask;
    protected override void Start()
    {
        accelerationFactor = 3f;
        turnFactor = 90;
        smoothingFactor = 0.1f;
        base.Start();
    }

    protected override void OnTriggerEnter2D(Collider2D collision)
    {
        switch (collision.transform.tag)
        {
            case "Wall":
                //Debug.Log("Touched Wall");
                Die(DeathCause.WallHit);
                break;

            case "Food":
                //Debug.Log("Touched Food");
                collision.gameObject.GetComponent<FoodController>().DieAndRespawn();
                currentEnergy += energyForFood;
                totalEnergy += energyForFood;
                break;
        }
    }

    public float GetHunted(PredatorController predator)
    {
        Die(DeathCause.Predation);
        return energyOffering(this);
    }

    public override void Die(DeathCause deathCause)
    {
        if (foodManager != null)
        {
            Destroy(gameObject);
            foodManager.MakeSingleFood(network.index);
        }
        else
        {
            network.fitness = totalEnergy;
            Destroy(gameObject);
            manager.RegisterDeath(deathCause);
        }
    }

    protected override int SetSensors()
    {
        int currSensor = base.SetSensors();

        //var rayDirections = new Vector2[] { transform.up + transform.right, transform.up, transform.up - transform.right };
        //foreach (var rayDirection in rayDirections)
        //{
        //    sensors[currSensor++] = CastRay(rayDirection, wallRayDistance, "Food", Color.yellow);
        //}

        float[] detectionSensorsValues;
        if (useFoW)
        {
            detectionSensorsValues = FindVisibleTargets(fowTargetsMask);
        }
        else
        {
            detectionSensorsValues = FindRaycastTargets();
        }

        foreach (var sensorValue in detectionSensorsValues)
        {
            sensors[currSensor++] = sensorValue;
        }

        foreach(var enemyDetectionSensorValue in FindVisibleTargets(predatorMask))
        {
            sensors[currSensor++] = enemyDetectionSensorValue;
        }

        return currSensor;
    }

    private float[] FindRaycastTargets()
    {
        var rayDirections = new Vector2[] { transform.up + transform.right, transform.up, transform.up - transform.right };
        float[] values = new float[rayDirections.Length];
        for(int i = 0; i < rayDirections.Length; i++) {
            values[i] = CastRay(rayDirections[i], wallRayDistance, "Food", Color.yellow);
        }

        return values;
    }
}