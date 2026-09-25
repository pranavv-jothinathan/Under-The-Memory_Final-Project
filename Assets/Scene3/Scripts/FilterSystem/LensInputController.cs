using UnityEngine;
using UnityEngine.InputSystem;

public class LensInputController : MonoBehaviour
{
    public InputActionReference toggleMenuAction;
    public LensMenuController lensMenu;

    private void OnEnable()
    {
        toggleMenuAction.action.Enable();
        toggleMenuAction.action.performed += OnToggleMenu;
    }

    private void OnDisable()
    {
        toggleMenuAction.action.performed -= OnToggleMenu;
        toggleMenuAction.action.Disable();
    }

    private void OnToggleMenu(InputAction.CallbackContext context)
    {
        lensMenu.ToggleMenu();
    }
}