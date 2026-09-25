using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class RecoveryManager : MonoBehaviour
{
    [Header("Recovery Progress")]
    [SerializeField, Min(1)]
    private int totalRecoverableItems = 2;

    [Header("Plastic Bottle Step")]
    [SerializeField, Min(1)]
    private int plasticBottlesRequired = 2;

    [SerializeField]
    private GameObject bladeForSeaweed;

    [Header("Scanner Indicator")]
    [SerializeField]
    private Renderer indicatorRenderer;

    [SerializeField]
    private Material inactiveMaterial;

    [SerializeField]
    private Material partialMaterial;

    [SerializeField]
    private Material completeMaterial;

    [Header("Indicator Blink")]
    [SerializeField]
    private Material blinkOffMaterial;

    [SerializeField, Min(0.05f)]
    private float blinkInterval = 0.5f;

    [Header("Scanner Box Animation")]
    [SerializeField]
    private Animator scannerBoxAnimator;

    [SerializeField, Min(0f)]
    private float scannerBoxOpenDelay = 1f;

    [Header("Scanner Box Events")]
    [SerializeField]
    private UnityEvent onScannerBoxAnimationStarted;

    private int removedItems;
    private int removedPlasticBottles;
    private bool scannerBoxOpened;
    private Coroutine indicatorBlinkCoroutine;

    private void Awake()
    {
        if (bladeForSeaweed != null)
        {
            bladeForSeaweed.SetActive(false);
        }
    }

    private void Start()
    {
        UpdateIndicator();
    }

    public void RegisterRemoved(GameObject debris)
    {
        RegisterRemoved(debris, false);
    }

    public void RegisterRemoved(GameObject debris, bool isPlasticBottle)
    {
        removedItems++;

        Debug.Log(
            debris.name + " registered by Recovery Manager. Progress: " +
            removedItems + "/" + totalRecoverableItems
        );

        if (isPlasticBottle)
        {
            removedPlasticBottles++;

            Debug.Log(
                "Plastic bottles removed: " +
                removedPlasticBottles + "/" + plasticBottlesRequired
            );

            if (removedPlasticBottles >= plasticBottlesRequired &&
                bladeForSeaweed != null)
            {
                bladeForSeaweed.SetActive(true);

                BladeFadeController bladeFade =
                    bladeForSeaweed.GetComponent<BladeFadeController>();

                if (bladeFade != null)
                {
                    bladeFade.FadeIn();
                }
                else
                {
                    Debug.LogWarning(
                        "BladeFadeController is missing from the blade."
                    );
                }

                Debug.Log("Plastic bottle step complete. Blade revealed.");
            }
        }

        UpdateIndicator();

        if (removedItems >= totalRecoverableItems && !scannerBoxOpened)
        {
            scannerBoxOpened = true;

            Debug.Log("Debris clearance complete.");

            if (scannerBoxAnimator != null)
            {
                Invoke(nameof(OpenScannerBox), scannerBoxOpenDelay);
            }
            else
            {
                Debug.LogWarning(
                    "Scanner Box Animator has not been assigned."
                );
            }
        }
    }

    private void OpenScannerBox()
    {
        if (scannerBoxAnimator == null)
            return;

        onScannerBoxAnimationStarted?.Invoke();
        scannerBoxAnimator.SetTrigger("OpenBox");
    }

    private void UpdateIndicator()
    {
        if (indicatorRenderer == null)
            return;

        if (removedItems == 0)
        {
            StopIndicatorBlink();
            indicatorRenderer.sharedMaterial = inactiveMaterial;
        }
        else if (removedItems < totalRecoverableItems)
        {
            if (indicatorBlinkCoroutine == null)
            {
                indicatorBlinkCoroutine =
                    StartCoroutine(BlinkPartialIndicator());
            }
        }
        else
        {
            StopIndicatorBlink();
            indicatorRenderer.sharedMaterial = completeMaterial;
        }
    }

    private IEnumerator BlinkPartialIndicator()
    {
        while (removedItems > 0 &&
               removedItems < totalRecoverableItems)
        {
            indicatorRenderer.sharedMaterial = partialMaterial;
            yield return new WaitForSeconds(blinkInterval);

            if (removedItems >= totalRecoverableItems)
                break;

            if (blinkOffMaterial != null)
            {
                indicatorRenderer.sharedMaterial = blinkOffMaterial;
            }

            yield return new WaitForSeconds(blinkInterval);
        }

        indicatorBlinkCoroutine = null;
    }

    private void StopIndicatorBlink()
    {
        if (indicatorBlinkCoroutine != null)
        {
            StopCoroutine(indicatorBlinkCoroutine);
            indicatorBlinkCoroutine = null;
        }
    }
}