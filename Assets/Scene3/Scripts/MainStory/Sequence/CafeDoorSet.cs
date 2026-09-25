using UnityEngine;

/// <summary>
/// 咖啡厅那四扇门的协调器：任意一扇打开后，全部停掉呼吸发光。
/// </summary>
public class CafeDoorSet : MonoBehaviour
{
    [SerializeField] private CafeSwingDoor[] doors;

    public event System.Action FirstOpened;

    public bool HasOpened { get; private set; }

    public void Bind(CafeSwingDoor[] boundDoors)
    {
        Unbind();
        doors = boundDoors;
        Bind();
    }

    private void OnEnable()
    {
        Bind();
    }

    private void OnDisable()
    {
        Unbind();
    }

    public void SetClosedAndGlowing()
    {
        HasOpened = false;
        if (doors == null)
            return;

        for (int i = 0; i < doors.Length; i++)
        {
            if (doors[i] != null)
                doors[i].SetGlowEnabled(true);
        }
    }

    public void StopGlow()
    {
        if (doors == null)
            return;

        for (int i = 0; i < doors.Length; i++)
        {
            if (doors[i] != null)
                doors[i].SetGlowEnabled(false);
        }
    }

    private void Bind()
    {
        if (doors == null)
            return;

        for (int i = 0; i < doors.Length; i++)
        {
            if (doors[i] != null)
                doors[i].Opened += OnDoorOpened;
        }
    }

    private void Unbind()
    {
        if (doors == null)
            return;

        for (int i = 0; i < doors.Length; i++)
        {
            if (doors[i] != null)
                doors[i].Opened -= OnDoorOpened;
        }
    }

    private void OnDoorOpened(CafeSwingDoor door)
    {
        if (HasOpened)
            return;

        HasOpened = true;
        StopGlow();
        FirstOpened?.Invoke();
    }
}
