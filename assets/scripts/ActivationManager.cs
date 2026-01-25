using System;
using UnityEngine;
using TMPro;

public class ActivationManager : MonoBehaviour
{
    [Header("Tracking")]
    public KinectBodyReader bodyReader;

    [Header("UI (fallback, se não usares UI_Experience_Controller)")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI mainMessage;
    public TextMeshProUGUI subText;

    [Header("FX Groups (fallback)")]
    public GameObject trackingFXGroup;
    public GameObject powerFXGroup;

    [Header("UI Controller (recomendado)")]
    public UI_Experience_Controller uiController;

    [Header("Timings")]
    [Tooltip("Tempo que a participante tem de aguentar a power pose")]
    public float powerPoseHoldTime = 0.7f;

    [Tooltip("Tempo sem pessoa para voltar ao estado Idle")]
    public float noBodyBackToIdleTime = 2f;

    [Tooltip("Quanto tempo fica no momento BMW")]
    public float showMomentTime = 3f;

    [Header("Mensagens Customizáveis")]
    [TextArea] public string idleMessage = "Aproxima-te do espaço BMW Woman Summit";
    [TextArea] public string introMessage = "Explora o movimento, sente a energia";
    [TextArea] public string poseMessage = "Agora faz a tua power pose";
    [TextArea] public string momentMessage = "Este é o teu momento BMW";
    [TextArea] public string uploadingMessage = "A preparar o teu momento";
    [TextArea] public string uploadingSub = "Só mais um segundo, estamos a guardar a tua imagem.";

    [Header("Títulos Customizáveis")]
    public string idleTitle = "BMW WOMAN SUMMIT";
    public string introTitle = "BMW WOMAN SUMMIT";
    public string poseTitle = "BMW WOMAN SUMMIT";
    public string momentTitle = "BMW WOMAN SUMMIT";
    public string uploadingTitle = "BMW WOMAN SUMMIT";

    [Header("Subtítulos Customizáveis")]
    [TextArea] public string idleSub = "Aproxima-te da área marcada para ativares a experiência.";
    [TextArea] public string introSub = "Move-te livremente, deixa o corpo ganhar confiança.";
    [TextArea] public string poseSub = "Levanta o braço direito, abre o peito, assume a tua power pose.";
    [TextArea] public string momentSub = "Guarda este momento, é só teu.";

    [Header("Opções de experiência")]
    [Tooltip("Se true, durante o Momento BMW o carro e gestos ficam bloqueados.")]
    public bool lockInteractionDuringMoment = true;

    // Eventos
    public event Action OnMomentStarted;
    public event Action OnMomentEnded;

    // Liga o carro e o pipeline (bmw_sync)
    public event Action<bool> OnExperienceActiveChanged;
    public event Action<bool> OnInteractionLockedChanged;

    float powerPoseTimer;
    float noBodyTimer;
    float momentTimer;

    ActivationState state = ActivationState.Idle;

    bool warnedMissingBodyReader;

    // Cache para evitar spam de UI e FX
    string lastTitle;
    string lastMessage;
    string lastSub;
    bool lastTrackingFx;
    bool lastPowerFx;

    bool experienceActive;
    bool interactionLocked;

    enum ActivationState
    {
        Idle,
        TrackingIntro,
        WaitingPowerPose,
        ShowingMoment,
        Uploading
    }

    void Start()
    {
        AutoWireUIControllerIfNeeded();
        ResetToIdleState();
    }

    void Update()
    {
        if (bodyReader == null)
        {
            if (!warnedMissingBodyReader)
            {
                Debug.LogWarning("ActivationManager: Referência ao 'bodyReader' está ausente. A experiência não pode iniciar tracking.");
                warnedMissingBodyReader = true;
            }
            return;
        }

        bool hasBody = bodyReader.hasBody || bodyReader.isTracked;
        bool isPowerPose = bodyReader.powerPose;

        switch (state)
        {
            case ActivationState.Idle:
                HandleIdleState(hasBody);
                break;

            case ActivationState.TrackingIntro:
                HandleTrackingIntroState(hasBody);
                break;

            case ActivationState.WaitingPowerPose:
                HandleWaitingPowerPoseState(hasBody, isPowerPose);
                break;

            case ActivationState.ShowingMoment:
                HandleShowingMomentState(hasBody);
                break;

            case ActivationState.Uploading:
                HandleUploadingState(hasBody);
                break;
        }
    }

    // ===================== State Handlers =====================

    void HandleIdleState(bool hasBody)
    {
        if (!hasBody) return;

        noBodyTimer = 0f;
        powerPoseTimer = 0f;
        momentTimer = 0f;

        SetExperienceActive(true);
        SetInteractionLocked(false);

        state = ActivationState.TrackingIntro;

        ApplyUIAndFx(introTitle, introMessage, introSub, trackingFx: true, powerFx: false);
    }

    void HandleTrackingIntroState(bool hasBody)
    {
        if (!hasBody)
        {
            HandleNoBody();
            return;
        }

        state = ActivationState.WaitingPowerPose;
        powerPoseTimer = 0f;

        ApplyUIAndFx(poseTitle, poseMessage, poseSub, trackingFx: true, powerFx: false);
    }

    void HandleWaitingPowerPoseState(bool hasBody, bool isPowerPose)
    {
        if (!hasBody)
        {
            HandleNoBody();
            powerPoseTimer = 0f;
            return;
        }

        noBodyTimer = 0f;

        SetExperienceActive(true);
        SetInteractionLocked(false);

        ApplyUIAndFx(poseTitle, poseMessage, poseSub, trackingFx: true, powerFx: false);

        if (isPowerPose)
        {
            powerPoseTimer += Time.deltaTime;

            if (powerPoseTimer >= powerPoseHoldTime)
            {
                state = ActivationState.ShowingMoment;
                momentTimer = 0f;

                if (lockInteractionDuringMoment)
                    SetInteractionLocked(true);

                ApplyUIAndFx(momentTitle, momentMessage, momentSub, trackingFx: false, powerFx: true);

                OnMomentStarted?.Invoke();
            }
        }
        else
        {
            powerPoseTimer = 0f;
        }
    }

    void HandleShowingMomentState(bool hasBody)
    {
        if (!hasBody)
        {
            HandleNoBody();
            return;
        }

        noBodyTimer = 0f;

        if (lockInteractionDuringMoment)
            SetInteractionLocked(true);

        ApplyUIAndFx(momentTitle, momentMessage, momentSub, trackingFx: false, powerFx: true);

        momentTimer += Time.deltaTime;

        if (momentTimer >= showMomentTime)
        {
            OnMomentEnded?.Invoke();

            // Tipicamente, com bmw_sync:
            // EnterUploadingState() e só depois CompleteUpload(success)
            SetInteractionLocked(false);

            state = ActivationState.WaitingPowerPose;
            powerPoseTimer = 0f;

            ApplyUIAndFx(poseTitle, poseMessage, poseSub, trackingFx: true, powerFx: false);
        }
    }

    void HandleUploadingState(bool hasBody)
    {
        if (!hasBody)
        {
            HandleNoBody();
            return;
        }

        noBodyTimer = 0f;

        SetExperienceActive(true);
        SetInteractionLocked(true);

        ApplyUIAndFx(uploadingTitle, uploadingMessage, uploadingSub, trackingFx: false, powerFx: true);

        // Termina via callback: CompleteUpload(success)
    }

    void HandleNoBody()
    {
        noBodyTimer += Time.deltaTime;

        if (noBodyTimer >= noBodyBackToIdleTime)
            ResetToIdleState();
    }

    // ===================== API para o bmw_sync =====================

    public void EnterUploadingState()
    {
        if (state == ActivationState.Uploading) return;

        state = ActivationState.Uploading;

        SetExperienceActive(true);
        SetInteractionLocked(true);

        ApplyUIAndFx(uploadingTitle, uploadingMessage, uploadingSub, trackingFx: false, powerFx: true);
    }

    public void CompleteUpload(bool success)
    {
        SetInteractionLocked(false);

        state = ActivationState.WaitingPowerPose;
        powerPoseTimer = 0f;

        ApplyUIAndFx(poseTitle, poseMessage, poseSub, trackingFx: true, powerFx: false);
    }

    // ===================== Visual / UI =====================

    void ResetToIdleState()
    {
        state = ActivationState.Idle;
        noBodyTimer = 0f;
        powerPoseTimer = 0f;
        momentTimer = 0f;

        SetExperienceActive(false);
        SetInteractionLocked(false);

        ApplyUIAndFx(idleTitle, idleMessage, idleSub, trackingFx: false, powerFx: false);
    }

    void ApplyUIAndFx(string title, string message, string sub, bool trackingFx, bool powerFx)
    {
        if (title != lastTitle || message != lastMessage || sub != lastSub)
        {
            lastTitle = title;
            lastMessage = message;
            lastSub = sub;
            AnimateMessage(title, message, sub);
        }

        if (trackingFx != lastTrackingFx || powerFx != lastPowerFx)
        {
            lastTrackingFx = trackingFx;
            lastPowerFx = powerFx;
            AnimateFX(trackingFx, powerFx);
        }
    }

    void AnimateMessage(string title, string message, string sub)
    {
        if (uiController != null)
        {
            uiController.SetUI(title, message, sub);
            return;
        }

        SetText(titleText, title);
        SetText(mainMessage, message);
        SetText(subText, sub);
    }

    void AnimateFX(bool trackingActive, bool powerActive)
    {
        if (uiController != null)
        {
            uiController.SetFX(trackingActive, powerActive);
            return;
        }

        if (trackingFXGroup != null && trackingFXGroup.activeSelf != trackingActive)
            trackingFXGroup.SetActive(trackingActive);

        if (powerFXGroup != null && powerFXGroup.activeSelf != powerActive)
            powerFXGroup.SetActive(powerActive);
    }

    void AutoWireUIControllerIfNeeded()
    {
        if (uiController != null) return;

        // tenta apanhar no mesmo GameObject, ou em filhos
        uiController = GetComponent<UI_Experience_Controller>();
        if (uiController == null)
            uiController = GetComponentInChildren<UI_Experience_Controller>();
    }

    // ===================== Eventos de alto nível =====================

    void SetExperienceActive(bool active)
    {
        if (experienceActive == active) return;
        experienceActive = active;
        OnExperienceActiveChanged?.Invoke(active);
    }

    void SetInteractionLocked(bool locked)
    {
        if (interactionLocked == locked) return;
        interactionLocked = locked;
        OnInteractionLockedChanged?.Invoke(locked);
    }

    // ===================== Helpers =====================

    void SetText(TextMeshProUGUI target, string value)
    {
        if (target != null)
            target.text = value;
    }
}

