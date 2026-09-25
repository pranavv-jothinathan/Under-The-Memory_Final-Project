using System.Collections;
using UnityEngine;

public class NPCDialogueController : MonoBehaviour
{
    public AudioSource[] dialogueLines;

    [Header("Dialogue Settings")]
    public float gapBetweenLines = 0.4f;

    private bool isPlaying = false;

    public void StartDialogue()
    {
        if (!isPlaying)
        {
            StartCoroutine(PlayDialogue());
        }
    }

    IEnumerator PlayDialogue()
    {
        isPlaying = true;

        for (int i = 0; i < dialogueLines.Length; i++)
        {
            AudioSource line = dialogueLines[i];

            if (line != null)
            {
                line.Play();

                yield return new WaitWhile(() => line.isPlaying);

                yield return new WaitForSeconds(gapBetweenLines);
            }
        }

        isPlaying = false;
    }

    // 临时测试：按数字 1 开始整段对话
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            StartDialogue();
        }
    }
}