using System;
using UnityEngine;

[ExecuteInEditMode]
public class SortModeSetter : MonoBehaviour
{
    [SerializeField] private Camera cam;
    [SerializeField] private TransparencySortMode sortMode;
    [SerializeField] private Vector3 sortAxis = Vector3.forward;
    
    private void SetSortMode()
    {
        cam.transparencySortMode = sortMode;

        sortAxis = sortAxis.normalized;
        cam.transparencySortAxis = sortAxis;
    }

    private void OnValidate()
    {
        SetSortMode();
    }

    private void Awake()
    {
        SetSortMode();
    }

    private void Reset()
    {
        cam = GetComponent<Camera>();
    }
}
