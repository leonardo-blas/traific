using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class SpawnPlayers : MonoBehaviour
{
    public GameObject playerPrefab;

    public float minX;
    public float minY;
    public float maxX;
    public float maxY;
    // Start is called before the first frame update
    void Start()
    {
        
        //Vector2 randomPosition = new Vector2(Random.Range(minX, maxX), Random.Range(minY, maxY));
        Vector2 randomPosition = new Vector2(-5.77f, -2.45f);
        PhotonNetwork.Instantiate(playerPrefab.name, randomPosition, Quaternion.identity);
        Debug.Log("Player Spawned");
    }


    
}
