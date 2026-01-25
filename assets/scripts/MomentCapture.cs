using UnityEngine;
using System;
using System.Collections;
using System.IO;

public class MomentCapture : MonoBehaviour
{
    [Header("Referências")]
    public ActivationManager activationManager;

    [Header("Configuração da captura")]
    [Tooltip("Subpasta onde as imagens vão ser guardadas")]
    public string folderName = "BMW_Captures";

    [Tooltip("Resolução extra no modo ScreenCapture. 1 = resolução do jogo")]
    [Range(1, 4)]
    public int superSize = 1;

    [Tooltip("Tempo máximo (segundos) para esperar que o ficheiro seja escrito no disco.")]
    public float maxWaitForFileSeconds = 4.0f;

    [Tooltip("Tamanho mínimo esperado do PNG (bytes), para validar que não está vazio.")]
    public long minValidFileBytes = 25_000;

    [Header("Estabilidade antes da captura")]
    [Tooltip("Frames extra a esperar depois do Momento começar, para garantir framing e FX estabilizados.")]
    [Range(0, 10)]
    public int captureDelayFrames = 2;

    [Tooltip("Delay extra em segundos (além dos frames), se quiseres dar margem ao ‘momentSafeZoom’ assentar.")]
    [Range(0f, 1.5f)]
    public float captureDelaySeconds = 0.0f;

    [Header("Modo Premium, câmara dedicada (não herda zoom)")]
    [Tooltip("Se true, usa uma captureCamera dedicada e grava PNG via RenderTexture. Recomendado para manter framing fixo.")]
    public bool useDedicatedCaptureCamera = true;

    [Tooltip("A câmara que vai renderizar para o PNG. Ideal: uma câmara só para captura.")]
    public Camera captureCamera;

    [Tooltip("Pivot do carro (ou um empty no centro do carro). A captureCamera vai olhar para aqui.")]
    public Transform capturePivot;

    [Tooltip("Distância fixa da captureCamera ao pivot. Isto define o framing da foto.")]
    public float captureDistance = 4.2f;

    [Tooltip("Offset local ao pivot (para subir um pouco o enquadramento).")]
    public Vector3 capturePivotOffset = new Vector3(0f, 0.15f, 0f);

    [Tooltip("Ângulo (yaw) relativo ao pivot para a posição da câmara, em graus.")]
    public float captureYaw = 0f;

    [Tooltip("Ângulo (pitch) relativo, em graus.")]
    public float capturePitch = 6f;

    [Tooltip("Se true, força o aspecto para o ecrã. Se false, usa captureWidth/Height.")]
    public bool captureUseScreenResolution = true;

    [Tooltip("Largura do PNG no modo câmara dedicada (se captureUseScreenResolution for false).")]
    public int captureWidth = 1920;

    [Tooltip("Altura do PNG no modo câmara dedicada (se captureUseScreenResolution for false).")]
    public int captureHeight = 1080;

    [Header("Integração com UI Uploading (opcional)")]
    [Tooltip("Se true, chama activationManager.EnterUploadingState() ao iniciar, e CompleteUpload() ao terminar.")]
    public bool notifyActivationManagerUploading = false;

    [Header("Sessão atual")]
    [Tooltip("Identificador único da participante, pode ser colado aqui pelo staff")]
    public string sessionId;

    [Header("Debug")]
    public bool verboseLogs = true;

    public event Action<string, string> OnCaptureSaved; // (sessionId, fullPath)
    public event Action<string> OnCaptureFailed;        // error

    private bool isCapturing = false;

    void Awake()
    {
        InitializeMomentCapture();
    }

    void OnDestroy()
    {
        UnsubscribeFromManagerEvents();
    }

    // ===================== Inicialização =====================
    private void InitializeMomentCapture()
    {
        string folderPath = GetCaptureFolderPath();
        if (verboseLogs)
            Debug.Log($"MomentCapture: Captures path: {folderPath}");

        SubscribeToManagerEvents();

        if (string.IsNullOrWhiteSpace(sessionId))
            GenerateNewSessionId();
        else
            sessionId = sessionId.Trim();
    }

