using Assets.Scripts.Neat;
using System;
using System.Collections.Generic;
using UnityEngine;

public class CreatureController : MonoBehaviour
{
    public enum DeathCause{ WallHit, Starvation, Predation, TimeEnd};

    [Header("Movement settings")]
    public float accelerationFactor;
    public int turnFactor;
    public float smoothingFactor;

    [Header("NEAT")]
    public NeatNetwork network;

    public int inputNodes, outputNodes;
    public Color spriteColor;

    [Header("Energy")]
    public float totalEnergy;

    public float currentEnergy;
    public float energyForFood;
    public float speedConsumptionMultiplier;

    [Header("Field of view")]
    public float viewDistance;
    [Range(0, 180)]
    public float viewAngle;
    public int angleSplits;
    public LayerMask fowTargetsMask;

    public float wallRayDistance;
    //[HideInInspector]
    //public List<List<Transform>> fowTargets = new();

    [SerializeField]
    protected float[] sensors;

    protected float pastVelocity;


    // Start is called before the first frame update
    protected virtual void Start()
    {
        currentEnergy = totalEnergy;
        sensors = new float[inputNodes];
        //for (int i = 0; i < angleSplits; i++)
        //{
        //    fowTargets.Add(new());
        //}

        gameObject.GetComponent<SpriteRenderer>().color = spriteColor;
    }

    protected void FixedUpdate()
    {
        SetSensors();
        float[] outputs = network != null? network.FeedForward(sensors): new float[] { 0f, 0f } ;
        pastVelocity = outputs[0];
        MoveCreature(outputs[0], outputs[1]);
        UpdateCreatureState();
    }

    public Vector3 DirFromAngle(float angleInDegrees, bool angleIsGlobal)
    {
        if (!angleIsGlobal)
        {
            angleInDegrees += transform.eulerAngles.z;
        }
        return new Vector3(Mathf.Sin(angleInDegrees * Mathf.Deg2Rad), -Mathf.Cos(angleInDegrees * Mathf.Deg2Rad), 0);
    }

    protected float[] FindVisibleTargets(LayerMask detectionMask)
    {
        //fowTargets.ForEach(list => list.Clear());
        float[] splitValues = new float[angleSplits];
        Collider2D[] targetsInViewRadius = Physics2D.OverlapCircleAll(transform.position, viewDistance, detectionMask);
        float angleIncrement = viewAngle / angleSplits;
        float limitLeft = viewAngle / 2;
        for (int i = 0; i < targetsInViewRadius.Length; i++)
        {
            Transform target = targetsInViewRadius[i].transform;
            Vector3 dirToTarget = (target.position - transform.position).normalized;

            var angleToTarget = Vector2.SignedAngle(transform.up, dirToTarget);
            for (int j = 0; j < angleSplits; j++)
            {
                if (angleToTarget <= limitLeft - (j * angleIncrement) && angleToTarget >= limitLeft - ((j + 1) * angleIncrement))
                {
                    //fowTargets[j].Add(target);
                    float distanceToTarget = Vector2.Distance(transform.position, target.position);
                    //we want to normalize the distance to the target in [0,1], and the closer to the target, the higher the value
                    splitValues[j] += 1f - (distanceToTarget / viewDistance);
                    break;
                }
            }
        }

        return splitValues;
    }

    protected void UpdateCreatureState()
    {
        currentEnergy -= Time.deltaTime + ((speedConsumptionMultiplier * Math.Abs(pastVelocity) * Math.Abs(pastVelocity)) * Time.deltaTime);

        if (currentEnergy <= 0)
        {
            Die(DeathCause.Starvation);
        }
    }

    protected void MoveCreature(float accelerationInput, float turnInput)
    {
        // Getting Next Position
        Vector3 input = Vector3.Lerp(Vector3.zero, new Vector3(0, accelerationInput * accelerationFactor, 0), smoothingFactor);
        input = transform.TransformDirection(input);

        // Movement of Agent
        transform.position += input;

        // Rotation of Agent
        transform.eulerAngles += new Vector3(0, 0, turnInput * turnFactor * smoothingFactor);
    }

    protected virtual void OnTriggerEnter2D(Collider2D collision)
    {
        switch (collision.transform.tag)
        {
            case "Wall":
                Die(DeathCause.WallHit);
                break;
        }
    }

    public virtual void Die(DeathCause deathCause)
    {
        network.fitness = totalEnergy;
        Destroy(gameObject);
        GameObject.FindObjectOfType<NeatManager>().RegisterDeath(deathCause);
    }

    protected virtual int SetSensors()
    {
        int currSensor = 0;
        //Bias
        sensors[currSensor++] = 1.0f;
        //Wall Raycast
        sensors[currSensor++] = CastRay(transform.up, wallRayDistance, "Wall", Color.white);
        //Past acceleration
        sensors[currSensor++] = pastVelocity;
        //Current angle from y axis
        sensors[currSensor++] = Vector2.SignedAngle(transform.up, Vector2.up) / 180f;

        return currSensor;
    }

    protected float CastRay(Vector3 direction, float distance, string hitTag, Color lineColor)
    {
        RaycastHit2D hit = Physics2D.Raycast(transform.position, direction, distance);
        if (hit.collider != null && hit.transform.CompareTag(hitTag))
        {
            Debug.DrawLine(transform.position, hit.point, lineColor);
            return hit.distance / wallRayDistance;
        }

        return 1.0f;
    }
}