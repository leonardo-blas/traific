using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PathFollower : MonoBehaviour
{
    Node[] PathNode;
    public GameObject Car;
    public float moveSpeed;
    float Timer;
    int CurrentNode;
    static Vector3 CurrentPositionHolder;
    // Start is called before the first frame update
    void Start()
    {
        PathNode = GetComponentsInChildren<Node>();
        CheckNode();
    }
    void CheckNode()
    {
        Timer = 0;
        CurrentPositionHolder = PathNode[CurrentNode].transform.position;

    }
    // Update is called once per frame
    void Update()
    {
        Debug.Log(CurrentNode);
        Timer += Time.deltaTime * moveSpeed;
        if(Car.transform.position != CurrentPositionHolder)
        {
            Car.transform.position = Vector3.Lerp(Car.transform.position, CurrentPositionHolder, Timer);
        }
        else
        {
            CurrentNode++;
            CheckNode();
        }
    }
}
