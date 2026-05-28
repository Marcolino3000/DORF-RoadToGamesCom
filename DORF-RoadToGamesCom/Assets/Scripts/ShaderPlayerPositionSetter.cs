using System;
using UnityEngine;

public class ShaderPlayerPositionSetter : MonoBehaviour
{
    [SerializeField] private Material material;
    [SerializeField] private Transform playerTransform;
    private void Update()
    {
        Debug.Log(playerTransform.position.z);
        material.SetFloat("PlayerZ", playerTransform.position.z);
    }
}
