using UnityEngine;

/// <summary>
/// Overlay feedback on 移动教学UI: cyan breathing for the current module,
/// a green check when done, a slight dim when waiting, and a B-button flash
/// while the player should switch modes.
/// </summary>
public sealed class TitleTutorialStepHud : MonoBehaviour
{
    public enum ModuleState
    {
        Pending,
        Active,
        Done
    }

    [System.Serializable]
    struct UvRect
    {
        public Vector2 min;
        public Vector2 size;
    }

    [SerializeField] UvRect m_Walking = new UvRect { min = new Vector2(0.035f, 0.02f), size = new Vector2(0.30f, 0.39f) };
    [SerializeField] UvRect m_Swimming = new UvRect { min = new Vector2(0.35f, 0.02f), size = new Vector2(0.30f, 0.39f) };
    [SerializeField] UvRect m_Teleport = new UvRect { min = new Vector2(0.655f, 0.02f), size = new Vector2(0.31f, 0.39f) };
    [SerializeField] UvRect m_ModeButton = new UvRect { min = new Vector2(0.818f, 0.668f), size = new Vector2(0.155f, 0.080f) };
    [SerializeField] Color m_Cyan = new Color(0.15f, 0.95f, 0.9f, 0.55f);
    [SerializeField] Color m_Dim = new Color(0f, 0f, 0f, 0.42f);
    [SerializeField] Color m_Check = new Color(0.25f, 0.95f, 0.4f, 0.95f);
    [SerializeField] float m_BreathSpeed = 2.2f;

    ModuleState m_Walk = ModuleState.Pending;
    ModuleState m_Swim = ModuleState.Pending;
    ModuleState m_TeleportState = ModuleState.Pending;
    bool m_FlashModeButton;
    ModuleVisual m_WalkVisual;
    ModuleVisual m_SwimVisual;
    ModuleVisual m_TeleportVisual;
    OverlayQuad[] m_ModeFlash;
    bool m_Built;

    public void SetStates(ModuleState walk, ModuleState swim, ModuleState teleport, bool flashModeButton)
    {
        EnsureBuilt();
        m_Walk = walk;
        m_Swim = swim;
        m_TeleportState = teleport;
        m_FlashModeButton = flashModeButton;
        ApplyStatic();
    }

