using System.Collections;
using TMPro;
using UnityEngine;

public class TypewriterText : MonoBehaviour
{
    [SerializeField] private TMP_Text textBox;
    [SerializeField] private float secondsPerCharacter = 0.08f;
    [SerializeField] private float startDelay = 1f;

    private IEnumerator Start()
    {
        if (textBox == null)
        {
            textBox = GetComponent<TMP_Text>();
        }

        if (textBox == null)
        {
            Debug.LogError("TypewriterText：没有找到 TMP 文字组件。");
            yield break;
        }

        textBox.ForceMeshUpdate();
        int totalCharacters = textBox.textInfo.characterCount;
        textBox.maxVisibleCharacters = 0;

        yield return new WaitForSecondsRealtime(startDelay);

        for (int i = 1; i <= totalCharacters; i++)
        {
            textBox.maxVisibleCharacters = i;
            yield return new WaitForSecondsRealtime(secondsPerCharacter);
        }
    }
}