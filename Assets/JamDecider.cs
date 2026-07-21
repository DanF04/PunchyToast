using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine.UI;
using UnityEngine.Audio;

public enum JamFlavor { None, Butter, StrawberryJam, GrapeJam, PeanutButter, Random }

public class JamDecider : MonoBehaviour
{
    public static JamDecider Instance;

    [System.Serializable]
    public struct JamType
    {
        public string name;
        public JamFlavor flavor;
        public Color jamColor;
        public Transform dippingStation;
        public GameObject numberSprite;
        public Outline outline;
    }

    [Header("Settings")]
    public GameObject armPrefab;
    public List<JamType> allAvailableJams;
    public List<JamType> activeJams;

    [Header("Animation & Cooldown")]
    public float dipDepth = 0.8f;
    public float dipDuration = 0.15f;
    public float zOffset = 2.0f;
    [SerializeField] private float dipCooldown = 0.5f;
    private float lastDipTime = -10f;

    public int currentJamIndex = 0;

    [SerializeField] private GameObject butterFist;
    [SerializeField] private GameObject stawberryFist;
    [SerializeField] private GameObject grapeFist;
    [SerializeField] private GameObject peanutFist;

    [SerializeField] private AudioMixer sfxMixer;
    [SerializeField] private AudioClip[] dipSounds;

