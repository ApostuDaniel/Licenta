using Assets.Scripts.Food;
using Assets.Scripts.Neat;
using UnityEngine;

public class PreyController : CreatureController
{
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

    public void GetHunted(PredatorController predator)
    {
        Die(DeathCause.Predation);
    }

    public override void Die(DeathCause deathCause)
    {
        network.fitness = totalEnergy;
        Destroy(gameObject);
        GameObject.FindObjectOfType<NeatManager>().RegisterDeath(deathCause);
    }

    protected override int SetSensors()
    {
        int currSensor = base.SetSensors();

        //var rayDirections = new Vector2[] { transform.up + transform.right, transform.up, transform.up - transform.right };
        //foreach (var rayDirection in rayDirections)
        //{
        //    sensors[currSensor++] = CastRay(rayDirection, wallRayDistance, "Food", Color.yellow);
        //}

        foreach (var foodDensity in FindVisibleTargets(fowTargetsMask))
        {
            sensors[currSensor++] = foodDensity;
        }

        return currSensor;
    }
}