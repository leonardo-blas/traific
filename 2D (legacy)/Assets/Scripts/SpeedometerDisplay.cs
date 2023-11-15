using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;

public class SpeedometerDisplay : MonoBehaviour
{
    public static SpeedometerDisplay Instance;
    public Text speedText;
    
    void Awake()
    {
        Instance = this;
    }

    public void Display(float speed)
    {
        // Round to two decimal places.
        speedText.text = speed.ToString("F1");
    }
}