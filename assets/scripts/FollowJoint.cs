using UnityEngine;

public class FollowJoint : MonoBehaviour
{
    [Header("Fonte de dados")]
    public KinectBodyReader reader;

    public enum JointToFollow
    {
        Head,
        HandRight,
        HandLeft,
        FootRight,
        FootLeft,
        KneeRight,
        KneeLeft,
        HipRight,
        HipLeft,
        SpineBase,
        SpineMid,
        SpineShoulder,
        ShoulderRight,
        ShoulderLeft
    }

    [Header("Junta a seguir")]
    public JointToFollow joint = JointToFollow.HandRight;

    [Header("Posicionamento")]
    [Tooltip("Distância fixa no eixo Z em Unity")]
    public float distanceZ = -2f;
    [Tooltip("Escala aplicada às coordenadas X e Y vindas do Kinect")]
    public float scaleXY = 2f;
    [Tooltip("Offset vertical adicional em Unity")]
    public float offsetY = 1f;
    [Tooltip("Velocidade de interpolação para suavizar o movimento")]
    public float smooth = 10f;

    private Vector3 currentPos; // Posição suavizada atual

    [Header("Debug")]
    [Tooltip("Exibe mensagens de depuração para o movimento da junta")]
    public bool enableDebug = false;

    void Start()
    {
        currentPos = transform.position;
    }

    void Update()
    {
        if (!ValidateReaderAndBody()) return;

        Vector3 src = GetJointPosition();

        if (!ValidateJointPosition(src)) return;

        // Calcula nova posição suavizada
        Vector3 target = CalculateTargetPosition(src);

        UpdateObjectPosition(target);

        if (enableDebug)
        {
            Debug.Log($"Posição do objeto atualizado: {transform.position}, Target: {target}");
        }
    }

    private bool ValidateReaderAndBody()
    {
        if (reader == null)
        {
            Debug.LogWarning($"KinectBodyReader ausente em '{gameObject.name}'.");
            return false;
        }

        if (!reader.hasBody)
        {
            Debug.LogWarning($"Nenhum corpo rastreado pelo Kinect no momento.");
            return false;
        }

        return true;
    }

    private Vector3 GetJointPosition()
    {
        switch (joint)
        {
            case JointToFollow.Head: return reader.headPos;
            case JointToFollow.HandRight: return reader.handRightPos;
            case JointToFollow.HandLeft: return reader.handLeftPos;
            case JointToFollow.FootRight: return reader.footRightPos;
            case JointToFollow.FootLeft: return reader.footLeftPos;
            case JointToFollow.KneeRight: return reader.kneeRightPos;
            case JointToFollow.KneeLeft: return reader.kneeLeftPos;
            case JointToFollow.HipRight: return reader.hipRightPos;
            case JointToFollow.HipLeft: return reader.hipLeftPos;
            case JointToFollow.SpineBase: return reader.spineBasePos;
            case JointToFollow.SpineMid: return reader.spineMidPos;
            case JointToFollow.SpineShoulder: return reader.spineShoulderPos;
            case JointToFollow.ShoulderRight: return reader.shoulderRightPos;
            case JointToFollow.ShoulderLeft: return reader.shoulderLeftPos;
            default:
                Debug.LogWarning($"Junta desconhecida selecionada. Usando HandRight por padrão.");
                return reader.handRightPos;
        }
    }

    private Vector3 CalculateTargetPosition(Vector3 src)
    {
        float x = src.x * scaleXY;
        float y = src.y * scaleXY + offsetY;
        float z = distanceZ;

        return new Vector3(x, y, z);
    }

    private bool ValidateJointPosition(Vector3 src)
    {
        if (float.IsNaN(src.x) || float.IsNaN(src.y) || float.IsNaN(src.z))
        {
            Debug.LogWarning($"Valores de posição inválidos para a junta {joint}.");
            return false;
        }

        return true;
    }

    private void UpdateObjectPosition(Vector3 target)
    {
        // Suaviza a transição entre a posição atual e a nova posição alvo
        currentPos = Vector3.Lerp(currentPos, target, Time.deltaTime * smooth);
        transform.position = currentPos;
    }
}