    public bool alreadyDipped = false;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        currentJamIndex = -1;
        lastDipTime = -10f;

        
    }

    public void ResetJams()
    {
        foreach (var jam in allAvailableJams)
        {
            if (jam.dippingStation != null)
                jam.dippingStation.gameObject.SetActive(false);
        }
    }

    public void SetupLevelJams(HashSet<JamFlavor> requiredFlavors)
    {
        activeJams = new List<JamType>();

        foreach (var jam in allAvailableJams)
        {
            if (requiredFlavors.Contains(jam.flavor))
            {
                activeJams.Add(jam);
                if (jam.dippingStation != null)
                    jam.dippingStation.gameObject.SetActive(true);

                // Ensure number sprites start visible
                if (jam.numberSprite != null)
                    jam.numberSprite.SetActive(true);
            }
            else
            {
                if (jam.dippingStation != null)
                    jam.dippingStation.gameObject.SetActive(false);
            }
        }

        if (!alreadyDipped && activeJams.Count > 0)
        {
            // Select the first active jam by default
            SelectByFlavor(activeJams[0].flavor, true);
        }
    }

    void Update()
    {
        // Fixed Keybindings: Butter = 1, Strawberry = 2, Grape = 3, Peanut = 4
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            SelectByFlavor(JamFlavor.Butter);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            SelectByFlavor(JamFlavor.StrawberryJam);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            SelectByFlavor(JamFlavor.GrapeJam);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            SelectByFlavor(JamFlavor.PeanutButter);
        }
    }

    void SelectJam(int index, bool bypassCooldown = false)
    {
        alreadyDipped = true;

        if (activeJams == null || index < 0 || index >= activeJams.Count) return;
        if (index == currentJamIndex) return;

        if (!bypassCooldown && Time.time < lastDipTime + dipCooldown) return;

        lastDipTime = Time.time;

        // Reactivate ALL number sprites first
        for (int i = 0; i < activeJams.Count; i++)
        {
            if (activeJams[i].numberSprite != null)
                activeJams[i].numberSprite.SetActive(true);
        }

        currentJamIndex = index;

        // Disable the selected jam's number sprite
        if (activeJams[index].numberSprite != null)
            activeJams[index].numberSprite.SetActive(false);

        PerformDipAnimation(activeJams[index]);

        // Visual Updates (fists)
        butterFist.SetActive(false);
        stawberryFist.SetActive(false);
        grapeFist.SetActive(false);
        peanutFist.SetActive(false);

        switch (activeJams[index].flavor)
        {
            case JamFlavor.Butter: butterFist.SetActive(true); break;
            case JamFlavor.StrawberryJam: stawberryFist.SetActive(true); break;
            case JamFlavor.GrapeJam: grapeFist.SetActive(true); break;
            case JamFlavor.PeanutButter: peanutFist.SetActive(true); break;
        }
    }

    public void SelectByFlavor(JamFlavor flavor, bool bypassCooldown = false)
    {
        for (int i = 0; i < activeJams.Count; i++)
        {
            if (activeJams[i].flavor == flavor)
            {
                SelectJam(i, bypassCooldown);
                return;
            }
        }
    }

    /// <summary>
    /// Call this from the client with the desired condiment string name or flavor string.
    /// Blinks the Outline component's OutlineWidth on the matching jam's dipping station back and forth 10 times, finishing at 0.
    /// </summary>
    public void BlinkOutline(string condimentName)
    {
        foreach (var jam in allAvailableJams)
        {
            // Matches against either jam.name or jam.flavor enum string
            if (jam.name.Equals(condimentName, System.StringComparison.OrdinalIgnoreCase) ||
                jam.flavor.ToString().Equals(condimentName, System.StringComparison.OrdinalIgnoreCase))
            {
                if (jam.dippingStation == null) return;

                // Retrieve all Outline components on the dipping station and all of its children
                Outline[] outlineComponents = jam.dippingStation.GetComponentsInChildren<Outline>(true);
                if (outlineComponents == null || outlineComponents.Length == 0) return;

                foreach (var outlineComponent in outlineComponents)
                {
                    if (outlineComponent == null) continue;

                    outlineComponent.enabled = true;

                    // Kill any existing tweens on this specific outline component
                    DOTween.Kill(outlineComponent);

                    // Initial reset to 0
                    outlineComponent.OutlineWidth = 0f;

                    // 0 -> 10 over 0.2s, repeated 10 times with Yoyo (0 -> 10 -> 0 -> 10 ...)
                    DOTween.To(() => outlineComponent.OutlineWidth, x => outlineComponent.OutlineWidth = x, 10f, 0.2f)
                        .SetLoops(10, LoopType.Yoyo)
                        .SetEase(Ease.InOutSine)
                        .SetTarget(outlineComponent)
                        .OnComplete(() =>
                        {
                            if (outlineComponent != null)
                            {
                                outlineComponent.OutlineWidth = 0f;
                                outlineComponent.enabled = false;
                            }
                        }); // Safeguard reset to guaranteed 0
                }

                break;
            }
        }
    }

    void PerformDipAnimation(JamType jam)
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySound(dipSounds, sfxMixer, jam.dippingStation.position);

        Vector3 spawnPos = jam.dippingStation.position + new Vector3(0, 0, -zOffset);
        GameObject dippingArm = Instantiate(armPrefab, spawnPos, Quaternion.identity);

        float screenX = Camera.main.WorldToViewportPoint(dippingArm.transform.position).x;
        if (screenX > 0.5f)
        {
            dippingArm.transform.localScale = Vector3.Scale(dippingArm.transform.localScale, new Vector3(-1, 1, 1));
        }

        Vector3 originalScale = dippingArm.transform.localScale;
        dippingArm.transform.localScale = Vector3.zero;

        Sequence dipSeq = DOTween.Sequence();
        float targetZ = jam.dippingStation.position.z + dipDepth;

        dipSeq.Append(dippingArm.transform.DOScale(originalScale, 0.15f).SetEase(Ease.OutBack));
        dipSeq.Append(dippingArm.transform.DOMoveZ(targetZ, dipDuration).SetEase(Ease.InQuad));
        dipSeq.Append(dippingArm.transform.DOMoveZ(spawnPos.z, dipDuration).SetEase(Ease.OutQuad));
        dipSeq.Append(dippingArm.transform.DOScale(Vector3.zero, 0.15f).SetEase(Ease.InBack));
        dipSeq.OnComplete(() => Destroy(dippingArm));
    }

    public string GetCurrentJamName()
    {
        if (activeJams.Count == 0 || currentJamIndex < 0) return "None";

        if (currentJamIndex >= activeJams.Count)
        {
            SelectByFlavor(activeJams[0].flavor, true);
        }

        return activeJams[currentJamIndex].flavor.ToString();
    }

    public Color GetCurrentJamColor()
    {
        if (activeJams.Count == 0 || currentJamIndex < 0) return Color.white;
        return activeJams[currentJamIndex].jamColor;
    }

    public Color GetColorFromFlavor(JamFlavor flavor)
    {
        foreach (var j in allAvailableJams)
            if (j.flavor == flavor) return j.jamColor;
        return Color.white;
    }
}