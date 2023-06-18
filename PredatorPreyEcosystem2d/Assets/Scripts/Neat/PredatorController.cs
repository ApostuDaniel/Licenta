using Assets.Scripts.Food;
using Assets.Scripts.Neat;
using UnityEngine;

public class PredatorController : CreatureController
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

    protected override void Die(DeathCause deathCause)
    {
        network.fitness = totalEnergy;
        Destroy(gameObject);
        GameObject.FindObjectOfType<NeatManager>().RegisterDeath(deathCause);
    }

    protected override int SetSensors()
    {
        //for (int i = 0; i < rayDirections.Length; i++)
        //{
        //    hit = Physics2D.Raycast(transform.position, rayDirections[i], rayDistance);
        //    if (hit.collider != null && hit.transform.CompareTag("Wall"))
        //    {
        //        sensors[currSensor] = hit.distance / rayDistance;
        //        Debug.DrawLine(transform.position, hit.point, Color.yellow);
        //    }
        //    else { sensors[currSensor] = 1.0f; }
        //    currSensor++;
        //}

        //for (int i = 0; i < rayDirections.Length; i++)
        //{
        //    hit = Physics2D.Raycast(transform.position, rayDirections[i], rayDistance);
        //    if (hit.collider != null && hit.transform.CompareTag("Food"))
        //    {
        //        sensors[currSensor] = hit.distance / rayDistance;
        //        Debug.DrawLine(transform.position, hit.point, Color.yellow);
        //    }
        //    else { sensors[currSensor] = 1.0f;}
        //    currSensor++;
        //}

        int currSensor = base.SetSensors();

        foreach (var foodDensity in FindVisibleTargets(fowTargetsMask))
        {
            sensors[currSensor++] = foodDensity;
        }

        return currSensor;
    }
}