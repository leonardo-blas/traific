using Unity.Netcode;
using UnityEngine;

public class StartClient : MonoBehaviour
{
    void Start()
    {
        NetworkManager.Singleton.StartClient();

    }
}