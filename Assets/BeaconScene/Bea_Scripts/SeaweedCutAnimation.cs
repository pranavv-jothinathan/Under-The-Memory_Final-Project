using System.Collections;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Rendering;

public class SeaweedCutAnimation : MonoBehaviour
{
    [Header("Seaweed Objects")]
    [SerializeField] private GameObject fullSeaweed;
    [SerializeField] private GameObject cutPiecesParent;
    [SerializeField] private Transform seaweedLeftSide;
    [SerializeField] private Transform seaweedRightSide;

    [Header("Animation Timing")]
    [SerializeField] private float leftRotationDuration = 1.25f;
    [SerializeField] private float rightMovementDuration = 2f;

    [Header("Left-Side Rotation")]
    [SerializeField] private float leftStartXRotation = -90f;
    [SerializeField] private float leftEndXRotation = 60f;

    [Header("Right-Side Final Position")]
    [SerializeField] private float rightFinalYPosition = 0f;
    [SerializeField] private float rightFinalZPosition = 0.6f;

    [SerializeField] private float fadeDuration = 2f;
    [Range(0f, 1f)]
    [SerializeField] private float fadeStartProgress = 0.5f;

    [Header("Blade")]
    [SerializeField] private BladeFadeController bladeFadeController;

    private bool cutStarted;

    private Vector3 leftOriginalEulerAngles;
    private Vector3 rightStartPosition;

    private InteractionAudio interactionAudio;

    private void Awake()
    {
        interactionAudio = GetComponent<InteractionAudio>();
    }

    private void Start()
    {
        cutPiecesParent.SetActive(false);
        fullSeaweed.SetActive(true);
    }

    public void StartCutAnimation()
    {
        if (cutStarted)
            return;

        cutStarted = true;
        if (bladeFadeController != null)
        {
            bladeFadeController.FadeOut();
        }
        else
        {
            Debug.LogWarning(
                "Blade Fade Controller is not assigned."
            );
        }

        fullSeaweed.SetActive(false);
        cutPiecesParent.SetActive(true);

        if (interactionAudio != null)
        {
            interactionAudio.PlayActionSound();
        }

        leftOriginalEulerAngles = seaweedLeftSide.localEulerAngles;
        rightStartPosition = seaweedRightSide.localPosition;

        Vector3 startingRotation = leftOriginalEulerAngles;
        startingRotation.x = leftStartXRotation;
        seaweedLeftSide.localEulerAngles = startingRotation;

        StartCoroutine(AnimateLeftSide());
        StartCoroutine(AnimateRightSide());
    }

    private IEnumerator AnimateLeftSide()
    {
        float elapsedTime = 0f;
        bool fadeStarted = false;

        while (elapsedTime < leftRotationDuration)
        {
            elapsedTime += Time.deltaTime;

            float progress =
                Mathf.Clamp01(elapsedTime / leftRotationDuration);

            float currentX = Mathf.Lerp(
                leftStartXRotation,
                leftEndXRotation,
                progress
            );

            Vector3 rotation = leftOriginalEulerAngles;
            rotation.x = currentX;
            seaweedLeftSide.localEulerAngles = rotation;

            if (!fadeStarted && progress >= fadeStartProgress)
            {
                fadeStarted = true;

                StartCoroutine(
                    FadeOutPiece(seaweedLeftSide.gameObject)
                );
            }

            yield return null;
        }

        Vector3 finalRotation = leftOriginalEulerAngles;
        finalRotation.x = leftEndXRotation;
        seaweedLeftSide.localEulerAngles = finalRotation;
    }

    private IEnumerator AnimateRightSide()
    {
        float elapsedTime = 0f;
        bool fadeStarted = false;

        Vector3 finalPosition = new Vector3(
            rightStartPosition.x,
            rightFinalYPosition,
            rightFinalZPosition
        );

        while (elapsedTime < rightMovementDuration)
        {
            elapsedTime += Time.deltaTime;

            float progress =
                Mathf.Clamp01(elapsedTime / rightMovementDuration);

            seaweedRightSide.localPosition =
                Vector3.Lerp(rightStartPosition, finalPosition, progress);

            if (!fadeStarted && progress >= fadeStartProgress)
            {
                fadeStarted = true;

                StartCoroutine(
                    FadeOutPiece(seaweedRightSide.gameObject)
                );
            }

            yield return null;
        }

        seaweedRightSide.localPosition = finalPosition;
    }

    private IEnumerator FadeOutPiece(GameObject piece)
    {
        Renderer[] pieceRenderers =
            piece.GetComponentsInChildren<Renderer>(true);

        List<Material> fadeMaterials = new List<Material>();

        foreach (Renderer pieceRenderer in pieceRenderers)
        {
            // Creates private runtime copies, so the original
            // full seaweed material remains unaffected.
            Material[] materials = pieceRenderer.materials;

            foreach (Material material in materials)
            {
                MakeMaterialTransparent(material);
                fadeMaterials.Add(material);
            }
        }

        float elapsedTime = 0f;

        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;

            float progress =
                Mathf.Clamp01(elapsedTime / fadeDuration);

            float currentAlpha = 1f - progress;

            foreach (Material material in fadeMaterials)
            {
                if (material.HasProperty("_BaseColor"))
                {
                    Color colour = material.GetColor("_BaseColor");
                    colour.a = currentAlpha;
                    material.SetColor("_BaseColor", colour);
                }
                else if (material.HasProperty("_Color"))
                {
                    Color colour = material.color;
                    colour.a = currentAlpha;
                    material.color = colour;
                }
            }

            yield return null;
        }

        piece.SetActive(false);
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

        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");

        material.renderQueue = (int)RenderQueue.Transparent;
        material.SetShaderPassEnabled("ShadowCaster", false);
    }
}