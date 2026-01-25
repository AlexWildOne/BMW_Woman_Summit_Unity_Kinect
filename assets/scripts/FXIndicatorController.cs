using System.Collections;
using UnityEngine;

public class FXIndicatorController : MonoBehaviour
{
    [Header("Visibilidade")]
    [Tooltip("Se vazio, tenta apanhar Renderers nos filhos")]
    public Renderer[] renderers;

    [Tooltip("Se true, desativa o GameObject quando está escondido")]
    public bool disableGameObjectWhenHidden = true;

    [Header("Opacidade")]
    [Tooltip("Nome da propriedade de cor no shader, _BaseColor para URP/Lit, _Color para outros")]
    public string colorProperty = "_BaseColor";

    [Tooltip("Opacidade quando está visível")]
    [Range(0f, 1f)] public float visibleAlpha = 1f;

    [Tooltip("Opacidade quando está escondido")]
    [Range(0f, 1f)] public float hiddenAlpha = 0f;

    [Tooltip("Velocidade de fade")]
    public float fadeSpeed = 10f;

    [Header("Pulse")]
    [Tooltip("Aumenta escala no pulse")]
    public float pulseScaleMultiplier = 1.15f;

    [Tooltip("Duração do pulse")]
    public float pulseDuration = 0.25f;

    private bool isVisible = false;
    private float targetAlpha = 0f;

    private Vector3 baseScale;
    private Coroutine pulseRoutine;

    void Awake()
    {
        baseScale = transform.localScale;

        if (renderers == null || renderers.Length == 0)
        {
            renderers = GetComponentsInChildren<Renderer>(true);
        }

        // Começa escondido
        SetAlphaImmediate(hiddenAlpha);
        if (disableGameObjectWhenHidden) gameObject.SetActive(false);
    }

    void Update()
    {
        if (renderers == null || renderers.Length == 0) return;

        // Se estiver escondido e o GameObject estiver desligado não há nada a fazer
        if (!gameObject.activeSelf && disableGameObjectWhenHidden) return;

        float currentAlpha = GetCurrentAlpha();
        float nextAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, Time.deltaTime * fadeSpeed);
        SetAlphaImmediate(nextAlpha);

        if (disableGameObjectWhenHidden && !isVisible && Mathf.Approximately(nextAlpha, hiddenAlpha))
        {
            gameObject.SetActive(false);
        }
    }

    public void Show()
    {
        isVisible = true;
        targetAlpha = visibleAlpha;

        if (disableGameObjectWhenHidden && !gameObject.activeSelf)
        {
            gameObject.SetActive(true);
            // Garante que volta a aparecer sem piscar
            SetAlphaImmediate(hiddenAlpha);
        }
    }

    public void Hide()
    {
        isVisible = false;
        targetAlpha = hiddenAlpha;
    }

    public void PulseOnce(float amount = 0.35f, float duration = 0.25f)
    {
        if (pulseRoutine != null) StopCoroutine(pulseRoutine);
        pulseRoutine = StartCoroutine(PulseRoutine(Mathf.Clamp01(amount), Mathf.Max(0.05f, duration)));
    }

    private IEnumerator PulseRoutine(float amount, float duration)
    {
        Vector3 start = baseScale;
        Vector3 end = baseScale * Mathf.Lerp(1f, pulseScaleMultiplier, amount);

        float halfDuration = duration * 0.5f;

        // Primeiro ciclo de crescimento
        for (float t = 0; t < 1f; t += Time.deltaTime / halfDuration)
        {
            transform.localScale = Vector3.Lerp(start, end, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)));
            yield return null;
        }

        // Segundo ciclo de retorno ao estado inicial
        for (float t = 0; t < 1f; t += Time.deltaTime / halfDuration)
        {
            transform.localScale = Vector3.Lerp(end, start, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)));
            yield return null;
        }

        transform.localScale = start;
        pulseRoutine = null;
    }

    private float GetCurrentAlpha()
    {
        foreach (var renderer in renderers)
        {
            if (renderer == null) continue;

            var material = renderer.material;
            if (material == null) continue;

            if (material.HasProperty(colorProperty))
                return material.GetColor(colorProperty).a;

            if (material.HasProperty("_Color"))
                return material.GetColor("_Color").a;

            return material.color.a;
        }

        return 0f; // Retorna opacidade mínima caso nada seja encontrado
    }

    private void SetAlphaImmediate(float alpha)
    {
        if (renderers == null) return;

        foreach (var renderer in renderers)
        {
            if (renderer == null) continue;

            var materials = renderer.materials;
            if (materials == null) continue;

            foreach (var material in materials)
            {
                if (material == null) continue;

                if (material.HasProperty(colorProperty))
                {
                    Color color = material.GetColor(colorProperty);
                    color.a = alpha;
                    material.SetColor(colorProperty, color);
                }
                else if (material.HasProperty("_Color"))
                {
                    Color color = material.GetColor("_Color");
                    color.a = alpha;
                    material.SetColor("_Color", color);
                }
                else
                {
                    Color color = material.color;
                    color.a = alpha;
                    material.color = color;
                }
            }
        }
    }
}
