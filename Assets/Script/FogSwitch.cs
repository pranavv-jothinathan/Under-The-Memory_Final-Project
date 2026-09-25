using UnityEngine;

public class FogSwitch : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnEnable()
    {
        RenderSettings.fog = true;
    }

    private void OnDisable()
    {
        RenderSettings.fog = false;
    }
}
