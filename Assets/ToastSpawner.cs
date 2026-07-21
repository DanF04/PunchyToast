using System.Collections;
using UnityEngine;

public class ToastSpawner : MonoBehaviour
{
    [Header("Prefab Settings")]
    [Tooltip("The toast prefab to spawn.")]
    [SerializeField] private GameObject toastPrefab;

    [Header("Spawn Timing")]
    [SerializeField] private float minSpawnDelay = 1.5f;
    [SerializeField] private float maxSpawnDelay = 3.5f;

    [Header("Floatiness / Air Mechanics")]
    [SerializeField] private float linearDrag = 1.2f;
    [SerializeField] private float angularDrag = 0.8f;

    [Tooltip("Custom gravity scale for floaty physics (0.35 = 35% normal gravity).")]
    [SerializeField] private float floatyGravityScale = 0.35f;

    [Header("Arc Launch Force")]
    [SerializeField] private float minUpwardForce = 4f;
    [SerializeField] private float maxUpwardForce = 7f;
    [SerializeField] private float minHorizontalForce = 1.5f;
    [SerializeField] private float maxHorizontalForce = 3.5f;

    [Header("Spin / Torque")]
    [SerializeField] private float minTorque = -100f;
    [SerializeField] private float maxTorque = 100f;

    [Header("Lifespan & Cleanup")]
    [Tooltip("Total lifetime of the toast before destruction.")]
    [SerializeField] private float toastLifetime = 8f;

    [Tooltip("Time in seconds before trail emission stops.")]
    [SerializeField] private float trailDisableTime = 3.5f;

    [Tooltip("How many seconds before destruction the toast shrinks to zero.")]
    [SerializeField] private float shrinkDuration = 1.2f;

    private void Start()
    {
        StartCoroutine(SpawnRoutine());
    }

    private IEnumerator SpawnRoutine()
    {
        while (true)
        {
            float delay = Random.Range(minSpawnDelay, maxSpawnDelay);
            yield return new WaitForSeconds(delay);

            SpawnAndThrowToast();
        }
    }

    private void SpawnAndThrowToast()
    {
        if (toastPrefab == null)
            return;

        // 1. Spawn at spawner position
        GameObject newToast = Instantiate(toastPrefab, transform.position, Quaternion.identity);

        // 2. Setup TAG_Toast
        TAG_Toast toastComponent = newToast.GetComponent<TAG_Toast>();
        if (toastComponent == null)
        {
            toastComponent = newToast.AddComponent<TAG_Toast>();
        }

        toastComponent.InitializeLifetime(toastLifetime, trailDisableTime, shrinkDuration);

        // 3. Setup Rigidbody for physics
        Rigidbody rb = newToast.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = newToast.AddComponent<Rigidbody>();
        }

        rb.linearDamping = linearDrag;
        rb.angularDamping = angularDrag;
        rb.useGravity = false; // Turn off Unity's default heavy gravity
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // 4. Launch Impulse
        float horizontalDirection = Random.value < 0.5f ? -1f : 1f;
        float sideForce = Random.Range(minHorizontalForce, maxHorizontalForce) * horizontalDirection;
        float upwardForce = Random.Range(minUpwardForce, maxUpwardForce);

        Vector3 arcImpulse = new Vector3(sideForce, upwardForce, 0f);
        rb.AddForce(arcImpulse, ForceMode.Impulse);

        // 5. Spin Torque
        Vector3 randomTorque = new Vector3(
            Random.Range(minTorque, maxTorque),
            Random.Range(minTorque, maxTorque),
            Random.Range(minTorque, maxTorque)
        );
        rb.AddTorque(randomTorque);

        // 6. Apply reduced gravity continuously via coroutine directly on the spawner
        StartCoroutine(ApplyCustomGravity(rb, floatyGravityScale));
    }

    private IEnumerator ApplyCustomGravity(Rigidbody rb, float gravityScale)
    {
        WaitForFixedUpdate waitForFixedUpdate = new WaitForFixedUpdate();

        while (rb != null)
        {
            // Applies lighter upward/downward floaty gravity every physics frame
            rb.AddForce(Physics.gravity * gravityScale, ForceMode.Acceleration);
            yield return waitForFixedUpdate;
        }
    }
}