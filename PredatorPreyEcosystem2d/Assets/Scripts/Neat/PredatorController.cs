using Assets.Scripts.Food;
using Assets.Scripts.Neat;
using UnityEngine;

public class PredatorController : CreatureController
{
    protected override void Start()
    {
        accelerationFactor = 5f;
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

            case "Prey":
                //Debug.Log("Touched Food");
                var extraAcquiredEnergy = collision.gameObject.GetComponent<PreyController>().GetHunted(this);
                currentEnergy += energyForFood + extraAcquiredEnergy;
                totalEnergy += energyForFood + extraAcquiredEnergy;
                break;
        }
    }

    public override void Die(DeathCause deathCause)
    {
        network.fitness = totalEnergy;
        Destroy(gameObject);
        manager.RegisterDeath(deathCause);
    }

    protected override int SetSensors()
    {
        int currSensor = base.SetSensors();

        foreach (var foodDensity in FindVisibleTargets(fowTargetsMask))
        {
            sensors[currSensor++] = foodDensity;
        }

        return currSensor;
    }
}