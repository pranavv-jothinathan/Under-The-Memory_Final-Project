using UnityEngine;

public class StructureDust : MonoBehaviour
{
    [SerializeField] private ParticleSystem stoneDust;

    public void PlayStoneDust()
    {
        if (stoneDust == null)
            return;

        stoneDust.Stop(
            true,
            ParticleSystemStopBehavior.StopEmittingAndClear
        );

        stoneDust.Play(true);
    }
}
