using UnityEngine;
using Photon.Pun;

public class Speedometer : MonoBehaviour
{
    Rigidbody2D rb;
    PhotonView view;

    void Start()
    {
        view = GetComponent<PhotonView>();
        rb = GetComponent<Rigidbody2D>();
    }

    void FixedUpdate()
    {
        if (view.IsMine)
        {
            // Pass speed (default is ms-1) to the SpeedometerDisplay.cs.
            SpeedometerDisplay.Instance.Display(rb.velocity.magnitude);
        }
    }
}