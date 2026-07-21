using UnityEngine;
using DG.Tweening;

public class PopAndSpin : MonoBehaviour
{
    [Header("Animation Settings")]
    [SerializeField] private float duration = 0.4f;
    [SerializeField] private float punchScaleAmount = 1.2f; // Slight overshoot for "squash & stretch"
    [SerializeField] private float spinRotations = 1.0f;    // Number of full 360-degree spins
    [SerializeField] private Ease scaleEase = Ease.OutBack;

    private Vector3 targetScale;

    void Awake()
    {
        // Cache the scale you set in the editor
        targetScale = transform.localScale;
    }

    private void Start()
    {
        // Force scale to zero instantly so it's hidden before spawning
        transform.localScale = Vector3.zero;
        transform.rotation = Quaternion.identity; // Optional: reset rotation if needed

        PlaySpawnAnimation();
    }

    void OnEnable()
    {
        // Force scale to zero instantly so it's hidden before spawning
        transform.localScale = Vector3.zero;
        transform.rotation = Quaternion.identity; // Optional: reset rotation if needed

        PlaySpawnAnimation();
    }

    void PlaySpawnAnimation()
    {
        // Kill any existing tweens to prevent overlapping conflicts
        transform.DOKill();

        // 1. Scale up from 0 to full size with a bouncy "OutBack" ease
        transform.DOScale(targetScale, duration).SetEase(scaleEase);

        // 2. Perform a snappy cartoony spin on the Z-axis (or Y-axis if 3D)
        // Adjust Vector3.forward to Vector3.up if your object is full 3D and you want a horizontal spin
        transform.DORotate(new Vector3(0, 0, 360f * spinRotations), duration, RotateMode.LocalAxisAdd)
            .SetEase(Ease.OutQuad);
    }

    void OnDisable()
    {
        // Clean up tweens when disabled
        transform.DOKill();
    }
}