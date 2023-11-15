using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class CircleController : MonoBehaviour
{
    [SerializeField]
    private Transform[] waypoints;
    [SerializeField]
    private float moveSpeed = 2f;
    private int waypointIndex = 0;
    //PhotonView view;

    // Start is called before the first frame update
    [SerializeField]
    private void Start()
    {
        //view = GetComponent<PhotonView>();
        transform.position = waypoints[waypointIndex].transform.position;
    }

    // Update is called once per frame
    void FixedUpdate()
    {

        //Debug.Log("ENEMY MOVING");
        Move();
          
        
        
    }
    private void Move()
    {
        if(waypointIndex <= waypoints.Length - 1)
        {
            transform.position = Vector2.MoveTowards(transform.position, waypoints[waypointIndex].transform.position, moveSpeed * Time.deltaTime);
            if(transform.position == waypoints[waypointIndex].transform.position)
            {
                //update waypoint
                waypointIndex += 1;
                //go back to the original waypoint 
                if (waypointIndex == waypoints.Length )
                {
                    waypointIndex = 0;
                }
            }
        }
    }
}