    private void SubscribeToManagerEvents()
    {
        if (activationManager != null)
        {
            activationManager.OnMomentStarted += HandleMomentStarted;
        }
        else
        {
            Debug.LogWarning("MomentCapture: ActivationManager não atribuído.");
        }
    }

    private void UnsubscribeFromManagerEvents()
    {
        if (activationManager != null)
        {
            activationManager.OnMomentStarted -= HandleMomentStarted;
        }
    }

    // ===================== Sessão e Identificador =====================
    public void GenerateNewSessionId()
    {
        sessionId = Guid.NewGuid().ToString("N");
        if (verboseLogs)
            Debug.Log($"MomentCapture: Nova SESSION_ID gerada: {sessionId}");
    }

    public void SetSessionId(string newSessionId)
    {
        if (string.IsNullOrWhiteSpace(newSessionId))
        {
            Debug.LogWarning("MomentCapture: SetSessionId chamado com valor vazio. Ignorado.");
            return;
        }

        sessionId = newSessionId.Trim();
        if (verboseLogs)
            Debug.Log($"MomentCapture: SESSION_ID definido manualmente: {sessionId}");
    }

    // ===================== Captura =====================
    private void HandleMomentStarted()
    {
        if (isCapturing) return;
        StartCoroutine(CaptureCoroutine());
    }

    private IEnumerator CaptureCoroutine()
    {
        isCapturing = true;

        if (notifyActivationManagerUploading && activationManager != null)
        {
            try { activationManager.EnterUploadingState(); } catch { }
        }

        // 1) Espera 1 frame para garantir mudança de estado / FX
        yield return new WaitForEndOfFrame();

        // 2) Espera frames extra configuráveis (para o framing assentar)
        for (int i = 0; i < captureDelayFrames; i++)
            yield return new WaitForEndOfFrame();

        // 3) Delay extra opcional
        if (captureDelaySeconds > 0f)
            yield return new WaitForSeconds(captureDelaySeconds);

        if (string.IsNullOrWhiteSpace(sessionId))
            GenerateNewSessionId();

        string folderPath = GetCaptureFolderPath();
        CreateCaptureFolderIfNeeded(folderPath);

        string timeStamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string fileName = $"{sessionId}_BMW_{timeStamp}.png";
        string fullPath = Path.Combine(folderPath, fileName);

        bool ok;

        if (useDedicatedCaptureCamera)
        {
            ok = CaptureWithDedicatedCamera(fullPath);
        }
        else
        {
            ok = StartScreenCapture(fullPath);
            if (ok)
            {
                bool fileOk = false;
                yield return StartCoroutine(WaitForFileReady(fullPath, result => fileOk = result));
                ok = fileOk;
            }
        }

        if (!ok)
        {
            string err = $"MomentCapture: Falha na captura. path={fullPath}";
            Debug.LogError(err);
            OnCaptureFailed?.Invoke(err);

            if (notifyActivationManagerUploading && activationManager != null)
            {
                try { activationManager.CompleteUpload(false); } catch { }
            }

            isCapturing = false;
            yield break;
        }

        if (verboseLogs)
            Debug.Log($"MomentCapture: Momento capturado. SESSION_ID={sessionId}, file={fullPath}");

        OnCaptureSaved?.Invoke(sessionId, fullPath);

        if (notifyActivationManagerUploading && activationManager != null)
        {
            try { activationManager.CompleteUpload(true); } catch { }
        }

        yield return new WaitForSeconds(0.15f);
        isCapturing = false;
    }

    // ===================== Modo ScreenCapture (fallback) =====================
    private bool StartScreenCapture(string fullPath)
    {
        try
        {
            ScreenCapture.CaptureScreenshot(fullPath, superSize);
            return true;
        }
        catch (Exception ex)
        {
            string err = $"MomentCapture: Falha ao iniciar ScreenCapture. {ex.Message}";
            Debug.LogError(err);
            OnCaptureFailed?.Invoke(err);
            return false;
        }
    }

