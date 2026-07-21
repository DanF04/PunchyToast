using UnityEngine;

public class TAG_Toast : MonoBehaviour
{
    [Header("Lifetime Settings")]
    [Tooltip("Total lifetime before destruction.")]
    [SerializeField] private float lifetime = 5f;

    [Tooltip("How long before destruction the toast starts shrinking.")]
    [SerializeField] private float shrinkDuration = 1f;

    [Header("Trail Settings")]
    [Tooltip("Array of TrailRenderers on this toast or its children.")]
    [SerializeField] private TrailRenderer[] trails;

    [Tooltip("Time in seconds before trails stop emitting.")]
    [SerializeField] private float trailDisableTime = 2.5f;

    private Vector3 initialScale;
    private float timer;
    private bool trailsDisabled;

    private void Start()
    {
        initialScale = transform.localScale;

        // Auto-find trails in children if none assigned in Inspector
        if (trails == null || trails.Length == 0)
        {
            trails = GetComponentsInChildren<TrailRenderer>();
        }
    }

    /// <summary>
    /// Called by ToastSpawner to pass custom lifetime and trail timing settings.
    /// </summary>
    public void InitializeLifetime(float totalLifetime, float trailDuration, float shrinkTime)
    {
        lifetime = totalLifetime;
        trailDisableTime = trailDuration;
        shrinkDuration = shrinkTime;
    }

    private void Update()
    {
        timer += Time.deltaTime;

        // 1. Disable trail emission after trailDisableTime
        if (!trailsDisabled && timer >= trailDisableTime)
        {
            DisableTrails();
            trailsDisabled = true;
        }

        // 2. Handle smooth shrinking before destruction
        float shrinkStartTime = Mathf.Max(0f, lifetime - shrinkDuration);
        if (timer >= shrinkStartTime)
        {
            float shrinkProgress = Mathf.Clamp01((timer - shrinkStartTime) / shrinkDuration);
            transform.localScale = Vector3.Lerp(initialScale, Vector3.zero, shrinkProgress);
        }

        // 3. Destroy object when lifetime finishes
        if (timer >= lifetime)
        {
            Destroy(gameObject);
        }
    }

    private void DisableTrails()
    {
        if (trails == null) return;

        foreach (TrailRenderer trail in trails)
        {
            if (trail != null)
            {
                trail.emitting = false;
            }
        }
    }
}