using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class CarScript : MonoBehaviour
{
    Rigidbody2D rb;
    PhotonView view;
    float maxSpeed = 2f;

    void Awake()
    {
       rb = GetComponent<Rigidbody2D>();
    }
    
    void Start()
    {
        view = GetComponent<PhotonView>();
    }

    void FixedUpdate()
    {
        if (view.IsMine)
        {
            rb.velocity = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical")) * maxSpeed;
        }
    }
}

