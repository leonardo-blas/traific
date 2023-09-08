using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using TMPro;

public class AllSpeedometers : MonoBehaviourPun, IPunObservable
{
    private Rigidbody2D remoteRb;
    public TextMeshProUGUI remoteSpeedText;
    private float remoteSpeed;

    void Start()
    {
        remoteRb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        // Update the remote player's speed text.
        if (!photonView.IsMine && PhotonNetwork.IsConnected)
        {
            if (remoteSpeedText != null)
            {
                remoteSpeedText.text = (Mathf.Round(remoteSpeed * 10f) / 10f).ToString();
            }
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        // Synchronize remote player's speed across the network.
        if (stream.IsWriting)
        {
            stream.SendNext(remoteRb.velocity.magnitude);
        }

        else if (stream.IsReading)
        {
            remoteSpeed = (float)stream.ReceiveNext();
        }
    }
}
