using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using System.IO;
using Object = UnityEngine.Object;

/// <summary>
/// Taaf Game Dev Tools — Custom MCP tools for autonomous Unity game development.
/// All tools accessible via MCP: execute_menu_item("GameDevTools/...")
/// Results written to Unity Console — read with read_console_logs.
/// Reference images go in: Assets/References/ (inside project, not project root)
/// </summary>
public static class ReferenceAnalyzerTool
{
    // References folder is INSIDE Assets for easy management
    private static string ReferencesPath =>
        Path.Combine(Application.dataPath, "References");

    private static string TempPath =>
        Path.Combine(Application.dataPath, "References", "_temp");

    // ─────────────────────────────────────────────
    // TOOL — Start Play Mode
    // MCP: execute_menu_item("GameDevTools/Start Play Mode")
    // ─────────────────────────────────────────────
    [MenuItem("GameDevTools/Start Play Mode")]
    public static void StartPlayMode()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.Log("[GameDevTools] Already in Play Mode.");
            return;
        }
        EditorApplication.isPlaying = true;
        Debug.Log("[GameDevTools] PLAY_MODE_STARTED — entering Play Mode now.");
    }

    // ─────────────────────────────────────────────
    // TOOL — Stop Play Mode
    // MCP: execute_menu_item("GameDevTools/Stop Play Mode")
    // ─────────────────────────────────────────────
    [MenuItem("GameDevTools/Stop Play Mode")]
    public static void StopPlayMode()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.Log("[GameDevTools] Not in Play Mode.");
            return;
        }
        EditorApplication.isPlaying = false;
        Debug.Log("[GameDevTools] PLAY_MODE_STOPPED.");
    }

    // ─────────────────────────────────────────────
    // TOOL — List reference images
    // MCP: execute_menu_item("GameDevTools/List Reference Images")
    // ─────────────────────────────────────────────
    [MenuItem("GameDevTools/List Reference Images")]
    public static void ListReferenceImages()
    {
        EnsureDirectories();

        var files = new List<string>();
        foreach (var ext in new[] { "*.png", "*.jpg", "*.jpeg" })
            files.AddRange(Directory.GetFiles(ReferencesPath, ext));

        if (files.Count == 0)
        {
            Debug.LogWarning(
                $"[GameDevTools] REFERENCES_EMPTY\n" +
                $"No reference images found.\n" +
                $"Add PNG/JPG files to: Assets/References/\n" +
                $"You can drag images directly into the Unity Project panel under Assets/References/");
            return;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[GameDevTools] REFERENCES_FOUND — {files.Count} image(s):");
        foreach (var f in files)
        {
            var info = new FileInfo(f);
            sb.AppendLine($"  {info.Name}  ({info.Length / 1024}KB)");
        }
        Debug.Log(sb.ToString());
    }

    // ─────────────────────────────────────────────
    // TOOL — Read reference image as base64 for Vision
    // Write filename to Assets/References/_temp/request.txt first
    // MCP: execute_menu_item("GameDevTools/Read Reference As Base64")
    // ─────────────────────────────────────────────
    [MenuItem("GameDevTools/Read Reference As Base64")]
    public static void ReadReferenceAsBase64()
    {
        EnsureDirectories();
        var requestFile = Path.Combine(TempPath, "request.txt");

        if (!File.Exists(requestFile))
        {
            Debug.LogWarning(
                "[GameDevTools] REQUEST_FILE_MISSING\n" +
                "Write the image filename to Assets/References/_temp/request.txt\n" +
                "Example: suika_gameplay.png");
            return;
        }

        var imageName = File.ReadAllText(requestFile).Trim();
        var imagePath = Path.Combine(ReferencesPath, imageName);

        if (!File.Exists(imagePath))
        {
            var available = GetAvailableImages();
            Debug.LogError(
                $"[GameDevTools] IMAGE_NOT_FOUND: {imageName}\n" +
                $"Available: {available}");
            return;
        }

        var bytes = File.ReadAllBytes(imagePath);
        var base64 = Convert.ToBase64String(bytes);
        var ext = Path.GetExtension(imageName).ToLower();
        var mime = (ext == ".jpg" || ext == ".jpeg") ? "image/jpeg" : "image/png";

        var outputFile = Path.Combine(TempPath, "base64_output.txt");
        File.WriteAllText(outputFile,
            $"FILE:{imageName}\n" +
            $"MIME:{mime}\n" +
            $"SIZE:{bytes.Length / 1024}KB\n" +
            $"BASE64:{base64}");

        Debug.Log(
            $"[GameDevTools] BASE64_READY\n" +
            $"File: {imageName} ({bytes.Length / 1024}KB)\n" +
            $"Output: Assets/References/_temp/base64_output.txt\n" +
            $"Read that file to get base64 data for Vision analysis.\n" +
            $"VISION_PROMPT: Analyze this game screenshot in extreme detail. " +
            $"For every visible element extract: exact shape, all hex colors, gradient direction+colors, " +
            $"outline thickness as % of object size, shine position+opacity, shadow, face details if any, " +
            $"surface patterns, UI panel styles, button styles, background colors. " +
            $"Return as JSON matching object_visual_spec.json structure.");
    }

    // ─────────────────────────────────────────────
    // TOOL — Save downloaded reference image
    // Write to Assets/References/_temp/download.txt:
    //   FILENAME:image.png
    //   BASE64:[data]
    // MCP: execute_menu_item("GameDevTools/Save Downloaded Reference")
    // ─────────────────────────────────────────────
    [MenuItem("GameDevTools/Save Downloaded Reference")]
    public static void SaveDownloadedReference()
    {
        EnsureDirectories();
        var downloadFile = Path.Combine(TempPath, "download.txt");

        if (!File.Exists(downloadFile))
        {
            Debug.LogWarning(
                "[GameDevTools] DOWNLOAD_FILE_MISSING\n" +
                "Write to Assets/References/_temp/download.txt:\n" +
                "FILENAME:image.png\n" +
                "BASE64:[base64 data]");
            return;
        }

        var content = File.ReadAllText(downloadFile);
        string filename = null, base64Data = null;

        foreach (var line in content.Split('\n'))
        {
            if (line.StartsWith("FILENAME:")) filename = line.Substring(9).Trim();
            else if (line.StartsWith("BASE64:")) base64Data = line.Substring(7).Trim();
        }

        if (string.IsNullOrEmpty(filename) || string.IsNullOrEmpty(base64Data))
        {
            Debug.LogError("[GameDevTools] DOWNLOAD_FORMAT_INVALID — need FILENAME: and BASE64: lines");
            return;
        }

        var outputPath = Path.Combine(ReferencesPath, filename);
        var imageBytes = Convert.FromBase64String(base64Data);
        File.WriteAllBytes(outputPath, imageBytes);
        AssetDatabase.Refresh();

        Debug.Log(
            $"[GameDevTools] REFERENCE_SAVED\n" +
            $"Saved: Assets/References/{filename}\n" +
            $"Size: {imageBytes.Length / 1024}KB\n" +
            $"Ready for Vision analysis.");
    }

    // ─────────────────────────────────────────────
    // TOOL — Capture screenshot (works in Edit mode too)
    // Saves to Assets/References/Screenshots/
    // MCP: execute_menu_item("GameDevTools/Capture Screenshot")
    // ─────────────────────────────────────────────
    [MenuItem("GameDevTools/Capture Screenshot")]
    public static void CaptureScreenshot()
    {
        EnsureDirectories();
        var screenshotsDir = Path.Combine(ReferencesPath, "Screenshots");
        if (!Directory.Exists(screenshotsDir)) Directory.CreateDirectory(screenshotsDir);

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var filename = $"screenshot_{timestamp}.png";
        var fullPath = Path.Combine(screenshotsDir, filename);
        var assetPath = $"Assets/References/Screenshots/{filename}";

        if (EditorApplication.isPlaying)
        {
            // In Play mode: use ScreenCapture
            ScreenCapture.CaptureScreenshot(fullPath);
            Debug.Log(
                $"[GameDevTools] SCREENSHOT_SAVED\n" +
                $"Mode: Play Mode\n" +
                $"Path: {assetPath}\n" +
                $"Note: File will appear after Play mode exits (Unity flushes on exit)");
        }
        else
        {
            // In Edit mode: use Scene view camera
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
            {
                Debug.LogWarning(
                    "[GameDevTools] SCREENSHOT_FAILED — no active Scene view.\n" +
                    "Open the Scene view window, or start Play mode first.");
                return;
            }

            var cam = sceneView.camera;
            int w = (int)sceneView.position.width;
            int h = (int)sceneView.position.height;

            var rt = new RenderTexture(w, h, 24);
            var prevActive = RenderTexture.active;
            var prevTarget = cam.targetTexture;

            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;

            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();

            RenderTexture.active = prevActive;
            cam.targetTexture = prevTarget;
            rt.Release();

            File.WriteAllBytes(fullPath, tex.EncodeToPNG());
            AssetDatabase.Refresh();

            Debug.Log(
                $"[GameDevTools] SCREENSHOT_SAVED\n" +
                $"Mode: Edit Mode (Scene View)\n" +
                $"Path: {assetPath}\n" +
                $"Size: {w}x{h}px");
        }

        // Write path to temp file for easy reference
        File.WriteAllText(Path.Combine(TempPath, "last_screenshot.txt"), fullPath);
    }

    // ─────────────────────────────────────────────
    // TOOL — Compare generated screenshot with reference
    // Write to Assets/References/_temp/compare_request.txt:
    //   REFERENCE:reference_filename.png
    //   SCREENSHOT:Screenshots/screenshot_timestamp.png
    //   OBJECT_TYPE:full_scene
    // MCP: execute_menu_item("GameDevTools/Compare With Reference")
    // ─────────────────────────────────────────────
    [MenuItem("GameDevTools/Compare With Reference")]
    public static void CompareWithReference()
    {
        EnsureDirectories();
        var requestFile = Path.Combine(TempPath, "compare_request.txt");

        if (!File.Exists(requestFile))
        {
            Debug.LogWarning(
                "[GameDevTools] COMPARE_REQUEST_MISSING\n" +
                "Write to Assets/References/_temp/compare_request.txt:\n" +
                "REFERENCE:reference_gameplay.png\n" +
                "SCREENSHOT:Screenshots/screenshot_timestamp.png\n" +
                "OBJECT_TYPE:full_scene");
            return;
        }

        var content = File.ReadAllText(requestFile);
        string refName = null, shotName = null, objectType = "full_scene";

        foreach (var line in content.Split('\n'))
        {
            if (line.StartsWith("REFERENCE:")) refName = line.Substring(10).Trim();
            else if (line.StartsWith("SCREENSHOT:")) shotName = line.Substring(11).Trim();
            else if (line.StartsWith("OBJECT_TYPE:")) objectType = line.Substring(12).Trim();
        }

        var refPath = Path.Combine(ReferencesPath, refName ?? "");
        var shotPath = Path.Combine(ReferencesPath, shotName ?? "");

        if (!File.Exists(refPath))
        {
            Debug.LogError($"[GameDevTools] COMPARE_FAILED — reference not found: {refName}");
            return;
        }
        if (!File.Exists(shotPath))
        {
            Debug.LogError($"[GameDevTools] COMPARE_FAILED — screenshot not found: {shotName}");
            return;
        }

        var refBytes = File.ReadAllBytes(refPath);
        var shotBytes = File.ReadAllBytes(shotPath);

        var refTex = new Texture2D(2, 2); refTex.LoadImage(refBytes);
        var shotTex = new Texture2D(2, 2); shotTex.LoadImage(shotBytes);

        float colorScore = CompareColorHistograms(refTex, shotTex);
        float brightnessScore = CompareBrightnessDistribution(refTex, shotTex);
        float edgeScore = CompareEdgeDensity(refTex, shotTex);
        float localScore = (colorScore + brightnessScore + edgeScore) / 3f;

        var refBase64 = Convert.ToBase64String(refBytes);
        var shotBase64 = Convert.ToBase64String(shotBytes);

        var outputFile = Path.Combine(TempPath, "comparison_output.txt");
        File.WriteAllText(outputFile,
            $"OBJECT_TYPE:{objectType}\n" +
            $"LOCAL_SCORE:{localScore:F1}\n" +
            $"COLOR_SCORE:{colorScore:F1}\n" +
            $"BRIGHTNESS_SCORE:{brightnessScore:F1}\n" +
            $"EDGE_SCORE:{edgeScore:F1}\n" +
            $"REFERENCE_BASE64:{refBase64}\n" +
            $"SCREENSHOT_BASE64:{shotBase64}\n" +
            $"VISION_PROMPT:Compare these two game screenshots in extreme detail. " +
            $"Score each aspect 0-100: " +
            $"1) Color accuracy (do the colors match the reference?) " +
            $"2) Scene layout (are elements in the same positions?) " +
            $"3) Visual quality (does it look professional vs placeholder?) " +
            $"4) Art style match (same style as reference?) " +
            $"5) Background quality (gradient? detailed? or flat color?) " +
            $"6) Character/object quality (detailed sprites or blobs?) " +
            $"7) UI quality (readable? styled? or default gray?) " +
            $"Give OVERALL score 0-100. " +
            $"List TOP 3 FIXES needed with exact instructions. " +
            $"Return as JSON: {{color:N, layout:N, quality:N, style:N, background:N, objects:N, ui:N, overall:N, fixes:[]}}");

        var passFail = localScore >= 70f ? "PASS" : "FAIL";
        Debug.Log(
            $"[GameDevTools] COMPARISON_COMPLETE — {passFail}\n" +
            $"Object: {objectType}\n" +
            $"Reference: {refName}\n" +
            $"Screenshot: {shotName}\n" +
            $"Local pixel scores:\n" +
            $"  Color histogram: {colorScore:F1}%\n" +
            $"  Brightness dist: {brightnessScore:F1}%\n" +
            $"  Edge density:    {edgeScore:F1}%\n" +
            $"  Local overall:   {localScore:F1}%\n" +
            $"Full comparison data: Assets/References/_temp/comparison_output.txt\n" +
            $"Read that file and send both images to Vision for detailed analysis.\n" +
            $"{(localScore < 70f ? "⚠️ Score below 70 — VisualAgent must fix and retry." : "✅ Acceptable quality.")}");
    }

    // ─────────────────────────────────────────────
    // TOOL — Check visual quality (gray/missing sprites)
    // MCP: execute_menu_item("GameDevTools/Check Visual Quality")
    // ─────────────────────────────────────────────
    [MenuItem("GameDevTools/Check Visual Quality")]
    public static void CheckVisualQuality()
    {
        var issues = new List<string>();

        var spriteRenderers = Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
        foreach (var r in spriteRenderers)
        {
            var path = GetPath(r.gameObject);
            if (r.sprite == null) { issues.Add($"[NO SPRITE]  {path}"); continue; }
            Color.RGBToHSV(r.color, out _, out float sat, out float val);
            if (sat < 0.12f && val > 0.25f)
                issues.Add($"[GRAY]       {path}  sat={sat:F2}");
            else if (r.color == Color.white && r.sprite.name.ToLower().Contains("default"))
                issues.Add($"[DEFAULT]    {path}");
        }

        var images = Object.FindObjectsByType<Image>(FindObjectsSortMode.None);
        foreach (var img in images)
        {
            Color.RGBToHSV(img.color, out _, out float sat, out float val);
            if (img.sprite == null && sat < 0.10f && val > 0.20f)
                issues.Add($"[UI-GRAY]    {GetPath(img.gameObject)}");
        }

        if (issues.Count == 0)
            Debug.Log(
                $"[GameDevTools] VISUAL_QUALITY_PASS\n" +
                $"Checked: {spriteRenderers.Length} sprites, {images.Length} UI images\n" +
                $"No gray or placeholder objects found.");
        else
            Debug.LogWarning(
                $"[GameDevTools] VISUAL_QUALITY_FAIL — {issues.Count} issue(s):\n" +
                string.Join("\n", issues) + "\n" +
                "Fix all before proceeding.");
    }

    // ─────────────────────────────────────────────
    // TOOL — Full QA Report
    // MCP: execute_menu_item("GameDevTools/Run Full QA Report")
    // ─────────────────────────────────────────────
    [MenuItem("GameDevTools/Run Full QA Report")]
    public static void RunFullQAReport()
    {
        Debug.Log("[GameDevTools] ═══ FULL QA REPORT START ═══");
        CheckInputSystemSettings();
        CheckEventSystem();
        CheckTMPUsage();
        CheckVisualQuality();
        ListReferenceImages();
        CheckButtonSizes();
        CheckBackgroundQuality();
        Debug.Log("[GameDevTools] ═══ FULL QA REPORT END ═══");
    }

    // ─────────────────────────────────────────────
    // TOOL — Check button sizes (U15)
    // MCP: execute_menu_item("GameDevTools/Check Button Sizes")
    // ─────────────────────────────────────────────
    [MenuItem("GameDevTools/Check Button Sizes")]
    public static void CheckButtonSizes()
    {
        var issues = new List<string>();
        var buttons = Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None);

        foreach (var btn in buttons)
        {
            var rt = btn.GetComponent<RectTransform>();
            if (rt == null) continue;

            var rect = rt.rect;
            var path = GetPath(btn.gameObject);

            if (rect.width < 60f || rect.height < 40f)
                issues.Add($"[TOO SMALL]  {path}  size={rect.width:F0}x{rect.height:F0}px (minimum 60x40)");

            // Check for text readability
            var tmp = btn.GetComponentInChildren<TMPro.TMP_Text>();
            if (tmp != null && tmp.fontSize < 14f)
                issues.Add($"[TINY TEXT]  {path}  fontSize={tmp.fontSize} (minimum 14)");

            // Check LayoutElement exists if inside a LayoutGroup
            var parentLayout = btn.transform.parent?.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>()
                            ?? btn.transform.parent?.GetComponent<UnityEngine.UI.VerticalLayoutGroup>() as UnityEngine.UI.LayoutGroup;
            if (parentLayout != null && btn.GetComponent<UnityEngine.UI.LayoutElement>() == null)
                issues.Add($"[NO LAYOUT_ELEMENT]  {path}  inside layout group but missing LayoutElement component");
        }

        if (issues.Count == 0)
            Debug.Log($"[GameDevTools] ✅ Button sizes OK — {buttons.Length} buttons checked");
        else
            Debug.LogWarning($"[GameDevTools] ⚠️ Button size issues ({issues.Count}):\n{string.Join("\n", issues)}");
    }

    // ─────────────────────────────────────────────
    // TOOL — Check background quality (U17)
    // MCP: execute_menu_item("GameDevTools/Check Background Quality")
    // ─────────────────────────────────────────────
    [MenuItem("GameDevTools/Check Background Quality")]
    public static void CheckBackgroundQuality()
    {
        var issues = new List<string>();

        // Check camera background color
        var cam = Camera.main;
        if (cam != null)
        {
            Color.RGBToHSV(cam.backgroundColor, out _, out float sat, out float val);
            if (sat < 0.05f)
                issues.Add($"[FLAT CAMERA BG]  Camera.backgroundColor is near-gray/black. Use a SpriteRenderer background instead.");
        }

        // Check for gradient background sprite
        var bgSprites = Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
        bool hasBackground = false;
        bool hasGradient = false;

        foreach (var sr in bgSprites)
        {
            if (sr.sortingOrder <= -90)
            {
                hasBackground = true;
                // Check if it uses a gradient texture (width > 1 and height > 1)
                if (sr.sprite != null && sr.sprite.texture.height > 4)
                    hasGradient = true;
            }
        }

        if (!hasBackground)
            issues.Add("[NO BACKGROUND]  No SpriteRenderer found at sortingOrder <= -90. Add a background sprite.");
        else if (!hasGradient)
            issues.Add("[FLAT BACKGROUND]  Background sprite found but may be single color. Use CreateVerticalGradient().");

        if (issues.Count == 0)
            Debug.Log("[GameDevTools] ✅ Background quality OK — gradient background detected");
        else
            Debug.LogWarning($"[GameDevTools] ⚠️ Background issues:\n{string.Join("\n", issues)}\n" +
                           "Per U17: background must always use gradient. Flat backgrounds look like placeholders.");
    }

    // ─────────────────────────────────────────────
    // TOOL — Validate game database
    // MCP: execute_menu_item("GameDevTools/Validate Game Database")
    // ─────────────────────────────────────────────
    [MenuItem("GameDevTools/Validate Game Database")]
    public static void ValidateGameDatabase()
    {
        // Try FruitDatabase first, then generic search
        var fruitsDb = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Data/FruitDatabase.asset");
        if (fruitsDb != null)
        {
            ValidateScriptableObject(fruitsDb, "FruitDatabase", "fruits", 11);
            return;
        }

        // Search for any ScriptableObject in Data folder
        var guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets/Data" });
        if (guids.Length == 0)
        {
            Debug.LogWarning("[GameDevTools] No ScriptableObjects found in Assets/Data/");
            return;
        }

        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            Debug.Log($"[GameDevTools] Found: {path} ({asset.GetType().Name})");
        }
    }

    // ─────────────────────────────────────────────
    // Helper checks
    // ─────────────────────────────────────────────
    private static void CheckEventSystem()
    {
        var es = Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
        if (es == null) { Debug.LogError("[GameDevTools] ❌ EventSystem NOT FOUND!"); return; }

        bool hasNew = es.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>() != null;
        bool hasOld = es.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>() != null;

        if (!hasNew && !hasOld)
            Debug.LogError("[GameDevTools] ❌ EventSystem has no input module!");
        else
            Debug.Log($"[GameDevTools] ✅ EventSystem OK — InputSystem:{hasNew} Standalone:{hasOld}");
    }

    private static void CheckTMPUsage()
    {
        var legacyTexts = Object.FindObjectsByType<UnityEngine.UI.Text>(FindObjectsSortMode.None);
        if (legacyTexts.Length > 0)
        {
            var names = new List<string>();
            foreach (var t in legacyTexts) names.Add(GetPath(t.gameObject));
            Debug.LogError($"[GameDevTools] ❌ Legacy Text ({legacyTexts.Length}):\n{string.Join("\n", names)}");
        }
        else
            Debug.Log("[GameDevTools] ✅ No legacy Text — TMP only");
    }

    private static void CheckInputSystemSettings()
    {
        const string path = "ProjectSettings/ProjectSettings.asset";
        if (!File.Exists(path)) { Debug.LogWarning("[GameDevTools] ProjectSettings not found"); return; }

        var content = File.ReadAllText(path);
        if (content.Contains("activeInputHandler: 1"))
            Debug.LogError("[GameDevTools] ❌ activeInputHandler = 1 — WILL CRASH with StandaloneInputModule!");
        else if (content.Contains("activeInputHandler: 2"))
            Debug.Log("[GameDevTools] ✅ activeInputHandler = 2 (Both)");
        else
            Debug.Log("[GameDevTools] ℹ️ activeInputHandler = 0 (Legacy only)");
    }

    private static void ValidateScriptableObject(ScriptableObject asset, string name, string arrayProp, int expectedCount)
    {
        var so = new SerializedObject(asset);
        var arr = so.FindProperty(arrayProp);
        if (arr == null) { Debug.LogWarning($"[GameDevTools] {name}: no '{arrayProp}' array found"); return; }

        var issues = new List<string>();
        if (arr.arraySize != expectedCount)
            issues.Add($"Expected {expectedCount} entries, found {arr.arraySize}");
        for (int i = 0; i < arr.arraySize; i++)
            if (arr.GetArrayElementAtIndex(i).objectReferenceValue == null)
                issues.Add($"Entry {i + 1}: NULL");

        if (issues.Count == 0)
            Debug.Log($"[GameDevTools] ✅ {name} valid — {arr.arraySize} entries");
        else
            Debug.LogWarning($"[GameDevTools] ⚠️ {name} issues:\n{string.Join("\n", issues)}");
    }

    private static void EnsureDirectories()
    {
        if (!Directory.Exists(ReferencesPath)) Directory.CreateDirectory(ReferencesPath);
        if (!Directory.Exists(TempPath)) Directory.CreateDirectory(TempPath);
        var screenshotsDir = Path.Combine(ReferencesPath, "Screenshots");
        if (!Directory.Exists(screenshotsDir)) Directory.CreateDirectory(screenshotsDir);
        AssetDatabase.Refresh();
    }

    private static string GetAvailableImages()
    {
        if (!Directory.Exists(ReferencesPath)) return "none (folder missing)";
        var files = new List<string>();
        foreach (var ext in new[] { "*.png", "*.jpg", "*.jpeg" })
            files.AddRange(Directory.GetFiles(ReferencesPath, ext));
        return files.Count == 0 ? "none" : string.Join(", ", files.ConvertAll(Path.GetFileName));
    }

    private static string GetPath(GameObject go)
    {
        var path = go.name;
        var p = go.transform.parent;
        while (p != null) { path = p.name + "/" + path; p = p.parent; }
        return path;
    }

    // ─────────────────────────────────────────────
    // Image comparison math
    // ─────────────────────────────────────────────
    private static float CompareColorHistograms(Texture2D a, Texture2D b)
    {
        int step = Mathf.Max(1, Mathf.Max(a.width * a.height, b.width * b.height) / 10000);
        var hA = new int[16, 16, 16]; var hB = new int[16, 16, 16];
        int cA = 0, cB = 0;

        var pA = a.GetPixels32(); var pB = b.GetPixels32();
        for (int i = 0; i < pA.Length; i += step)
            if (pA[i].a > 10) { hA[pA[i].r / 16, pA[i].g / 16, pA[i].b / 16]++; cA++; }
        for (int i = 0; i < pB.Length; i += step)
            if (pB[i].a > 10) { hB[pB[i].r / 16, pB[i].g / 16, pB[i].b / 16]++; cB++; }

        if (cA == 0 || cB == 0) return 0f;
        float inter = 0f;
        for (int r = 0; r < 16; r++)
            for (int g = 0; g < 16; g++)
                for (int bl = 0; bl < 16; bl++)
                    inter += Mathf.Min((float)hA[r, g, bl] / cA, (float)hB[r, g, bl] / cB);
        return inter * 100f;
    }

    private static float CompareEdgeDensity(Texture2D a, Texture2D b)
    {
        float eA = CalcEdgeDensity(a), eB = CalcEdgeDensity(b);
        if (eA == 0 && eB == 0) return 100f;
        return Mathf.Clamp01(1f - Mathf.Abs(eA - eB) / Mathf.Max(eA, eB)) * 100f;
    }

    private static float CalcEdgeDensity(Texture2D tex)
    {
        var px = tex.GetPixels32(); int w = tex.width, h = tex.height;
        int edges = 0, total = 0;
        for (int y = 1; y < h - 1; y++)
            for (int x = 1; x < w - 1; x++)
            {
                var c = px[y * w + x]; if (c.a < 10) continue; total++;
                var r = px[y * w + x + 1]; var u = px[(y + 1) * w + x];
                if (Mathf.Abs(c.r - r.r) + Mathf.Abs(c.g - r.g) + Mathf.Abs(c.b - r.b) > 60 ||
                    Mathf.Abs(c.r - u.r) + Mathf.Abs(c.g - u.g) + Mathf.Abs(c.b - u.b) > 60)
                    edges++;
            }
        return total == 0 ? 0f : (float)edges / total;
    }

    private static float CompareBrightnessDistribution(Texture2D a, Texture2D b)
    {
        var bA = new float[8]; var bB = new float[8];
        int cA = 0, cB = 0;
        foreach (var p in a.GetPixels32())
            if (p.a > 10) { bA[Mathf.Min((p.r + p.g + p.b) / 3 / 32, 7)]++; cA++; }
        foreach (var p in b.GetPixels32())
            if (p.a > 10) { bB[Mathf.Min((p.r + p.g + p.b) / 3 / 32, 7)]++; cB++; }
        if (cA == 0 || cB == 0) return 0f;
        float sim = 0f;
        for (int i = 0; i < 8; i++) sim += Mathf.Min(bA[i] / cA, bB[i] / cB);
        return sim * 100f;
    }
}