    private IEnumerator WaitForFileReady(string fullPath, Action<bool> done)
    {
        float t0 = Time.time;

        while (Time.time - t0 <= Mathf.Max(0.2f, maxWaitForFileSeconds))
        {
            if (File.Exists(fullPath))
            {
                try
                {
                    var fi = new FileInfo(fullPath);
                    if (fi.Exists && fi.Length >= minValidFileBytes)
                    {
                        done?.Invoke(true);
                        yield break;
                    }
                }
                catch { }
            }

            yield return null;
        }

        done?.Invoke(false);
    }

    // ===================== Modo Premium (câmara dedicada) =====================
    private bool CaptureWithDedicatedCamera(string fullPath)
    {
        if (captureCamera == null || capturePivot == null)
        {
            if (verboseLogs)
                Debug.LogWarning("MomentCapture: useDedicatedCaptureCamera está ativo, mas falta captureCamera ou capturePivot. A usar ScreenCapture.");
            return StartScreenCapture(fullPath);
        }

        RenderTexture prevActive = RenderTexture.active;
        RenderTexture prevTarget = captureCamera.targetTexture;

        Vector3 prevPos = captureCamera.transform.position;
        Quaternion prevRot = captureCamera.transform.rotation;

        RenderTexture rt = null;
        Texture2D tex = null;

        try
        {
            // Pivot + offset em espaço local do pivot (sem problemas com escala)
            Vector3 pivotPos = capturePivot.position + (capturePivot.rotation * capturePivotOffset);

            Quaternion rot = Quaternion.Euler(capturePitch, captureYaw, 0f);
            Vector3 dir = rot * Vector3.back;

            captureCamera.transform.position = pivotPos + (dir.normalized * captureDistance);
            captureCamera.transform.LookAt(pivotPos);

            int w = captureUseScreenResolution ? Mathf.Max(64, Screen.width * superSize) : Mathf.Max(64, captureWidth);
            int h = captureUseScreenResolution ? Mathf.Max(64, Screen.height * superSize) : Mathf.Max(64, captureHeight);

            rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
            tex = new Texture2D(w, h, TextureFormat.RGBA32, false);

            captureCamera.targetTexture = rt;
            RenderTexture.active = rt;

            captureCamera.Render();

            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply(false, false);

            byte[] png = tex.EncodeToPNG();
            File.WriteAllBytes(fullPath, png);

            if (!File.Exists(fullPath)) return false;

            var fi = new FileInfo(fullPath);
            if (!fi.Exists || fi.Length < minValidFileBytes) return false;

            return true;
        }
        catch (Exception ex)
        {
            string err = $"MomentCapture: Falha na captura com câmara dedicada. {ex.Message}";
            Debug.LogError(err);
            OnCaptureFailed?.Invoke(err);
            return false;
        }
        finally
        {
            // Restore camera
            captureCamera.targetTexture = prevTarget;
            RenderTexture.active = prevActive;

            captureCamera.transform.position = prevPos;
            captureCamera.transform.rotation = prevRot;

            if (tex != null) Destroy(tex);
            if (rt != null) RenderTexture.ReleaseTemporary(rt);
        }
    }

    // ===================== Helpers =====================
    private string GetCaptureFolderPath()
    {
        return Path.Combine(Application.persistentDataPath, folderName);
    }

    private void CreateCaptureFolderIfNeeded(string folderPath)
    {
        if (Directory.Exists(folderPath)) return;

        try
        {
            Directory.CreateDirectory(folderPath);
            if (verboseLogs)
                Debug.Log($"MomentCapture: Pasta de capturas criada: {folderPath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"MomentCapture: Falha ao criar pasta de capturas. {ex.Message}");
        }
    }
}
