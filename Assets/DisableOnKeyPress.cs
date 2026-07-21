using System.Collections;
using UnityEngine;

public class DisableOnKeyPress : MonoBehaviour
{

    [SerializeField] public bool disableOnStart = false;

    private IEnumerator Start()
    {
        yield return null;
        if (disableOnStart)
            gameObject.SetActive(false);
    }
    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            gameObject.SetActive(false);
        }
    }
}
