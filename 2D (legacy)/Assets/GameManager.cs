using UnityEngine;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    //List<Transform> _wayPoints = new List<Transform>();
    [SerializeField]
    private Transform[] _wayPoints;
    //public List<Transform> Waypoints { get { return _wayPoints; } set { _wayPoints = value; } }
    public Transform[] Waypoints { get { return _wayPoints; } set { _wayPoints = value; } }
    static GameManager _instance;
    public static GameManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType(typeof(GameManager)) as GameManager;
            }
            return _instance;
        }
        set { _instance = value; }
    }

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        // Initialise waypoints here
        // _waypoints =
    }
}
