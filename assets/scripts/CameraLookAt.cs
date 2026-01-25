using UnityEngine;

public class CameraLookAt : MonoBehaviour
{
    public Transform target;

    void LateUpdate()
    {
        if (target == null) return;
        transform.LookAt(target);
    }
}
