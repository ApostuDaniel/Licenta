using Assets.Scripts.Food;
using Assets.Scripts.Neat;
using System;
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

    private float hitDivider = 20f;
    private float rayDistance = 20f;

    private float wallLimitX = 60f;
    private float wallLimitY = 34f;

    [Header("Energy")]
    public float totalEnergy;
    public float currentEnergy;
    public float energyForFood;


    private float[] sensors;
    private float pastVelocity;

    void Awake()
    {
    }

    // Start is called before the first frame update
    void Start()
    {
        currentEnergy = totalEnergy;
        //genome = new NeatGenome(inputNodes, outputNodes);
        //network = new NeatNetwork(genome);
        sensors = new float[inputNodes];

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

    private void UpdatePreyState()
    {
        currentEnergy -= Time.deltaTime + (Math.Abs(pastVelocity) * Time.deltaTime);

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
        GameObject.FindObjectOfType<NeatManager>().RegisterDeath();
        Destroy(gameObject);
    }

    private void SetSensors()
    {
        //Bias
        sensors[0] = 1.0f;


        var rayDirections = new Vector3[3] {transform.up, transform.up + transform.right, transform.up - transform.right };
        RaycastHit2D hit;

        for (int i = 0; i < rayDirections.Length; i++)
        {
            hit = Physics2D.Raycast(transform.position, rayDirections[i], rayDistance);
            if (hit.collider != null)
            {
                if (hit.transform.CompareTag("Wall"))
                {
                    sensors[1 + i] = hit.distance / hitDivider;
                    Debug.DrawLine(transform.position, hit.point, Color.white);
                }
                else sensors[1 + i] = 1.0f;
            }
        }

        for (int i = 0; i < rayDirections.Length; i++)
        {
            hit = Physics2D.Raycast(transform.position, rayDirections[i], rayDistance);
            if (hit.collider != null)
            {
                if (hit.transform.CompareTag("Food"))
                {
                    sensors[4 + i] = hit.distance / hitDivider;
                    Debug.DrawLine(transform.position, hit.point, Color.yellow);
                }
                else sensors[4 + i] = 1.0f;
            }
        }

        //sensors[7] = transform.position.x / wallLimitX;
        //sensors[8] = transform.position.y / wallLimitY;
    }
}
