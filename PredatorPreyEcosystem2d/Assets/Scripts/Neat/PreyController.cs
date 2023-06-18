using Assets.Scripts.Food;
using Assets.Scripts.Neat;
using System;
using System.Collections.Generic;
using Unity.Burst.CompilerServices;
using UnityEngine;
using static Unity.VisualScripting.Member;

public class PreyController : MonoBehaviour
{
    [Header("Movement settings")]
    public float accelerationFactor = 3f;
    public int turnFactor = 90;
    public float smoothingFactor = 0.1f;

    //[Header("Controls")]
    //[Range(-1f, 1f)]
    //public float accelerationInput, turnInput;

    [Header("NEAT")]
    public NeatNetwork network;
    public int inputNodes, outputNodes;
    public Color spriteColor;
    //private NeatGenome genome;

    private float rayDistance = 20f;

    //private float wallLimitX = 60f;
    //private float wallLimitY = 34f;

    [Header("Energy")]
    public float totalEnergy;
    public float currentEnergy;
    public float energyForFood;
    public float speedConsumptionMultiplier = 1.5f;

    [Header("Field of view")]
    public float viewDistance;
    [Range(0, 180)]
    public float viewAngle;
    public int angleSplits;

    public LayerMask fowTargetMask;

    //[HideInInspector]
    //public List<List<Transform>> fowTargets = new();

    [SerializeField]
    private float[] sensors;
    private float pastVelocity;

    void Awake()
    {
    }

    // Start is called before the first frame update
    void Start()
    {
        currentEnergy = totalEnergy;
        sensors = new float[inputNodes];
        fowTargetMask = LayerMask.GetMask("Food");
        //for (int i = 0; i < angleSplits; i++)
        //{
        //    fowTargets.Add(new());
        //}

        gameObject.GetComponent<SpriteRenderer>().color = spriteColor;
    }

    void FixedUpdate()
    {
        SetSensors();
        float[] outputs = network.FeedForward(sensors);
        pastVelocity = outputs[0];
        MovePrey(outputs[0], outputs[1]);
        UpdatePreyState();
    }

    public Vector3 DirFromAngle(float angleInDegrees, bool angleIsGlobal)
    {
        if (!angleIsGlobal)
        {
            angleInDegrees += transform.eulerAngles.z;
        }
        return new Vector3(Mathf.Sin(angleInDegrees * Mathf.Deg2Rad), -Mathf.Cos(angleInDegrees * Mathf.Deg2Rad), 0);
    }

    private float[] FindVisibleTargets()
    {
        //fowTargets.ForEach(list => list.Clear());
        float[] splitValues = new float[angleSplits];
        Collider2D[] targetsInViewRadius = Physics2D.OverlapCircleAll(transform.position, viewDistance, fowTargetMask);
        float angleIncrement = viewAngle / angleSplits;
        float limitLeft = viewAngle / 2;
        for (int i = 0; i < targetsInViewRadius.Length; i++)
        {
            Transform target = targetsInViewRadius[i].transform;
            Vector3 dirToTarget = (target.position - transform.position).normalized;

            
            var angleToTarget = Vector2.SignedAngle(transform.up, dirToTarget);
            for(int j = 0; j < angleSplits; j++)
            {
                if (angleToTarget <= limitLeft - (j * angleIncrement) && angleToTarget >= limitLeft - ((j + 1) * angleIncrement))
                {
                    //fowTargets[j].Add(target); break;

                    float distanceToTarget = Vector2.Distance(transform.position, target.position);
                    //we want to normalize the distance to the target in [0,1], and the closer to the target, the higher the value
                    splitValues[j] += 1f - (distanceToTarget/viewDistance);
                }
            }
        }

        return splitValues;
    }

    private void UpdatePreyState()
    {
        currentEnergy -= Time.deltaTime + ((speedConsumptionMultiplier * Math.Abs(pastVelocity) * Math.Abs(pastVelocity)) * Time.deltaTime);

        if(currentEnergy <= 0)
        {
            Die();
        }
    }

    private void MovePrey(float accelerationInput, float turnInput)
    {
        // Getting Next Position
        Vector3 input = Vector3.Lerp(Vector3.zero, new Vector3(0, accelerationInput * accelerationFactor, 0), smoothingFactor);
        input = transform.TransformDirection(input);

        // Movement of Agent
        transform.position += input;

        // Rotation of Agent
        transform.eulerAngles += new Vector3(0, 0, turnInput * turnFactor * smoothingFactor);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        switch (collision.transform.tag)
        {
            case "Wall":
                //Debug.Log("Touched Wall");
                Die();
                break;
            case "Food":
                //Debug.Log("Touched Food");
                collision.gameObject.GetComponent<FoodController>().DieAndRespawn();
                currentEnergy += energyForFood;
                totalEnergy += energyForFood;
                break;
        }

    }

    private void Die()
    {
        network.fitness = totalEnergy;
        Destroy(gameObject);
        GameObject.FindObjectOfType<NeatManager>().RegisterDeath();
    }

    private void SetSensors()
    {
        int currSensor = 0;
        //Bias
        sensors[currSensor++] = 1.0f;


        var rayDirections = new Vector3[3] {transform.up, transform.up + transform.right, transform.up - transform.right };
        RaycastHit2D hit;

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

        hit = Physics2D.Raycast(transform.position, transform.up, rayDistance);
        if (hit.collider != null && hit.transform.CompareTag("Wall"))
        {
            sensors[currSensor] = hit.distance / rayDistance;
            Debug.DrawLine(transform.position, hit.point, Color.white);
        }
        else { sensors[currSensor] = 1.0f; }
        currSensor++;

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

        float[] foodDensities = FindVisibleTargets();
        foreach( var foodDensity in foodDensities) {
            sensors[currSensor++] = foodDensity;
        }

        sensors[currSensor++] = pastVelocity;
        sensors[currSensor++] = Vector2.SignedAngle(transform.up, Vector2.up) / 180f;
    }
}
