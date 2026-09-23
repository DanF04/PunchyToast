using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections.Generic;
using UnityEngine.Audio;

public class LevelComplete : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image[] stars;
    [SerializeField] private Sprite filledStar;
    [SerializeField] private Sprite emptyStar;
    [SerializeField] private TextMeshProUGUI timeText;

    [Header("Animation Settings")]
    [SerializeField] private float starPopDuration = 0.3f;
    [SerializeField] private float delayBetweenStars = 0.15f;

    [Header("5 Star Final Pop Animation")]
    [Tooltip("How much bigger each star gets during the final pop.")]
    [SerializeField] private float finalPopStrength = 0.25f;

    [Tooltip("How long each final pop lasts.")]
    [SerializeField] private float finalPopDuration = 0.25f;

    [Tooltip("Delay between each pop from star 5 to star 1.")]
    [SerializeField] private float delayBetweenFinalPops = 0.05f;

    [Tooltip("Small delay after the fifth star appears before the reverse pop starts.")]
    [SerializeField] private float delayBeforeFinalPops = 0.1f;

    [Header("Star Sound Settings")]
    [SerializeField] private AudioMixer sfxMixer;

    [Tooltip("Sounds used for stars 1 to 4.")]
    [SerializeField] private AudioClip[] starPopSounds;

    [Tooltip("Special sound used for the fifth star.")]
    [SerializeField] private AudioClip fifthStarSound;

    [Tooltip("Pitch of the first star.")]
    [SerializeField] private float startingPitch = 1f;

    [Tooltip("How much the pitch increases for each consecutive star.")]
    [SerializeField] private float pitchIncreasePerStar = 0.1f;

    private Vector3 panelOriginalScale;
    private Vector3 timeOriginalScale;

    private Dictionary<int, Vector3> starScales =
        new Dictionary<int, Vector3>();

    public void Initialize(int starNumber, float totalTime)
    {
        // Store original scales
        panelOriginalScale = transform.localScale;
        timeOriginalScale = timeText.transform.localScale;

        starScales.Clear();

        // Prepare stars
        for (int i = 0; i < stars.Length; i++)
        {
            starScales[i] = stars[i].transform.localScale;

            // Filled if earned, empty if not earned
            stars[i].sprite =
                i < starNumber ? filledStar : emptyStar;

            // Hide before animation starts
            stars[i].transform.localScale = Vector3.zero;
        }

        // Hide panel and time before animation
        transform.localScale = Vector3.zero;
        timeText.transform.localScale = Vector3.zero;

        timeText.gameObject.SetActive(true);

        // Format completion time
        int minutes = Mathf.FloorToInt(totalTime / 60);
        int seconds = Mathf.FloorToInt(totalTime % 60);

        timeText.text = $"{minutes:00}m:{seconds:00}s";

        // Create animation sequence
        Sequence finishSeq = DOTween.Sequence();

        // Main panel appears
        finishSeq.Append(
            transform.DOScale(panelOriginalScale, 0.5f)
                .SetEase(Ease.OutBack)
        );

        // Stars appear one by one
        for (int i = 0; i < stars.Length; i++)
        {
            int index = i;

            // Play sound only for earned stars
            if (index < starNumber)
            {
                finishSeq.AppendCallback(() =>
                {
                    if (AudioManager.Instance == null)
                        return;

                    if (sfxMixer == null)
                        return;

                    AudioClip clip;
                    float pitch;

                    // Fifth star:
                    // special sound and normal pitch
                    if (index == 4 && fifthStarSound != null)
                    {
                        clip = fifthStarSound;
                        pitch = 1f;
                    }
                    else
                    {
                        if (starPopSounds == null ||
                            starPopSounds.Length == 0)
                        {
                            return;
                        }

                        clip = starPopSounds[
                            Random.Range(0, starPopSounds.Length)
                        ];

                        // Stars 1-4 progressively increase in pitch
                        pitch =
                            startingPitch +
                            (pitchIncreasePerStar * index);
                    }

                    AudioManager.Instance.PlaySoundFixedPitch(
                        clip,
                        pitch,
                        sfxMixer
                    );
                });
            }

            // Animate star appearing
            finishSeq.Append(
                stars[index].transform
                    .DOScale(
                        starScales[index],
                        starPopDuration
                    )
                    .SetEase(Ease.OutBack)
            );

            // Extra rotation punch for earned stars
            if (index < starNumber)
            {
                finishSeq.Join(
                    stars[index].transform.DOPunchRotation(
                        new Vector3(0f, 0f, 15f),
                        starPopDuration
                    )
                );
            }

            // Delay before next star
            finishSeq.AppendInterval(delayBetweenStars);
        }

        // -------------------------------------------------
        // 5 STAR SPECIAL ANIMATION
        // Star 5 -> 4 -> 3 -> 2 -> 1
        // -------------------------------------------------
        if (starNumber >= 5 && stars.Length >= 5)
        {
            finishSeq.AppendInterval(delayBeforeFinalPops);

            for (int i = 4; i >= 0; i--)
            {
                int index = i;

                // Scale up and return to original scale
                finishSeq.Append(
                    stars[index].transform.DOPunchScale(
                        starScales[index] * finalPopStrength,
                        finalPopDuration,
                        1,
                        0.5f
                    )
                );

                finishSeq.AppendInterval(delayBetweenFinalPops);
            }
        }

        // Show completion time last
        finishSeq.Append(
            timeText.transform
                .DOScale(timeOriginalScale, 0.4f)
                .SetEase(Ease.OutBack)
        );
    }
}