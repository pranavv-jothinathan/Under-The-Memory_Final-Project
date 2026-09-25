using UnityEngine;

/// <summary>
/// 小狗只在水上、且咖啡厅或市政厅 NPC 还在时出现。两个 director 各自汇报自己的人物状态。
/// </summary>
public static class FetchDogPresence
{
    private static bool cafeNpcs;
    private static bool cityHallNpcs;
    private static bool surface = true;

    public static void SetCafeNpcsPresent(bool present)
    {
        cafeNpcs = present;
        Refresh();
    }

    public static void SetCityHallNpcsPresent(bool present)
    {
        cityHallNpcs = present;
        Refresh();
    }

    public static void NotifyWorld(WorldSwitchManager.CurrentWorld world)
    {
        surface = world == WorldSwitchManager.CurrentWorld.Surface;
        Refresh();
    }

    public static void Refresh()
    {
        bool show = surface && (cafeNpcs || cityHallNpcs);

        FetchDog dog = Object.FindFirstObjectByType<FetchDog>(FindObjectsInactive.Include);
        if (dog != null && dog.gameObject.activeSelf != show)
        {
            if (!show)
                dog.StopFetchAudio();
            dog.gameObject.SetActive(show);
        }

        FetchBall ball = Object.FindFirstObjectByType<FetchBall>(FindObjectsInactive.Include);
        if (ball != null && ball.gameObject.activeSelf != show)
            ball.gameObject.SetActive(show);
    }
}
