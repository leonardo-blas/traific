using Unity.Netcode;
using UnityEngine;

public class StartServer : MonoBehaviour
{
    void Start()
    {
        NetworkManager.Singleton.StartServer();

    }
}