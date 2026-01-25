using UnityEngine;

public class FollowHand : MonoBehaviour
{
    public KinectBodyReader reader;
    public float distanceZ = -2f;
    public float scaleXY = 2f;
    public float offsetY = 1f;
    public float smooth = 10f;   // maior = mais suave

    Vector3 _currentPos;

    void Start()
    {
        _currentPos = transform.position;
    }

    void Update()
    {
        if (reader == null) return;

        var src = reader.handRightPos;

        float x = src.x * scaleXY;
        float y = src.y * scaleXY + offsetY;
        float z = distanceZ;

        Vector3 target = new Vector3(x, y, z);

        // suaviza o movimento
        _currentPos = Vector3.Lerp(_currentPos, target, Time.deltaTime * smooth);
        transform.position = _currentPos;
    }
}
