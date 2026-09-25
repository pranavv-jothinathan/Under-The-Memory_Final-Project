using UnityEngine;
using UnityEngine.Rendering;

public class UnderwaterCamera : MonoBehaviour
{
    [Header("Depth")]
    [SerializeField] private GameObject playerCamera;
    [SerializeField] private int depth = 0;

    [Header("Post Processing Volume")]
    [SerializeField] private Volume postPorcesssingVolume;

    [Header("Post Processing Profiles")]
    [SerializeField] private VolumeProfile surface;
    [SerializeField] private VolumeProfile underwater;

    void Start()
    {
        if(playerCamera.transform.position.y < depth)
        {
            EnableEffects(true);
        }
        else
        {
            EnableEffects(false);
        }
        
    }
    
    private void EnableEffects(bool active)
    {
        if (active)
        {
            RenderSettings.fog = (true);
            postPorcesssingVolume.profile = underwater;
        }
        else
        {
            postPorcesssingVolume.profile = surface;
            RenderSettings.fog = (false);
        }
    }
}
