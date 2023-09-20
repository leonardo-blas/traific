using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SplitScreenCamera : MonoBehaviour
{
    public Camera frontViewCamera;
    public Camera rearViewCamera;
    public Transform kartTransform; // Reference to the kart's transform.

    private Vector3 initialFrontViewCameraOffset;
    private Vector3 initialRearViewCameraOffset;

    private void Start()
    {
        // Store the initial camera offsets relative to the kart.
        initialFrontViewCameraOffset = frontViewCamera.transform.position - kartTransform.position;
        initialRearViewCameraOffset = rearViewCamera.transform.position - kartTransform.position;
    }

    private void Update()
    {
        // Update camera positions based on kart's position.
        Vector3 frontViewCameraPosition = kartTransform.position + initialFrontViewCameraOffset;
        Vector3 rearViewCameraPosition = kartTransform.position + initialRearViewCameraOffset;

        frontViewCamera.transform.position = frontViewCameraPosition;
        rearViewCamera.transform.position = rearViewCameraPosition;
    }
}
