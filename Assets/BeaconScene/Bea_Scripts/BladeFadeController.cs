using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class BladeFadeController : MonoBehaviour
{
    [SerializeField]
    private float fadeDuration = 2f;

    private readonly List<Material> fadeMaterials =
        new List<Material>();

    private readonly List<float> originalAlphas =
        new List<float>();

    private Coroutine currentFade;
    private bool materialsPrepared;

    private void Awake()
    {
        PrepareMaterials();
        SetFadeAmount(0f);
    }

    public void FadeIn()
    {
        PrepareMaterials();

        if (currentFade != null)
        {
            StopCoroutine(currentFade);
        }

        currentFade = StartCoroutine(
            FadeBlade(0f, 1f, false)
        );
    }

    public void FadeOut()
    {
        PrepareMaterials();

        if (currentFade != null)
        {
            StopCoroutine(currentFade);
        }

        currentFade = StartCoroutine(
            FadeBlade(1f, 0f, true)
        );
    }

    private void PrepareMaterials()
    {
        if (materialsPrepared)
            return;

        Renderer[] bladeRenderers =
            GetComponentsInChildren<Renderer>(true);

        foreach (Renderer bladeRenderer in bladeRenderers)
        {
            // Creates private runtime material copies.
            Material[] materials = bladeRenderer.materials;

            foreach (Material material in materials)
            {
                if (!TryGetColour(material, out Color colour))
                    continue;

                MakeMaterialTransparent(material);

                fadeMaterials.Add(material);
                originalAlphas.Add(colour.a);
            }
        }

        materialsPrepared = true;
    }

    private IEnumerator FadeBlade(
        float startingAmount,
        float endingAmount,
        bool deactivateAfterFade)
    {
        float elapsedTime = 0f;

        SetFadeAmount(startingAmount);

        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;

            float progress =
                Mathf.Clamp01(elapsedTime / fadeDuration);

            float fadeAmount = Mathf.Lerp(
                startingAmount,
                endingAmount,
                progress
            );

            SetFadeAmount(fadeAmount);

            yield return null;
        }

        SetFadeAmount(endingAmount);
        currentFade = null;

        if (deactivateAfterFade)
        {
            gameObject.SetActive(false);
        }
    }

    private void SetFadeAmount(float fadeAmount)
    {
        for (int i = 0; i < fadeMaterials.Count; i++)
        {
            Material material = fadeMaterials[i];
            float alpha = originalAlphas[i] * fadeAmount;

            if (material.HasProperty("_BaseColor"))
            {
                Color colour =
                    material.GetColor("_BaseColor");

                colour.a = alpha;

                material.SetColor("_BaseColor", colour);
            }
            else if (material.HasProperty("_Color"))
            {
                Color colour = material.color;
                colour.a = alpha;
                material.color = colour;
            }
        }
    }

    private bool TryGetColour(
        Material material,
        out Color colour)
    {
        if (material.HasProperty("_BaseColor"))
        {
            colour = material.GetColor("_BaseColor");
            return true;
        }

        if (material.HasProperty("_Color"))
        {
            colour = material.color;
            return true;
        }

        colour = Color.white;
        return false;
    }

    private void MakeMaterialTransparent(Material material)
    {
        material.SetFloat("_Surface", 1f);
        material.SetFloat(
            "_SrcBlend",
            (float)BlendMode.SrcAlpha
        );
        material.SetFloat(
            "_DstBlend",
            (float)BlendMode.OneMinusSrcAlpha
        );
        material.SetFloat("_ZWrite", 0f);

        material.SetOverrideTag(
            "RenderType",
            "Transparent"
        );

        material.EnableKeyword(
            "_SURFACE_TYPE_TRANSPARENT"
        );

        material.DisableKeyword(
            "_ALPHAPREMULTIPLY_ON"
        );

        material.renderQueue =
            (int)RenderQueue.Transparent;

        material.SetShaderPassEnabled(
            "ShadowCaster",
            false
        );
    }
}