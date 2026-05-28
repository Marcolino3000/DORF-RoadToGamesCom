using UnityEngine;

public class WwiseRTPCTester : MonoBehaviour
{
    [SerializeField] [Range(0f, 1f)]
    private float value = 0f;
    [SerializeField]
    private float min = 0f;
    [SerializeField]
    private float max = 1f;

    [SerializeField]
    private AK.Wwise.RTPC rtpc = null;


    void OnValidate()
    {
        
        rtpc.SetGlobalValue(Mathf.Lerp(min, max, value));
    }
}
