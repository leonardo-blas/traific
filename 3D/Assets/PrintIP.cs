using System.Net.NetworkInformation;
using TMPro;
using UnityEngine;

public class PrintIP : MonoBehaviour
{
    public TextMeshProUGUI ipAddressText;

    private void Start()
    {
        // Get the local IPv4 address
        string ipAddress = GetLocalIPv4();

        // Update the TextMeshPro text
        ipAddressText.text = ipAddress;
    }

    private string GetLocalIPv4()
    {
        string ipAddress = "N/A";

        foreach (var netInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (netInterface.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ||
                netInterface.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
            {
                foreach (var addrInfo in netInterface.GetIPProperties().UnicastAddresses)
                {
                    if (addrInfo.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        ipAddress = addrInfo.Address.ToString();
                        break;
                    }
                }
            }
        }

        return ipAddress;
    }
}