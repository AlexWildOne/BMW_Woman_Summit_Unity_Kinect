using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class BMWCarController : MonoBehaviour
{
    [Header("Kinect")]
    public KinectBodyReader bodyReader;
    public ActivationManager activationManager;

    [Header("Carro")]
    public Transform carRoot;

    [Tooltip("Se o Kinect estiver a devolver coordenadas espelhadas, ativa isto para corrigir a direção da rotação.")]
    public bool invertRotationDirection = true;

    // ===================== MODOS DA EXPERIÊNCIA =====================
    public enum ExperienceMode
    {
        Showroom,
        Moment
    }

    [Header("Modo da experiência")]
    [Tooltip("Showroom = interação livre. Moment = cinemático, bloqueado.")]
    public ExperienceMode mode = ExperienceMode.Showroom;

    [Header("Controlo por campanha")]
    [Tooltip("Se true, o ActivationManager passa a mandar no modo, via OnInteractionLockedChanged.")]
    public bool driveModeFromActivationManager = true;

    [Tooltip("Quando entra em Moment, fecha a mala.")]
    public bool momentForceTrunkClosed = true;

    [Tooltip("Quando entra em Moment, força faróis OFF.")]
    public bool momentForceHeadlightsOff = true;

    [Tooltip("Quando entra em Moment, bloqueia rotação.")]
    public bool momentBlockRotation = true;

    [Tooltip("Quando entra em Moment, bloqueia zoom.")]
    public bool momentBlockZoom = true;

    [Tooltip("Quando entra em Moment, bloqueia mudança de cor.")]
    public bool momentBlockColor = true;

    [Tooltip("Quando entra em Moment, bloqueia faróis por cabeça.")]
    public bool momentBlockHeadlights = true;

    [Tooltip("Quando entra em Moment, bloqueia mala por joelho.")]
    public bool momentBlockTrunk = true;

    [Header("Momento, framing para captura")]
    [Tooltip("Se true, ao entrar em Moment força um zoom seguro imediatamente (antes da captura).")]
    public bool momentResetZoomOnStart = true;

    [Tooltip("Distância segura para a foto (normalmente o teu zoomMaxDistance ou parecido).")]
    public float momentSafeZoomDistance = 4.2f;

    [Tooltip("Se true, ao entrar em Moment força também uma rotação do carro para um ângulo específico.")]
    public bool momentForceCarRotation = false;

    [Tooltip("Rotação alvo (Y) para a pose do momento, em graus.")]
    public float momentTargetYaw = 0f;

    [Tooltip("Velocidade de aproximação à pose do momento.")]
    public float momentPoseSmooth = 2.2f;

    // ===================== RESET / SHOWROOM =====================
    [Header("Reset quando perde body")]
    public bool resetOnBodyLost = true;
    public float resetCarSmooth = 3.5f;
    public float resetDoorSmooth = 6f;
    public float resetZoomSmooth = 0.25f;

    // ===================== MATERIAIS =====================
    [Header("Carroçaria, Renderers e Materiais")]
    public Renderer[] carPaintRenderers;

    [Tooltip("Índice do material a trocar em cada renderer de carroçaria, se vazio assume 0 para todos")]
    public int[] carPaintMaterialIndices;

    [Tooltip("Opções de cor. Se vazio, não há mudança de cor.")]
    public Material[] colorOptions;

    [Header("Cor original do carro")]
    [Tooltip("Se true, no Start restaura o material e a cor original do carro, em vez de aplicar colorOptions[0].")]
    public bool useOriginalPaintOnStart = true;

    [Tooltip("Se true, o gesto de cor pode alterar materiais e cores.")]
    public bool enableColorGesture = true;

    // Cache do material original por renderer e índice
    private Material[] originalPaintMaterials;
    private Color[] originalPaintColors;
    private bool[] originalHasBaseColor;
    private bool originalCaptured = false;

    // ===================== PERFIL DE DISTÂNCIA =====================
    public enum UserDistanceProfile
    {
        Near_1m,
        Mid_2m,
        Far_3m
    }

    [Header("Perfil de distância do utilizador")]
    public UserDistanceProfile userDistanceProfile = UserDistanceProfile.Far_3m;

    // ===================== ROTACAO POR BRAÇOS =====================
    [Header("Rotação por braços, premium")]
    public float rotationSpeed = 44f;
    public float rotationSensitivity = 1.2f;
    public float armDeadZone = 0.07f;
    public float rotationSmoothTime = 0.10f;
    public float rotationMaxVelocity = 150f;

    [Tooltip("Gating suave, reduz rotação ao caminhar. Se quiseres ainda mais fácil, mete -0.08")]
    public float handAboveShoulderForRotation = -0.03f;

    float rotationVelocity = 0f;
    float rotationVelocityRef = 0f;

    // ===================== ZOOM PREMIUM =====================
    [Header("Zoom premium, bloqueio + mapeamento por abertura de braços")]
    public bool enableZoom = true;

    [Tooltip("Câmara a mover. Se vazio tenta Camera.main")]
    public Camera targetCamera;

    [Tooltip("Pivot do zoom. Se vazio usa carRoot")]
    public Transform zoomPivot;

    [Header("Zoom, limites por perfil")]
    public float zoomMin_1m = 1.3f;
    public float zoomMax_1m = 3.2f;

    public float zoomMin_2m = 1.6f;
    public float zoomMax_2m = 4.0f;

    public float zoomMin_3m = 1.8f;
    public float zoomMax_3m = 4.8f;

    [HideInInspector] public float zoomMinDistance = 1.8f;
    [HideInInspector] public float zoomMaxDistance = 4.8f;

    [Header("Entrada do modo zoom")]
    public float zoomHandsAboveSpine = 0.04f;
    public float zoomHandsInFrontOfSpine = 0.06f;
    public float zoomEntryMinHandsDistance = 0.20f;

    [Header("Lock do zoom")]
    public float zoomLockHoldTime = 1.2f;
    public float zoomLostPoseGrace = 0.25f;

    [Header("Mapeamento humano do zoom")]
    public float humanHandsDistanceMin = 0.22f;
    public float humanHandsDistanceMax = 0.75f;
    public float zoomResponsePower = 1.35f;

    [Header("Suavização do zoom")]
    public float handsDistanceFilter = 0.16f;
    public float zoomSmoothTime = 0.18f;
    public float zoomRangeMultiplier = 1.0f;

    [Header("Zoom, voltar ao original facilmente")]
    [Tooltip("Se true, o baseline do gesto é o zoomMaxDistance, assim fechar as mãos volta sempre ao original.")]
    public bool zoomBaselineUseMaxDistance = true;

    [Tooltip("Zona perto do mínimo das mãos onde o zoom volta com mais força ao baseline.")]
    [Range(0.0f, 0.4f)]
    public float zoomReturnAssistZone = 0.12f;

    [Tooltip("Força do assist de retorno ao baseline quando as mãos ficam muito juntas.")]
    public float zoomReturnAssistStrength = 10f;

    bool zoomLocked = false;
    float zoomLockTimer = 0f;
    float zoomLostPoseTimer = 0f;

    float filteredHandsDist = 0f;
    float filteredHandsDistVel = 0f;

    float desiredDistance = 0f;
    float zoomVelocity = 0f;

    float lockCameraDistance = 0f;
    float lockHandsDistance = 0f;

    float resetZoomVel = 0f;

    // ===================== FARÓIS =====================
    [Header("Faróis pela cabeça")]
    public Renderer[] headlightRenderers;
    public Material headlightOffMaterial;
    public Material headlightOnMaterial;
    public int headlightMaterialIndex = 0;

    public float headTurnThreshold = 0.12f;
    public float headTurnDeadZone = 0.05f;
    bool headlightsOn = false;

    // ===================== MALA =====================
    public enum TrunkRotationMode
    {
        EulerLocal,
        AxisAngleLocal
    }

    [Header("Mala por joelho direito")]
    public Transform[] trunkTransforms;

    [Header("Mala, rotação")]
    [Tooltip("Modo EulerLocal: usa trunkClosedEuler e trunkOpenEuler. Modo AxisAngleLocal: usa trunkAxis e trunkOpenAngle.")]
    public TrunkRotationMode trunkRotationMode = TrunkRotationMode.AxisAngleLocal;

    [Tooltip("Euler local quando a mala está fechada (EulerLocal).")]
    public Vector3 trunkClosedEuler = Vector3.zero;

    [Tooltip("Euler local quando a mala está aberta (EulerLocal). Se abre de lado, este é o teu problema.")]
    public Vector3 trunkOpenEuler = new Vector3(0f, 50f, 0f);

    [Tooltip("Eixo local para abrir a mala (AxisAngleLocal). Normalmente X ou Z, depende do modelo.")]
    public Vector3 trunkAxis = Vector3.right;

    [Tooltip("Ângulo em graus para abrir a mala (AxisAngleLocal). 35 a 70 costuma ser bom.")]
    public float trunkOpenAngle = 55f;

    public float trunkLerpSpeed = 4.5f;
    public bool trunkToggleMode = true;
    bool trunkIsOpen = false;

    // ===================== CORES =====================
    [Header("Mudança de cor")]
    public float colorChangeCooldown = 1.0f;
    public float colorFadeDuration = 1.0f;

    int currentColor = 0;
    float lastColorChangeTime = 0f;
    bool leftWasUp = false;
    bool isColorTransitionRunning = false;

    // ===================== DEBUG =====================
    [Header("Debug")]
    public bool enableKeyboardDebug = true;
    public bool logZoomDebug = false;

    // ===================== ESTADO INICIAL PARA RESET =====================
    Quaternion initialCarRotation;
    Vector3 initialCarPosition;

    void Start()
    {
        if (activationManager != null)
        {
            activationManager.OnMomentStarted += OnMomentStarted;

            if (driveModeFromActivationManager)
                activationManager.OnInteractionLockedChanged += OnInteractionLockedChanged;
        }

        if (targetCamera == null)
            targetCamera = Camera.main;

        if (zoomPivot == null)
            zoomPivot = carRoot;

        if (carRoot != null)
        {
            initialCarRotation = carRoot.rotation;
            initialCarPosition = carRoot.position;
        }

        CaptureOriginalPaintIfNeeded();

        ApplyDistanceProfile();
        SetupInitialZoom();

        SetHeadlights(false);

        ApplyTrunkImmediate(closed: true);

        if (useOriginalPaintOnStart && originalCaptured)
        {
            RestoreOriginalPaint();
        }
        else
        {
            if (colorOptions != null && colorOptions.Length > 0)
                ApplyPaintColorInstant(currentColor);
        }

        ApplyModeSideEffects(force: true);
    }

    void OnDestroy()
    {
        if (activationManager != null)
        {
            activationManager.OnMomentStarted -= OnMomentStarted;

            if (driveModeFromActivationManager)
                activationManager.OnInteractionLockedChanged -= OnInteractionLockedChanged;
        }
    }

    void Update()
    {
        if (!HasBody())
        {
            if (resetOnBodyLost)
                ResetToInitialStateSmooth();

            ResetZoomState();
            return;
        }

        if (mode == ExperienceMode.Showroom)
        {
            HandleRotationByArmsEasyPremium();
            HandleZoomLockedPremium_NoJump();
            HandleColorChangeGesture();
            HandleHeadlightControl();
            HandleTrunkByKneeKick();
        }
        else
        {
            HandleMomentCinematicPose();
            ApplyTrunkRotationSmooth();
        }

        HandleKeyboardDebug();
    }

    bool HasBody()
    {
        return bodyReader != null && bodyReader.hasBody;
    }

    // ===================== INTEGRAÇÃO COM ACTIVATION MANAGER =====================
    void OnInteractionLockedChanged(bool locked)
    {
        mode = locked ? ExperienceMode.Moment : ExperienceMode.Showroom;
        ApplyModeSideEffects(force: true);
    }

    void OnMomentStarted()
    {
        EnterMSportMode();

        if (momentResetZoomOnStart)
        {
            ForceMomentSafeZoomImmediate();
        }
    }

    void ApplyModeSideEffects(bool force)
    {
        if (!force) return;

        if (mode == ExperienceMode.Moment)
        {
            rotationVelocity = 0f;
            rotationVelocityRef = 0f;
            ResetZoomState();

            if (momentForceTrunkClosed)
                trunkIsOpen = false;

            if (momentForceHeadlightsOff)
                SetHeadlights(false);
        }
    }

    void ForceMomentSafeZoomImmediate()
    {
        if (!enableZoom || targetCamera == null || zoomPivot == null) return;

        float target = Mathf.Clamp(momentSafeZoomDistance, zoomMinDistance, zoomMaxDistance);
        SetCameraDistanceImmediate(target);
        ResetZoomState();
    }

    // ===================== MOMENTO, CINEMÁTICO =====================
    void HandleMomentCinematicPose()
    {
        if (carRoot != null && momentForceCarRotation)
        {
            float currentY = carRoot.eulerAngles.y;
            float targetY = momentTargetYaw;

            float y = Mathf.LerpAngle(currentY, targetY, Time.deltaTime * momentPoseSmooth);
            carRoot.rotation = Quaternion.Euler(carRoot.eulerAngles.x, y, carRoot.eulerAngles.z);
        }

        if (momentResetZoomOnStart)
        {
            if (enableZoom && targetCamera != null && zoomPivot != null)
            {
                float current = GetCameraDistance();
                float target = Mathf.Clamp(momentSafeZoomDistance, zoomMinDistance, zoomMaxDistance);
                float d = Mathf.SmoothDamp(current, target, ref zoomVelocity, 0.10f);
                SetCameraDistance(d);
            }
        }
    }

    // ===================== RESET SHOWROOM =====================
    void ResetToInitialStateSmooth()
    {
        if (carRoot != null)
        {
            carRoot.rotation = Quaternion.Slerp(carRoot.rotation, initialCarRotation, Time.deltaTime * resetCarSmooth);
            carRoot.position = Vector3.Lerp(carRoot.position, initialCarPosition, Time.deltaTime * resetCarSmooth);
        }

        if (trunkTransforms != null && trunkTransforms.Length > 0)
        {
            trunkIsOpen = false;

            Quaternion target = GetTrunkTargetRotation(open: false);
            for (int i = 0; i < trunkTransforms.Length; i++)
            {
                var t = trunkTransforms[i];
                if (t == null) continue;

                t.localRotation = Quaternion.Slerp(t.localRotation, target, Time.deltaTime * resetDoorSmooth);
            }
        }

        if (headlightRenderers != null && headlightRenderers.Length > 0)
            SetHeadlights(false);

        if (enableZoom && targetCamera != null && zoomPivot != null)
        {
            float current = GetCameraDistance();
            float target = Mathf.Clamp(zoomMaxDistance, zoomMinDistance, zoomMaxDistance);

            float d = Mathf.SmoothDamp(current, target, ref resetZoomVel, resetZoomSmooth);
            SetCameraDistance(d);
        }
    }

    // ===================== PERFIL =====================
    void ApplyDistanceProfile()
    {
        switch (userDistanceProfile)
        {
            case UserDistanceProfile.Near_1m:
                zoomMinDistance = zoomMin_1m;
                zoomMaxDistance = zoomMax_1m;
                break;

            case UserDistanceProfile.Mid_2m:
                zoomMinDistance = zoomMin_2m;
                zoomMaxDistance = zoomMax_2m;
                break;

            default:
            case UserDistanceProfile.Far_3m:
                zoomMinDistance = zoomMin_3m;
                zoomMaxDistance = zoomMax_3m;
                break;
        }
    }

    void SetupInitialZoom()
    {
        if (!enableZoom || targetCamera == null || zoomPivot == null) return;

        float startDist = Mathf.Clamp(zoomMaxDistance, zoomMinDistance, zoomMaxDistance);
        SetCameraDistanceImmediate(startDist);

        desiredDistance = startDist;
        zoomVelocity = 0f;

        filteredHandsDist = humanHandsDistanceMin;
        filteredHandsDistVel = 0f;

        ResetZoomState();
    }

    // ===================== ROTACAO PREMIUM =====================
    void HandleRotationByArmsEasyPremium()
    {
        if (momentBlockRotation && mode == ExperienceMode.Moment) return;
        if (carRoot == null) return;
        if (bodyReader == null) return;

        // Intenção: só roda se o BodyReader diz que é rotação
        if (!bodyReader.rotationGestureActive)
        {
            rotationVelocity = Mathf.SmoothDamp(rotationVelocity, 0f, ref rotationVelocityRef, rotationSmoothTime, rotationMaxVelocity, Time.deltaTime);
            return;
        }

        // Se está em gesto de cor, não roda, ponto final
        if (bodyReader.colorGestureActive)
        {
            rotationVelocity = Mathf.SmoothDamp(rotationVelocity, 0f, ref rotationVelocityRef, rotationSmoothTime, rotationMaxVelocity, Time.deltaTime);
            return;
        }

        Vector3 shoulderR = bodyReader.shoulderRightPos;
        Vector3 shoulderL = bodyReader.shoulderLeftPos;
        Vector3 handR = bodyReader.handRightPos;
        Vector3 handL = bodyReader.handLeftPos;

        if (!IsValid(shoulderR) || !IsValid(shoulderL) || !IsValid(handR) || !IsValid(handL))
        {
            rotationVelocity = Mathf.SmoothDamp(rotationVelocity, 0f, ref rotationVelocityRef, rotationSmoothTime, rotationMaxVelocity, Time.deltaTime);
            return;
        }

        float shoulderYAvg = 0.5f * (shoulderL.y + shoulderR.y);
        bool handsHighEnough =
            (handL.y > shoulderYAvg + handAboveShoulderForRotation) ||
            (handR.y > shoulderYAvg + handAboveShoulderForRotation);

        if (!handsHighEnough)
        {
            rotationVelocity = Mathf.SmoothDamp(rotationVelocity, 0f, ref rotationVelocityRef, rotationSmoothTime, rotationMaxVelocity, Time.deltaTime);
            return;
        }

        float leftOpen = (handL.x - shoulderL.x);
        float rightOpen = (handR.x - shoulderR.x);

        float raw = 0f;

        if (leftOpen < -armDeadZone)
            raw -= Mathf.InverseLerp(-armDeadZone, -0.35f, leftOpen);

        if (rightOpen > armDeadZone)
            raw += Mathf.InverseLerp(armDeadZone, 0.35f, rightOpen);

        if (invertRotationDirection)
            raw = -raw;

        float targetVel = 0f;
        if (Mathf.Abs(raw) > 0.001f)
            targetVel = raw * rotationSpeed * rotationSensitivity;

        rotationVelocity = Mathf.SmoothDamp(rotationVelocity, targetVel, ref rotationVelocityRef, rotationSmoothTime, rotationMaxVelocity, Time.deltaTime);

        if (Mathf.Abs(rotationVelocity) < 0.01f) return;

        carRoot.Rotate(0f, rotationVelocity * Time.deltaTime, 0f, Space.World);
    }

    // ===================== ZOOM PREMIUM =====================
    void HandleZoomLockedPremium_NoJump()
    {
        if (momentBlockZoom && mode == ExperienceMode.Moment) return;
        if (!enableZoom || targetCamera == null || zoomPivot == null) return;
        if (bodyReader == null) return;

        // Intenção: só entra no zoom se o BodyReader diz que é zoom
        if (!bodyReader.zoomGestureActive)
        {
            if (zoomLocked)
            {
                zoomLostPoseTimer += Time.deltaTime;
                if (zoomLostPoseTimer >= zoomLostPoseGrace)
                    ResetZoomState();
            }
            else
            {
                zoomLockTimer = 0f;
                filteredHandsDistVel = 0f;
                zoomLostPoseTimer = 0f;
            }
            return;
        }

        Vector3 handL = bodyReader.handLeftPos;
        Vector3 handR = bodyReader.handRightPos;
        Vector3 spineMid = bodyReader.spineMidPos;

        if (!IsValid(handL) || !IsValid(handR) || !IsValid(spineMid))
        {
            ResetZoomState();
            return;
        }

        float handsDist = Vector3.Distance(handL, handR);

        bool handsAbove = (handL.y > spineMid.y + zoomHandsAboveSpine) && (handR.y > spineMid.y + zoomHandsAboveSpine);
        bool handsInFront = (handL.z > spineMid.z + zoomHandsInFrontOfSpine) && (handR.z > spineMid.z + zoomHandsInFrontOfSpine);
        bool handsApartEnough = handsDist >= zoomEntryMinHandsDistance;

        bool entryPose = handsAbove && handsInFront && handsApartEnough;

        if (!zoomLocked)
        {
            if (entryPose)
            {
                zoomLockTimer += Time.deltaTime;
                filteredHandsDist = Mathf.SmoothDamp(filteredHandsDist, handsDist, ref filteredHandsDistVel, handsDistanceFilter);

                if (zoomLockTimer >= zoomLockHoldTime)
                {
                    zoomLocked = true;
                    zoomLostPoseTimer = 0f;

                    lockCameraDistance = zoomBaselineUseMaxDistance
                        ? Mathf.Clamp(zoomMaxDistance, zoomMinDistance, zoomMaxDistance)
                        : GetCameraDistance();

                    lockHandsDistance = Mathf.Clamp(filteredHandsDist, humanHandsDistanceMin, humanHandsDistanceMax);

                    desiredDistance = lockCameraDistance;
                    zoomVelocity = 0f;

                    if (logZoomDebug)
                        Debug.Log($"[ZOOM] LOCK, baseline={lockCameraDistance:F3}, handsBase={lockHandsDistance:F3}");
                }
            }
            else
            {
                zoomLockTimer = 0f;
                zoomLostPoseTimer = 0f;
                filteredHandsDistVel = 0f;
            }

            return;
        }

        if (!entryPose)
        {
            zoomLostPoseTimer += Time.deltaTime;
            if (zoomLostPoseTimer >= zoomLostPoseGrace)
                ResetZoomState();

            return;
        }

        zoomLostPoseTimer = 0f;

        filteredHandsDist = Mathf.SmoothDamp(filteredHandsDist, handsDist, ref filteredHandsDistVel, handsDistanceFilter);

        float clamped = Mathf.Clamp(filteredHandsDist, humanHandsDistanceMin, humanHandsDistanceMax);
        float tHuman = Mathf.InverseLerp(humanHandsDistanceMin, humanHandsDistanceMax, clamped);
        tHuman = Mathf.Pow(Mathf.Clamp01(tHuman), zoomResponsePower);

        float fullRange = (lockCameraDistance - zoomMinDistance) * zoomRangeMultiplier;
        fullRange = Mathf.Max(0f, fullRange);

        float targetDistance = lockCameraDistance - (tHuman * fullRange);
        targetDistance = Mathf.Clamp(targetDistance, zoomMinDistance, zoomMaxDistance);

        if (tHuman < zoomReturnAssistZone)
        {
            float a = 1f - Mathf.InverseLerp(0f, zoomReturnAssistZone, tHuman);
            float assist = zoomReturnAssistStrength * a;
            targetDistance = Mathf.Lerp(targetDistance, lockCameraDistance, Time.deltaTime * assist);
        }

        desiredDistance = Mathf.SmoothDamp(desiredDistance, targetDistance, ref zoomVelocity, zoomSmoothTime, Mathf.Infinity, Time.deltaTime);
        SetCameraDistance(desiredDistance);

        if (logZoomDebug)
            Debug.Log($"[ZOOM] hands={handsDist:F3} filt={filteredHandsDist:F3} t={tHuman:F3} cam={desiredDistance:F3}");
    }

    void ResetZoomState()
    {
        zoomLocked = false;
        zoomLockTimer = 0f;
        zoomLostPoseTimer = 0f;

        filteredHandsDistVel = 0f;

        lockCameraDistance = 0f;
        lockHandsDistance = 0f;
    }

    float GetCameraDistance()
    {
        if (targetCamera == null || zoomPivot == null) return 0f;
        return Vector3.Distance(targetCamera.transform.position, zoomPivot.position);
    }

    void SetCameraDistance(float distance)
    {
        if (targetCamera == null || zoomPivot == null) return;

        Vector3 dirRaw = targetCamera.transform.position - zoomPivot.position;
        Vector3 dir;

        if (dirRaw.sqrMagnitude > 0.000001f)
            dir = dirRaw.normalized;
        else
            dir = -zoomPivot.forward;

        targetCamera.transform.position = zoomPivot.position + dir * distance;
        targetCamera.transform.LookAt(zoomPivot.position);
    }

    void SetCameraDistanceImmediate(float distance)
    {
        desiredDistance = distance;
        zoomVelocity = 0f;
        SetCameraDistance(distance);
    }

    // ===================== CORES =====================
    void HandleColorChangeGesture()
    {
        if (!enableColorGesture) return;
        if (momentBlockColor && mode == ExperienceMode.Moment) return;
        if (colorOptions == null || colorOptions.Length == 0) return;
        if (carPaintRenderers == null || carPaintRenderers.Length == 0) return;
        if (bodyReader == null) return;

        // Intenção: só muda cor quando o BodyReader diz que é cor
        bool colorActive = bodyReader.colorGestureActive;

        if (!colorActive)
        {
            leftWasUp = false;
            return;
        }

        if (colorActive && !leftWasUp)
        {
            if (Time.time - lastColorChangeTime >= colorChangeCooldown && !isColorTransitionRunning)
            {
                lastColorChangeTime = Time.time;
                currentColor = (currentColor + 1) % colorOptions.Length;
                StartCoroutine(FadeToColor(currentColor));
            }
        }

        leftWasUp = colorActive;
    }

    IEnumerator FadeToColor(int colorIndex)
    {
        isColorTransitionRunning = true;

        Material targetMat = (colorOptions != null && colorOptions.Length > 0) ? colorOptions[colorIndex] : null;
        if (targetMat == null)
        {
            isColorTransitionRunning = false;
            yield break;
        }

        Color from = GetCurrentPaintColor();
        Color to = GetMaterialColorSafe(targetMat);

        float t = 0f;
        float dur = Mathf.Max(0.01f, colorFadeDuration);

        while (t < 1f)
        {
            t += Time.deltaTime / dur;
            Color c = Color.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t));
            ApplyPaintColor(c);
            yield return null;
        }

        ApplyPaintMaterial(targetMat);

        isColorTransitionRunning = false;
    }

    void ApplyPaintColorInstant(int colorIndex)
    {
        if (colorOptions == null || colorOptions.Length == 0) return;
        if (colorIndex < 0 || colorIndex >= colorOptions.Length) return;

        Material m = colorOptions[colorIndex];
        if (m == null) return;

        ApplyPaintMaterial(m);
        ApplyPaintColor(GetMaterialColorSafe(m));
    }

    void CaptureOriginalPaintIfNeeded()
    {
        if (originalCaptured) return;
        if (carPaintRenderers == null || carPaintRenderers.Length == 0) return;

        int n = carPaintRenderers.Length;
        originalPaintMaterials = new Material[n];
        originalPaintColors = new Color[n];
        originalHasBaseColor = new bool[n];

        for (int i = 0; i < n; i++)
        {
            var r = carPaintRenderers[i];
            if (r == null) continue;

            int idx = GetPaintMaterialIndex(i);
            var mats = r.materials;
            if (idx < 0 || idx >= mats.Length) continue;

            var m = mats[idx];
            originalPaintMaterials[i] = m;

            if (m != null)
            {
                if (m.HasProperty("_BaseColor"))
                {
                    originalHasBaseColor[i] = true;
                    originalPaintColors[i] = m.GetColor("_BaseColor");
                }
                else if (m.HasProperty("_Color"))
                {
                    originalHasBaseColor[i] = false;
                    originalPaintColors[i] = m.GetColor("_Color");
                }
            }
        }

        originalCaptured = true;
    }

    void RestoreOriginalPaint()
    {
        if (!originalCaptured) return;
        if (carPaintRenderers == null) return;

        for (int i = 0; i < carPaintRenderers.Length; i++)
        {
            var r = carPaintRenderers[i];
            if (r == null) continue;

            int idx = GetPaintMaterialIndex(i);
            var mats = r.materials;
            if (idx < 0 || idx >= mats.Length) continue;

            var originalMat = originalPaintMaterials != null && i < originalPaintMaterials.Length ? originalPaintMaterials[i] : null;
            if (originalMat != null)
            {
                mats[idx] = originalMat;
                r.materials = mats;

                if (originalMat.HasProperty("_BaseColor"))
                    originalMat.SetColor("_BaseColor", originalPaintColors[i]);
                else if (originalMat.HasProperty("_Color"))
                    originalMat.SetColor("_Color", originalPaintColors[i]);
            }
        }
    }

    void ApplyPaintMaterial(Material mat)
    {
        if (mat == null) return;
        if (carPaintRenderers == null) return;

        for (int i = 0; i < carPaintRenderers.Length; i++)
        {
            var r = carPaintRenderers[i];
            if (r == null) continue;

            int idx = GetPaintMaterialIndex(i);
            var mats = r.materials;
            if (idx < 0 || idx >= mats.Length) continue;

            mats[idx] = mat;
            r.materials = mats;
        }
    }

    void ApplyPaintColor(Color c)
    {
        if (carPaintRenderers == null) return;

        for (int i = 0; i < carPaintRenderers.Length; i++)
        {
            var r = carPaintRenderers[i];
            if (r == null) continue;

            int idx = GetPaintMaterialIndex(i);
            var mats = r.materials;
            if (idx < 0 || idx >= mats.Length) continue;

            if (mats[idx] != null)
            {
                if (mats[idx].HasProperty("_BaseColor"))
                    mats[idx].SetColor("_BaseColor", c);
                else if (mats[idx].HasProperty("_Color"))
                    mats[idx].SetColor("_Color", c);
            }
        }
    }

    Color GetCurrentPaintColor()
    {
        if (carPaintRenderers == null || carPaintRenderers.Length == 0) return Color.white;

        var r = carPaintRenderers[0];
        if (r == null) return Color.white;

        int idx = GetPaintMaterialIndex(0);
        var mats = r.materials;
        if (idx < 0 || idx >= mats.Length) return Color.white;

        var m = mats[idx];
        if (m == null) return Color.white;

        if (m.HasProperty("_BaseColor")) return m.GetColor("_BaseColor");
        if (m.HasProperty("_Color")) return m.GetColor("_Color");

        return Color.white;
    }

    Color GetMaterialColorSafe(Material m)
    {
        if (m == null) return Color.white;
        if (m.HasProperty("_BaseColor")) return m.GetColor("_BaseColor");
        if (m.HasProperty("_Color")) return m.GetColor("_Color");
        return Color.white;
    }

    int GetPaintMaterialIndex(int rendererIndex)
    {
        if (carPaintMaterialIndices == null || carPaintMaterialIndices.Length == 0) return 0;
        if (rendererIndex < 0 || rendererIndex >= carPaintMaterialIndices.Length) return 0;
        return Mathf.Max(0, carPaintMaterialIndices[rendererIndex]);
    }

    // ===================== FARÓIS =====================
    void HandleHeadlightControl()
    {
        if (momentBlockHeadlights && mode == ExperienceMode.Moment) return;
        if (headlightRenderers == null || headlightRenderers.Length == 0) return;
        if (bodyReader == null) return;

        Vector3 head = bodyReader.headPos;
        Vector3 spineShoulder = bodyReader.spineShoulderPos;

        if (!IsValid(head) || !IsValid(spineShoulder)) return;

        float dx = head.x - spineShoulder.x;

        if (Mathf.Abs(dx) < headTurnDeadZone) return;

        if (dx > headTurnThreshold && !headlightsOn)
            SetHeadlights(true);
        else if (dx < -headTurnThreshold && headlightsOn)
            SetHeadlights(false);
    }

    void SetHeadlights(bool on)
    {
        headlightsOn = on;

        Material m = on ? headlightOnMaterial : headlightOffMaterial;
        if (m == null) return;

        for (int i = 0; i < headlightRenderers.Length; i++)
        {
            var r = headlightRenderers[i];
            if (r == null) continue;

            var mats = r.materials;
            int idx = Mathf.Clamp(headlightMaterialIndex, 0, mats.Length - 1);
            mats[idx] = m;
            r.materials = mats;
        }
    }

    // ===================== MALA POR JOELHO =====================
    void HandleTrunkByKneeKick()
    {
        if (momentBlockTrunk && mode == ExperienceMode.Moment) return;
        if (trunkTransforms == null || trunkTransforms.Length == 0) return;
        if (bodyReader == null) return;

        if (bodyReader.trunkKickTriggered)
        {
            if (trunkToggleMode)
                trunkIsOpen = !trunkIsOpen;
            else
                trunkIsOpen = true;
        }

        ApplyTrunkRotationSmooth();
    }

    void ApplyTrunkImmediate(bool closed)
    {
        if (trunkTransforms == null || trunkTransforms.Length == 0) return;

        trunkIsOpen = !closed;
        Quaternion target = GetTrunkTargetRotation(open: trunkIsOpen);

        for (int i = 0; i < trunkTransforms.Length; i++)
        {
            var t = trunkTransforms[i];
            if (t == null) continue;
            t.localRotation = target;
        }
    }

    Quaternion GetTrunkTargetRotation(bool open)
    {
        if (trunkRotationMode == TrunkRotationMode.EulerLocal)
        {
            return Quaternion.Euler(open ? trunkOpenEuler : trunkClosedEuler);
        }

        Vector3 axis = trunkAxis;
        if (axis.sqrMagnitude < 0.000001f) axis = Vector3.right;
        axis = axis.normalized;

        float angle = open ? trunkOpenAngle : 0f;
        return Quaternion.AngleAxis(angle, axis) * Quaternion.Euler(trunkClosedEuler);
    }

    void ApplyTrunkRotationSmooth()
    {
        if (trunkTransforms == null || trunkTransforms.Length == 0) return;

        Quaternion target = GetTrunkTargetRotation(open: trunkIsOpen);

        for (int i = 0; i < trunkTransforms.Length; i++)
        {
            var t = trunkTransforms[i];
            if (t == null) continue;

            t.localRotation = Quaternion.Slerp(t.localRotation, target, Time.deltaTime * trunkLerpSpeed);
        }
    }

    // ===================== KEYBOARD DEBUG =====================
    void HandleKeyboardDebug()
    {
        if (!enableKeyboardDebug) return;

        var kb = Keyboard.current;
        if (kb == null) return;

        if (carRoot != null && mode == ExperienceMode.Showroom)
        {
            if (kb.leftArrowKey.isPressed) carRoot.Rotate(0f, -60f * Time.deltaTime, 0f, Space.World);
            if (kb.rightArrowKey.isPressed) carRoot.Rotate(0f, 60f * Time.deltaTime, 0f, Space.World);
        }

        if (enableZoom && targetCamera != null && zoomPivot != null && mode == ExperienceMode.Showroom)
        {
            if (kb.upArrowKey.isPressed)
            {
                float d = Mathf.Clamp(GetCameraDistance() - 1.2f * Time.deltaTime, zoomMinDistance, zoomMaxDistance);
                SetCameraDistanceImmediate(d);
            }
            if (kb.downArrowKey.isPressed)
            {
                float d = Mathf.Clamp(GetCameraDistance() + 1.2f * Time.deltaTime, zoomMinDistance, zoomMaxDistance);
                SetCameraDistanceImmediate(d);
            }
        }

        if (kb.cKey.wasPressedThisFrame && !isColorTransitionRunning && mode == ExperienceMode.Showroom)
        {
            if (colorOptions != null && colorOptions.Length > 0)
            {
                currentColor = (currentColor + 1) % colorOptions.Length;
                StartCoroutine(FadeToColor(currentColor));
            }
        }

        if (kb.hKey.wasPressedThisFrame && mode == ExperienceMode.Showroom)
            SetHeadlights(!headlightsOn);

        if (kb.oKey.wasPressedThisFrame && mode == ExperienceMode.Showroom)
            trunkIsOpen = !trunkIsOpen;

        if (kb.rKey.wasPressedThisFrame)
        {
            ResetZoomState();
            SetupInitialZoom();
        }

        if (kb.mKey.wasPressedThisFrame)
        {
            mode = (mode == ExperienceMode.Showroom) ? ExperienceMode.Moment : ExperienceMode.Showroom;
            ApplyModeSideEffects(force: true);

            if (mode == ExperienceMode.Moment && momentResetZoomOnStart)
                ForceMomentSafeZoomImmediate();
        }
    }

    // ===================== UTIL =====================
    bool IsValid(Vector3 v)
    {
        if (float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z)) return false;
        return true;
    }

    // ===================== EVENTO EXTERNO =====================
    void EnterMSportMode()
    {
        // Hook mantido.
    }
}
