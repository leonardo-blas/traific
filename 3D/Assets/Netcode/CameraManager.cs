using UnityEngine;
using Unity.Netcode;
 
public class CameraManager : NetworkBehaviour
{
    void Start()
    {
        if (IsOwner)
        {
            GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Cinemachine.CinemachineVirtualCamera>().Follow =
                gameObject.transform;
            GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Cinemachine.CinemachineVirtualCamera>().LookAt =
                gameObject.transform;
        }
    }
}