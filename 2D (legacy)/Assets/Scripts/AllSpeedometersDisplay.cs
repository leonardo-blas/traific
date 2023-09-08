using UnityEngine;

public class SpeedTag : MonoBehaviour
{
    public GameObject nameTag;
    public GameObject objectToFollow;

    void Update()
    {
        if (objectToFollow != null)
        {
            Vector3 objectPosition = Camera.main.WorldToScreenPoint(objectToFollow.transform.position);
            Vector3 offset = new Vector3(0f, 100f, 0f);
            nameTag.transform.position = objectPosition + offset;
        }
    }
}