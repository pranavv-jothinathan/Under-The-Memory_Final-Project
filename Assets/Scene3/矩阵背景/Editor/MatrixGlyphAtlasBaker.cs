using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 把一套拉丁字符烘成 N×N 的字形图集 PNG，给 Scene3/MatrixRain 用。
/// 图集第 0 格在左上角，逐行往右排，和 shader 里的取格方式一致。
/// </summary>
public class MatrixGlyphAtlasBaker : EditorWindow
{
    private enum FontSource
    {
        系统字体,
        工程内字体资源,
    }

    private enum ChannelMode
    {
        自动,
        Alpha通道,
        Red通道,
    }

    private const string BlitShaderName = "Hidden/Scene3/GlyphBlit";
    private const string RainShaderName = "Scene3/MatrixRain";

    private const string VoidMaterialPath =
        "Assets/Scene3/矩阵背景/VOiD1 Gaming - Free Matrix Shader Unity URP/" +
        "Material/Shader Graphs_New Shader Graph.mat";

    private const string DefaultOutputPath =
        "Assets/Scene3/矩阵背景/Textures/T_MatrixGlyphAtlas.png";

    private FontSource fontSource = FontSource.系统字体;
    private string osFontName = "Consolas";
    private Font projectFont;
    private FontStyle fontStyle = FontStyle.Bold;

    private int atlasColumns = 16;
    private int atlasRows = 16;
    private int cellSize = 64;
    private int padding = 6;

    private bool includeDigits = true;
    private bool includeUppercase = true;
    private bool includeLowercase = false;
    private bool includeSymbols = true;
    private bool includeMirrored = true;
    private string extraCharacters = string.Empty;

    private ChannelMode channelMode = ChannelMode.自动;
    private string outputPath = DefaultOutputPath;

    private bool assignToRainMaterials = true;
    private bool assignToVoidMaterial = true;

    private Vector2 scroll;

    private struct GlyphEntry
    {
        public char character;
        public bool mirrored;
    }

