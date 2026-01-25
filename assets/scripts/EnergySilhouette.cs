using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class EnergySilhouette : MonoBehaviour
{
    [Header("Fonte de tracking")]
    public KinectBodyReader reader;

    [Header("Mapping Kinect -> Ecrã")]
    public float distanceZ = -2f;
    public float scaleXY = 2f;
    public float offsetY = 1f;
    public float smooth = 15f;

    [Header("Forma da energia")]
    [Tooltip("Número de pontos da linha")]
    public int pointCount = 64;
    [Tooltip("Raio horizontal, em função da largura dos ombros")]
    public float radiusAlong = 0.8f;
    [Tooltip("Raio vertical, em função da distância cabeça–anca")]
    public float radiusPerp = 1.1f;

    LineRenderer lr;
    Vector3 currentCenter;

    void Awake()
    {
        lr = GetComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.loop = true;
    }

    void Update()
    {
        if (reader == null || !reader.isTracked)
        {
            if (lr.enabled) lr.enabled = false;
            return;
        }

        if (!lr.enabled) lr.enabled = true;

        // Centro da silhueta no tronco (entre ombros e meio da coluna)
        Vector3 spine = reader.spineMidPos;
        Vector3 spineBase = reader.spineBasePos;
        Vector3 head = reader.headPos;
        Vector3 shoulderL = reader.shoulderLeftPos;
        Vector3 shoulderR = reader.shoulderRightPos;

        // Converter para o mesmo plano que os outros FX (tipo FollowJoint)
        Vector3 src = spine;
        float x = src.x * scaleXY;
        float y = src.y * scaleXY + offsetY;
        float z = distanceZ;

        Vector3 targetCenter = new Vector3(x, y, z);
        currentCenter = Vector3.Lerp(currentCenter, targetCenter, Time.deltaTime * smooth);

        // Medidas do corpo para adaptar a silhueta
        float shouldersWidth = Vector3.Distance(shoulderL, shoulderR) * scaleXY;
        float bodyHeight = (head.y - spineBase.y) * scaleXY;

        float rx = Mathf.Max(shouldersWidth * radiusAlong, 0.3f);
        float ry = Mathf.Max(bodyHeight * 0.5f * radiusPerp, 0.6f);

        if (pointCount < 3) pointCount = 3;
        lr.positionCount = pointCount;

        float step = Mathf.PI * 2f / pointCount;

        for (int i = 0; i < pointCount; i++)
        {
            float a = step * i;

            // elipse vertical à volta do corpo
            float px = currentCenter.x + Mathf.Cos(a) * rx;
            float py = currentCenter.y + Mathf.Sin(a) * ry;
            float pz = currentCenter.z;

            lr.SetPosition(i, new Vector3(px, py, pz));
        }
    }
}