    void LateUpdate()
    {
        if (!m_Built)
            return;

        float breath = 0.35f + 0.65f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * m_BreathSpeed));
        m_WalkVisual.SetBreath(m_Walk == ModuleState.Active ? breath : 0f);
        m_SwimVisual.SetBreath(m_Swim == ModuleState.Active ? breath : 0f);
        m_TeleportVisual.SetBreath(m_TeleportState == ModuleState.Active ? breath : 0f);

        if (m_ModeFlash != null)
        {
            bool show = m_FlashModeButton;
            float pulse = 0.2f + 0.55f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f));
            for (int i = 0; i < m_ModeFlash.Length; i++)
            {
                Renderer renderer = m_ModeFlash[i].renderer;
                if (renderer == null)
                    continue;

                renderer.enabled = show;
                if (!show)
                    continue;

                Color color = m_Cyan;
                color.a = pulse;
                renderer.material.color = color;
            }
        }
    }

    void EnsureBuilt()
    {
        if (m_Built)
            return;

        m_WalkVisual = BuildModule("WalkFeedback", m_Walking);
        m_SwimVisual = BuildModule("SwimFeedback", m_Swimming);
        m_TeleportVisual = BuildModule("TeleportFeedback", m_Teleport);
        m_ModeFlash = CreateFrame("ModeButtonFlash", m_ModeButton, m_Cyan);
        m_Built = true;
        ApplyStatic();
    }

    void ApplyStatic()
    {
        m_WalkVisual.SetDone(m_Walk == ModuleState.Done);
        m_WalkVisual.SetDim(m_Walk == ModuleState.Pending);
        m_SwimVisual.SetDone(m_Swim == ModuleState.Done);
        m_SwimVisual.SetDim(m_Swim == ModuleState.Pending);
        m_TeleportVisual.SetDone(m_TeleportState == ModuleState.Done);
        m_TeleportVisual.SetDim(m_TeleportState == ModuleState.Pending);
    }

    ModuleVisual BuildModule(string name, UvRect rect)
    {
        var visual = new ModuleVisual();
        visual.highlight = CreateFrame(name + "Highlight", rect, m_Cyan);
        visual.dim = CreateQuad(name + "Dim", rect, 0.008f, m_Dim);
        visual.check = CreateCheck(name + "Check", rect);
        return visual;
    }

    OverlayQuad[] CreateFrame(string name, UvRect rect, Color color)
    {
        const float thickness = 0.012f;
        return new[]
        {
            CreateQuad(name + "N", new UvRect { min = new Vector2(rect.min.x, rect.min.y + rect.size.y - thickness), size = new Vector2(rect.size.x, thickness) }, 0.01f, color),
            CreateQuad(name + "S", new UvRect { min = rect.min, size = new Vector2(rect.size.x, thickness) }, 0.01f, color),
            CreateQuad(name + "W", new UvRect { min = rect.min, size = new Vector2(thickness, rect.size.y) }, 0.01f, color),
            CreateQuad(name + "E", new UvRect { min = new Vector2(rect.min.x + rect.size.x - thickness, rect.min.y), size = new Vector2(thickness, rect.size.y) }, 0.01f, color)
        };
    }

    OverlayQuad CreateCheck(string name, UvRect rect)
    {
        var checkRect = new UvRect
        {
            min = new Vector2(rect.min.x + rect.size.x - 0.07f, rect.min.y + rect.size.y - 0.08f),
            size = new Vector2(0.06f, 0.07f)
        };
        OverlayQuad quad = CreateQuad(name, checkRect, 0.014f, m_Check);
        if (quad.renderer != null)
            quad.renderer.material.mainTexture = CreateCheckTexture();
        return quad;
    }

    OverlayQuad CreateQuad(string name, UvRect rect, float z, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = name;
        go.transform.SetParent(transform, false);
        go.transform.localPosition = UvToLocal(rect, z);
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = new Vector3(Mathf.Max(0.001f, rect.size.x), Mathf.Max(0.001f, rect.size.y), 1f);

        Collider collider = go.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);

        Renderer renderer = go.GetComponent<Renderer>();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.material = CreateOverlayMaterial(color);
        renderer.enabled = false;

        return new OverlayQuad { renderer = renderer };
    }

    static Vector3 UvToLocal(UvRect rect, float z)
    {
        return new Vector3(
            rect.min.x + rect.size.x * 0.5f - 0.5f,
            rect.min.y + rect.size.y * 0.5f - 0.5f,
            -z);
    }

    static Material CreateOverlayMaterial(Color color)
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");

        var material = new Material(shader) { color = color };
        return material;
    }

    static Texture2D CreateCheckTexture()
    {
        const int size = 32;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Color clear = new Color(0f, 0f, 0f, 0f);
        Color solid = Color.white;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
                texture.SetPixel(x, y, clear);
        }

        DrawLine(texture, 7, 15, 13, 8, 3, solid);
        DrawLine(texture, 13, 8, 25, 24, 3, solid);
        texture.Apply();
        return texture;
    }

    static void DrawLine(Texture2D texture, int x0, int y0, int x1, int y1, int radius, Color color)
    {
        int steps = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0));
        for (int i = 0; i <= steps; i++)
        {
            float t = steps == 0 ? 0f : i / (float)steps;
            int cx = Mathf.RoundToInt(Mathf.Lerp(x0, x1, t));
            int cy = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t));
            for (int y = cy - radius; y <= cy + radius; y++)
            {
                for (int x = cx - radius; x <= cx + radius; x++)
                {
                    if (x < 0 || y < 0 || x >= texture.width || y >= texture.height)
                        continue;
                    if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= radius * radius)
                        texture.SetPixel(x, y, color);
                }
            }
        }
    }

    struct OverlayQuad
    {
        public Renderer renderer;
    }

    struct ModuleVisual
    {
        public OverlayQuad[] highlight;
        public OverlayQuad dim;
        public OverlayQuad check;

        public void SetDim(bool dimmed)
        {
            if (dim.renderer != null)
                dim.renderer.enabled = dimmed;
        }

        public void SetDone(bool done)
        {
            if (check.renderer != null)
                check.renderer.enabled = done;
        }

        public void SetBreath(float amount)
        {
            if (highlight == null)
                return;

            bool show = amount > 0.01f;
            for (int i = 0; i < highlight.Length; i++)
            {
                Renderer renderer = highlight[i].renderer;
                if (renderer == null)
                    continue;

                renderer.enabled = show;
                if (!show)
                    continue;

                Color color = renderer.material.color;
                color.a = 0.25f + 0.7f * amount;
                renderer.material.color = color;
            }
        }
    }
}