    [MenuItem("Prefab Library/矩阵字幕帘/烘焙字符图集")]
    public static void Open()
    {
        MatrixGlyphAtlasBaker window = GetWindow<MatrixGlyphAtlasBaker>(true, "矩阵字符图集", true);
        window.minSize = new Vector2(430f, 560f);
        window.Show();
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField("字体", EditorStyles.boldLabel);
        fontSource = (FontSource)EditorGUILayout.EnumPopup("来源", fontSource);

        if (fontSource == FontSource.系统字体)
        {
            osFontName = EditorGUILayout.TextField("系统字体名", osFontName);
            EditorGUILayout.HelpBox(
                "等宽字体效果最好：Consolas / Courier New / Lucida Console。" +
                "名字打错会被 Unity 静默换成 Arial。",
                MessageType.None
            );
        }
        else
        {
            projectFont = (Font)EditorGUILayout.ObjectField(
                "字体资源", projectFont, typeof(Font), false
            );
        }

        fontStyle = (FontStyle)EditorGUILayout.EnumPopup("字重", fontStyle);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("图集尺寸", EditorStyles.boldLabel);
        atlasColumns = EditorGUILayout.IntSlider("列数", atlasColumns, 4, 32);
        atlasRows = EditorGUILayout.IntSlider("行数", atlasRows, 4, 32);
        cellSize = EditorGUILayout.IntPopup(
            "每格像素",
            cellSize,
            new[] { "32", "64", "128" },
            new[] { 32, 64, 128 }
        );
        padding = EditorGUILayout.IntSlider("格内留白", padding, 0, cellSize / 4);

        int width = atlasColumns * cellSize;
        int height = atlasRows * cellSize;
        EditorGUILayout.LabelField(
            "输出分辨率",
            $"{width} × {height}  （{atlasColumns * atlasRows} 格）"
        );

        if (width > 4096 || height > 4096)
        {
            EditorGUILayout.HelpBox("超过 4096，先把每格像素调小。", MessageType.Error);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("字符集", EditorStyles.boldLabel);
        includeDigits = EditorGUILayout.Toggle("数字 0-9", includeDigits);
        includeUppercase = EditorGUILayout.Toggle("大写 A-Z", includeUppercase);
        includeLowercase = EditorGUILayout.Toggle("小写 a-z", includeLowercase);
        includeSymbols = EditorGUILayout.Toggle("符号", includeSymbols);
        includeMirrored = EditorGUILayout.Toggle("加入镜像变体", includeMirrored);
        extraCharacters = EditorGUILayout.TextField("额外字符", extraCharacters);

        List<GlyphEntry> preview = BuildEntries();
        EditorGUILayout.LabelField(
            "可用字形",
            preview.Count == 0
                ? "0（至少勾一类）"
                : $"{preview.Count} 个，不足 {atlasColumns * atlasRows} 格时会循环填充"
        );

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("输出", EditorStyles.boldLabel);
        channelMode = (ChannelMode)EditorGUILayout.EnumPopup("覆盖度通道", channelMode);
        outputPath = EditorGUILayout.TextField("路径", outputPath);
        assignToRainMaterials = EditorGUILayout.Toggle(
            "自动填进 MatrixRain 材质", assignToRainMaterials
        );
        assignToVoidMaterial = EditorGUILayout.Toggle(
            "顺便补给 VOiD1 旧材质", assignToVoidMaterial
        );

        EditorGUILayout.Space();

        using (new EditorGUI.DisabledScope(preview.Count == 0 || width > 4096 || height > 4096))
        {
            if (GUILayout.Button("烘焙", GUILayout.Height(32f)))
            {
                Bake();
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void Bake()
    {
        int columns = Mathf.Max(1, atlasColumns);
        int rows = Mathf.Max(1, atlasRows);
        int cell = Mathf.Max(8, cellSize);
        int width = columns * cell;
        int height = rows * cell;

        List<GlyphEntry> entries = BuildEntries();

        if (entries.Count == 0)
        {
            Debug.LogError("MatrixGlyphAtlasBaker: 字符集是空的。");
            return;
        }

        int glyphPixelSize = Mathf.Max(8, cell - padding * 2);

        Font font = ResolveFont(glyphPixelSize, out string fontLabel, out bool disposeFont);

        if (font == null)
            return;

        Shader blitShader = Shader.Find(BlitShaderName);

        if (blitShader == null)
        {
            Debug.LogError(
                $"MatrixGlyphAtlasBaker: 找不到 {BlitShaderName}，" +
                "确认 Assets/Scene3/矩阵背景/Shaders/GlyphBlit.shader 已经导入。"
            );

            if (disposeFont)
                DestroyImmediate(font);

            return;
        }

        font.RequestCharactersInTexture(BuildRequestString(entries), glyphPixelSize, fontStyle);

        Texture fontTexture = font.material != null ? font.material.mainTexture : null;

        if (fontTexture == null)
        {
            Debug.LogError(
                "MatrixGlyphAtlasBaker: 拿不到字体图集贴图，" +
                "工程内字体资源要在导入设置里选 Dynamic。"
            );

            if (disposeFont)
                DestroyImmediate(font);

            return;
        }

        Material blit = new Material(blitShader) { hideFlags = HideFlags.HideAndDontSave };
        blit.SetTexture("_MainTex", fontTexture);
        blit.SetFloat("_AutoChannel", channelMode == ChannelMode.自动 ? 1f : 0f);
        blit.SetVector(
            "_ChannelMask",
            channelMode == ChannelMode.Red通道
                ? new Vector4(1f, 0f, 0f, 0f)
                : new Vector4(0f, 0f, 0f, 1f)
        );

        RenderTexture target = RenderTexture.GetTemporary(
            width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear
        );

        RenderTexture previousActive = RenderTexture.active;
        Texture2D baked = null;
        int drawn = 0;
        int missing = 0;

        try
        {
            RenderTexture.active = target;
            GL.Clear(true, true, new Color(1f, 1f, 1f, 0f));
            GL.PushMatrix();
            GL.LoadPixelMatrix(0f, width, height, 0f);
            blit.SetPass(0);
            GL.Begin(GL.QUADS);

            int cellCount = columns * rows;
            float available = cell - padding * 2f;

            for (int index = 0; index < cellCount; index++)
            {
                GlyphEntry entry = entries[index % entries.Count];

                if (!font.GetCharacterInfo(
                        entry.character, out CharacterInfo info, glyphPixelSize, fontStyle))
                {
                    missing++;
                    continue;
                }

                if (info.glyphWidth <= 0 || info.glyphHeight <= 0)
                {
                    missing++;
                    continue;
                }

                float scale = Mathf.Min(
                    available / info.glyphWidth,
                    available / info.glyphHeight
                );

                float drawWidth = info.glyphWidth * scale;
                float drawHeight = info.glyphHeight * scale;

                int cellX = index % columns;
                int cellY = index / columns;

                // 每个字形按自身包围盒居中，而不是按基线对齐，网格看起来更整齐。
                float left = cellX * cell + (cell - drawWidth) * 0.5f;
                float top = cellY * cell + (cell - drawHeight) * 0.5f;

                Vector2 uvTopLeft = info.uvTopLeft;
                Vector2 uvTopRight = info.uvTopRight;
                Vector2 uvBottomRight = info.uvBottomRight;
                Vector2 uvBottomLeft = info.uvBottomLeft;

                if (entry.mirrored)
                {
                    (uvTopLeft, uvTopRight) = (uvTopRight, uvTopLeft);
                    (uvBottomLeft, uvBottomRight) = (uvBottomRight, uvBottomLeft);
                }

                GL.TexCoord2(uvTopLeft.x, uvTopLeft.y);
                GL.Vertex3(left, top, 0f);

                GL.TexCoord2(uvTopRight.x, uvTopRight.y);
                GL.Vertex3(left + drawWidth, top, 0f);

                GL.TexCoord2(uvBottomRight.x, uvBottomRight.y);
                GL.Vertex3(left + drawWidth, top + drawHeight, 0f);

                GL.TexCoord2(uvBottomLeft.x, uvBottomLeft.y);
                GL.Vertex3(left, top + drawHeight, 0f);

                drawn++;
            }

            GL.End();
            GL.PopMatrix();

            baked = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            baked.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
            baked.Apply();
        }
        finally
        {
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(target);
            DestroyImmediate(blit);

            if (disposeFont)
                DestroyImmediate(font);
        }

        if (drawn == 0 || baked == null)
        {
            Debug.LogError(
                "MatrixGlyphAtlasBaker: 一个字形都没画出来，换个字体或改覆盖度通道再试。"
            );

            if (baked != null)
                DestroyImmediate(baked);

            return;
        }

        string projectRoot = Path.GetDirectoryName(Application.dataPath);
        string absolutePath = Path.Combine(projectRoot, outputPath);
        string absoluteDirectory = Path.GetDirectoryName(absolutePath);

        if (!Directory.Exists(absoluteDirectory))
            Directory.CreateDirectory(absoluteDirectory);

        File.WriteAllBytes(absolutePath, baked.EncodeToPNG());
        DestroyImmediate(baked);

        AssetDatabase.Refresh();
        AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceUpdate);
        ConfigureImporter(outputPath, Mathf.Max(width, height));

        Texture2D atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(outputPath);

        var log = new StringBuilder();
        log.AppendLine("MatrixGlyphAtlasBaker: 图集烘好了。");
        log.AppendLine($"  · 字体 {fontLabel}，字重 {fontStyle}");
        log.AppendLine($"  · {columns}×{rows} 格，{width}×{height} 像素，每格 {cell}px");
        log.AppendLine($"  · 画出 {drawn} 格，跳过 {missing} 格");
        log.AppendLine($"  · {outputPath}");

        if (assignToRainMaterials)
            log.AppendLine($"  · 填进 {AssignToRainMaterials(atlas, columns, rows)} 个 MatrixRain 材质");

        if (assignToVoidMaterial)
            log.AppendLine($"  · VOiD1 旧材质：{AssignToVoidMaterial(atlas, columns, rows)}");

        AssetDatabase.SaveAssets();
        Debug.Log(log.ToString(), atlas);
        EditorGUIUtility.PingObject(atlas);
    }

    private Font ResolveFont(int size, out string label, out bool disposable)
    {
        if (fontSource == FontSource.工程内字体资源)
        {
            disposable = false;
            label = projectFont != null ? projectFont.name : "（空）";

            if (projectFont == null)
                Debug.LogError("MatrixGlyphAtlasBaker: 没指定字体资源。");

            return projectFont;
        }

        disposable = true;
        label = osFontName;

        string[] installed = Font.GetOSInstalledFontNames();
        bool found = false;

        for (int i = 0; i < installed.Length; i++)
        {
            if (string.Equals(installed[i], osFontName, System.StringComparison.OrdinalIgnoreCase))
            {
                found = true;
                break;
            }
        }

        if (!found)
        {
            Debug.LogWarning(
                $"MatrixGlyphAtlasBaker: 系统里没有「{osFontName}」，Unity 会退回 Arial。"
            );
        }

        Font font = Font.CreateDynamicFontFromOSFont(osFontName, size);

        if (font == null)
            Debug.LogError($"MatrixGlyphAtlasBaker: 创建字体「{osFontName}」失败。");

        return font;
    }

    private List<GlyphEntry> BuildEntries()
    {
        var characters = new List<char>();

        if (includeDigits)
            characters.AddRange("0123456789");

        if (includeUppercase)
            characters.AddRange("ABCDEFGHIJKLMNOPQRSTUVWXYZ");

        if (includeLowercase)
            characters.AddRange("abcdefghijklmnopqrstuvwxyz");

        if (includeSymbols)
            characters.AddRange("+-*/=<>|_^~$%#@&!?:;.,'\"()[]{}\\");

        if (!string.IsNullOrEmpty(extraCharacters))
        {
            for (int i = 0; i < extraCharacters.Length; i++)
            {
                if (!char.IsWhiteSpace(extraCharacters[i]))
                    characters.Add(extraCharacters[i]);
            }
        }

        var entries = new List<GlyphEntry>(characters.Count * 2);

        for (int i = 0; i < characters.Count; i++)
            entries.Add(new GlyphEntry { character = characters[i], mirrored = false });

        if (includeMirrored)
        {
            for (int i = 0; i < characters.Count; i++)
                entries.Add(new GlyphEntry { character = characters[i], mirrored = true });
        }

        return entries;
    }

    private static string BuildRequestString(List<GlyphEntry> entries)
    {
        var builder = new StringBuilder(entries.Count);

        for (int i = 0; i < entries.Count; i++)
            builder.Append(entries[i].character);

        return builder.ToString();
    }

    private static void ConfigureImporter(string assetPath, int longestSide)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;

        if (importer == null)
            return;

        importer.textureType = TextureImporterType.Default;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.sRGBTexture = true;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.mipmapEnabled = true;
        importer.filterMode = FilterMode.Bilinear;
        importer.anisoLevel = 4;
        // Repeat 会让 flipbook 取格时相邻字形互相渗色。
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.maxTextureSize = Mathf.Max(512, Mathf.NextPowerOfTwo(longestSide));
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
    }

    private static int AssignToRainMaterials(Texture2D atlas, int columns, int rows)
    {
        string[] guids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/Scene3" });
        int count = 0;

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null || material.shader == null)
                continue;

            if (material.shader.name != RainShaderName)
                continue;

            material.SetTexture("_GlyphAtlas", atlas);
            material.SetFloat("_AtlasColumns", columns);
            material.SetFloat("_AtlasRows", rows);
            EditorUtility.SetDirty(material);
            count++;
        }

        return count;
    }

    private static string AssignToVoidMaterial(Texture2D atlas, int columns, int rows)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(VoidMaterialPath);

        if (material == null)
            return "找不到，跳过";

        if (!material.HasProperty("Texture2D_17B912DB"))
            return "属性名对不上，跳过";

        material.SetTexture("Texture2D_17B912DB", atlas);
        material.SetFloat("Vector1_2EEFDE17", columns);
        material.SetFloat("Vector1_73EC3F44", rows);
        EditorUtility.SetDirty(material);

        return "已补上 Letter Atlas";
    }
}
