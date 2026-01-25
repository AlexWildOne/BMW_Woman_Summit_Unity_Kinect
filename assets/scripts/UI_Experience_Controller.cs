using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class UI_Experience_Controller : MonoBehaviour
{
    [Header("Refs (liga isto no Inspector)")]
    public ActivationManager activationManager;

    [Header("Textos")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI mainMessage;
    public TextMeshProUGUI subText;

    [Header("FX Groups (opcional)")]
    public GameObject trackingFXGroup;
    public GameObject powerFXGroup;

    [Header("Fade (opcional, premium)")]
    [Tooltip("Se atribuíres um CanvasGroup, fazemos fade suave ao trocar de mensagem.")]
    public CanvasGroup uiCanvasGroup;

    [Tooltip("Duração do fade (segundos). 0 desliga.")]
    [Range(0f, 1f)]
    public float fadeDuration = 0.18f;

    string lastTitle;
    string lastMessage;
    string lastSub;

    void Reset()
    {
        // tenta apanhar automaticamente
        activationManager = FindFirstObjectByType<ActivationManager>();
        uiCanvasGroup = GetComponentInChildren<CanvasGroup>();
    }

    void OnEnable()
    {
        if (activationManager == null) return;

        activationManager.OnExperienceActiveChanged += OnExperienceActiveChanged;
        activationManager.OnInteractionLockedChanged += OnInteractionLockedChanged;
    }

    void OnDisable()
    {
        if (activationManager == null) return;

        activationManager.OnExperienceActiveChanged -= OnExperienceActiveChanged;
        activationManager.OnInteractionLockedChanged -= OnInteractionLockedChanged;
    }

    // Estes eventos são úteis para UI global (ex: mostrar "bloqueado" no momento)
    void OnExperienceActiveChanged(bool active)
    {
        // se quiseres, podes esconder UI quando não há body
        // aqui mantemos sempre ligado para ter Idle Message visível
    }

    void OnInteractionLockedChanged(bool locked)
    {
        // locked = true durante o Momento
        // se quiseres mudar layout, podes fazer aqui
    }

    // ===================== API pública (chamada pelo ActivationManager se quiseres) =====================

    public void SetUI(string title, string message, string sub)
    {
        if (title == lastTitle && message == lastMessage && sub == lastSub)
            return;

        lastTitle = title;
        lastMessage = message;
        lastSub = sub;

        if (fadeDuration > 0f && uiCanvasGroup != null)
            StopAllCoroutines();

        if (fadeDuration > 0f && uiCanvasGroup != null)
            StartCoroutine(FadeSwap(title, message, sub));
        else
            ApplyTexts(title, message, sub);
    }

    public void SetFX(bool trackingFx, bool powerFx)
    {
        if (trackingFXGroup != null && trackingFXGroup.activeSelf != trackingFx)
            trackingFXGroup.SetActive(trackingFx);

        if (powerFXGroup != null && powerFXGroup.activeSelf != powerFx)
            powerFXGroup.SetActive(powerFx);
    }

    // ===================== Interno =====================

    void ApplyTexts(string title, string message, string sub)
    {
        if (titleText != null) titleText.text = title;
        if (mainMessage != null) mainMessage.text = message;
        if (subText != null) subText.text = sub;
    }

    System.Collections.IEnumerator FadeSwap(string title, string message, string sub)
    {
        float t = 0f;
        float start = uiCanvasGroup.alpha;

        // fade out
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            uiCanvasGroup.alpha = Mathf.Lerp(start, 0f, t / fadeDuration);
            yield return null;
        }

        uiCanvasGroup.alpha = 0f;

        ApplyTexts(title, message, sub);

        // fade in
        t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            uiCanvasGroup.alpha = Mathf.Lerp(0f, 1f, t / fadeDuration);
            yield return null;
        }

        uiCanvasGroup.alpha = 1f;
    }
}
