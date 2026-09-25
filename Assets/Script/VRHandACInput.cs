using UnityEngine;
using UnityEngine.InputSystem;

public class VRHandACInput : MonoBehaviour
{
    [SerializeField] private InputActionReference _gripActionInputReference;
    [SerializeField] private InputActionReference _triggerActionInputReference;

    private Animator _animator;

    
    private void Awake()
    {
        _animator = GetComponent<Animator>();


    }

    // Update is called once per frame
    void Update()
    {
        float gripValue = _gripActionInputReference.action.ReadValue<float>();
        float triggerValue = _triggerActionInputReference.action.ReadValue<float>();

        _animator.SetFloat("Grip", gripValue);
        _animator.SetFloat("Pinch", triggerValue);
    }
}
