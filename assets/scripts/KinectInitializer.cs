using UnityEngine;
using Windows.Kinect;

public class KinectInitializer : MonoBehaviour
{
    [Header("Kinect V2")]
    public KinectSensor sensor;
    public BodyFrameReader bodyReader;

    [Tooltip("Array com os corpos detetados pelo Kinect")]
    public Body[] bodies;

    void Start()
    {
        InitializeKinect();
    }

    void Update()
    {
        if (bodyReader == null) return;

        AcquireBodyFrame();
    }

    void OnApplicationQuit()
    {
        ShutdownKinect();
    }

    void OnDestroy()
    {
        ShutdownKinect();
    }

    // ===================== Inicialização =====================
    private void InitializeKinect()
    {
        // Obter sensor por defeito
        sensor = KinectSensor.GetDefault();

        if (sensor == null)
        {
            Debug.LogError("Nenhum Kinect v2 encontrado! Certifique-se de que o dispositivo está conectado.");
            return;
        }

        // Abrir o leitor de frames de Body
        bodyReader = sensor.BodyFrameSource.OpenReader();
        if (bodyReader == null)
        {
            Debug.LogError("Falha ao abrir BodyFrameReader. Verifique o estado do Kinect.");
            return;
        }

        // Inicializar array de bodies com o BodyCount do sensor
        bodies = new Body[sensor.BodyFrameSource.BodyCount];

        // Abrir o Kinect, se ainda não estiver ativo
        if (!sensor.IsOpen)
        {
            sensor.Open();
        }

        if (sensor.IsOpen)
        {
            Debug.Log("Kinect v2 iniciado com sucesso!");
        }
        else
        {
            Debug.LogError("Falha ao abrir o Kinect v2! Certifique-se de que está instalado corretamente.");
        }
    }

    // ===================== Atualização =====================
    private void AcquireBodyFrame()
    {
        using (var frame = bodyReader.AcquireLatestFrame())
        {
            if (frame == null) return;

            // Garante que o array está criado com o tamanho correto
            if (bodies == null || bodies.Length != sensor.BodyFrameSource.BodyCount)
            {
                bodies = new Body[sensor.BodyFrameSource.BodyCount];
            }

            try
            {
                frame.GetAndRefreshBodyData(bodies);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Erro ao atualizar dados do frame de corpos: {ex.Message}");
            }
        }
    }

    // ===================== Desligamento =====================
    private void ShutdownKinect()
    {
        if (bodyReader != null)
        {
            bodyReader.Dispose();
            bodyReader = null;
        }

        if (sensor != null)
        {
            try
            {
                if (sensor.IsOpen)
                {
                    sensor.Close();
                    Debug.Log("Kinect v2 foi fechado com sucesso.");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Erro ao fechar o Kinect v2: {ex.Message}");
            }

            sensor = null;
        }
    }
}