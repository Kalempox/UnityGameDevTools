using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using System.IO;
using Object = UnityEngine.Object;

/// <summary>
/// Custom game dev tools accessible via Unity menu items.
/// Call via MCP: execute_menu_item("GameDevTools/...")
/// Results appear in Unity Console — read with read_console_logs.
/// </summary>
public static class ReferenceAnalyzerTool
{
    // ─────────────────────────────────────────────
    // TOOL 1 — List reference images
    // MCP: execute_menu_item("GameDevTools/List Reference Images")
    // ─────────────────────────────────────────────
    [MenuItem("GameDevTools/List Reference Images")]
    public static void ListReferenceImages()
    {
        var projectRoot = Path.GetDirectoryName(Application.dataPath);
        var refDir = Path.Combine(projectRoot, "references");

        if (!Directory.Exists(refDir))
        {
            Debug.LogWarning($"[GameDevTools] references/ folder does not exist.\nCreate it at: {refDir}");
            return;
        }

        var files = new List<string>();
        foreach (var ext in new[] { "*.png", "*.jpg", "*.jpeg" })
            files.AddRange(Directory.GetFiles(refDir, ext));

        if (files.Count == 0)
        {
            Debug.LogWarning("[GameDevTools] references/ folder is empty. Add PNG or JPG reference images.");
            return;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[GameDevTools] Found {files.Count} reference image(s):");
        foreach (var f in files)
        {
            var info = new FileInfo(f);
            sb.AppendLine($"  - {info.Name}  ({info.Length / 1024}KB)");
        }
        Debug.Log(sb.ToString());
    }

    // ─────────────────────────────────────────────
    // TOOL 2 — Read reference image as base64
    // MCP: execute_menu_item("GameDevTools/Read Reference As Base64")
    // Then pass the image name via a marker in the log
    // Usage: Agent reads console, finds REFERENCE_BASE64 marker
    // ─────────────────────────────────────────────
    [MenuItem("GameDevTools/Read Reference As Base64")]
    public static void ReadReferenceAsBase64()
    {
        // Agent must write the filename to a temp file before calling this
        // File: ProjectRoot/references/_request.txt
        var projectRoot = Path.GetDirectoryName(Application.dataPath);
        var requestFile = Path.Combine(projectRoot, "references", "_request.txt");

        if (!File.Exists(requestFile))
        {
            Debug.LogWarning("[GameDevTools] No request file found.\n" +
                           "Write the image filename to references/_request.txt first.\n" +
                           "Example content: suika_screenshot.png");
            return;
        }

        var imageName = File.ReadAllText(requestFile).Trim();
        var imagePath = Path.Combine(projectRoot, "references", imageName);

        if (!File.Exists(imagePath))
        {
            Debug.LogError($"[GameDevTools] Image not found: {imageName}\n" +
                          $"Available images in references/: {GetAvailableReferenceImages(projectRoot)}");
            return;
        }

        var bytes = File.ReadAllBytes(imagePath);
        var base64 = Convert.ToBase64String(bytes);
        var ext = Path.GetExtension(imageName).ToLower();
        var mimeType = (ext == ".jpg" || ext == ".jpeg") ? "image/jpeg" : "image/png";

        // Write to output file — base64 is too long for console
        var outputPath = Path.Combine(projectRoot, "references", "_base64_output.txt");
        File.WriteAllText(outputPath, $"MIME:{mimeType}\nFILE:{imageName}\nSIZE:{bytes.Length / 1024}KB\nBASE64:{base64}");

        Debug.Log($"[GameDevTools] REFERENCE_BASE64_READY\n" +
                 $"File: {imageName}\n" +
                 $"Size: {bytes.Length / 1024}KB\n" +
                 $"Output written to: references/_base64_output.txt\n" +
                 $"Read that file to get the base64 data for Vision analysis.\n" +
                 $"Analysis instruction: Analyze this game screenshot in extreme detail. " +
                 $"For every visible game object (fruits, UI panels, buttons, background), extract: " +
                 $"exact shape, all colors as hex codes, gradient direction and colors, " +
                 $"outline thickness as percentage of object size, shine/highlight position and opacity, " +
                 $"shadow properties, face/expression details if present, " +
                 $"surface texture pattern, edge softness. " +
                 $"Return as JSON matching object_visual_spec.json format.");
    }

    // ─────────────────────────────────────────────
    // TOOL 3 — Save downloaded reference image
    // Agent downloads image bytes, writes to references/_download.txt as base64
    // Then calls this tool to decode and save as PNG
    // MCP: execute_menu_item("GameDevTools/Save Downloaded Reference")
    // ─────────────────────────────────────────────
    [MenuItem("GameDevTools/Save Downloaded Reference")]
    public static void SaveDownloadedReference()
    {
        var projectRoot = Path.GetDirectoryName(Application.dataPath);
        var downloadFile = Path.Combine(projectRoot, "references", "_download.txt");

        if (!File.Exists(downloadFile))
        {
            Debug.LogWarning("[GameDevTools] No download file found.\n" +
                           "Write to references/_download.txt with format:\n" +
                           "FILENAME:suika_screenshot.png\n" +
                           "BASE64:[base64 data]");
            return;
        }

        var content = File.ReadAllText(downloadFile);
        var lines = content.Split('\n');

        string filename = null;
        string base64Data = null;

        foreach (var line in lines)
        {
            if (line.StartsWith("FILENAME:"))
                filename = line.Substring("FILENAME:".Length).Trim();
            else if (line.StartsWith("BASE64:"))
                base64Data = line.Substring("BASE64:".Length).Trim();
        }

        if (string.IsNullOrEmpty(filename) || string.IsNullOrEmpty(base64Data))
        {
            Debug.LogError("[GameDevTools] Invalid download file format.\n" +
                          "Expected:\nFILENAME:image.png\nBASE64:[data]");
            return;
        }

        var refDir = Path.Combine(projectRoot, "references");
        if (!Directory.Exists(refDir)) Directory.CreateDirectory(refDir);

        var outputPath = Path.Combine(refDir, filename);
        var bytes = Convert.FromBase64String(base64Data);
        File.WriteAllBytes(outputPath, bytes);

        Debug.Log($"[GameDevTools] REFERENCE_SAVED\n" +
                 $"Saved: references/{filename}\n" +
                 $"Size: {bytes.Length / 1024}KB\n" +
                 $"Ready for analysis with Read Reference As Base64.");
    }

    // ─────────────────────────────────────────────
    // TOOL 4 — Compare generated sprite with reference
    // Agent writes comparison request to references/_compare_request.txt:
    //   REFERENCE:suika_cherry.png
    //   GENERATED:Assets/Art/Fruits/Cherry_generated.png
    // MCP: execute_menu_item("GameDevTools/Compare With Reference")
    // ─────────────────────────────────────────────
    [MenuItem("GameDevTools/Compare With Reference")]
    public static void CompareWithReference()
    {
        var projectRoot = Path.GetDirectoryName(Application.dataPath);
        var requestFile = Path.Combine(projectRoot, "references", "_compare_request.txt");

        if (!File.Exists(requestFile))
        {
            Debug.LogWarning("[GameDevTools] No compare request file found.\n" +
                           "Write to references/_compare_request.txt:\n" +
                           "REFERENCE:reference_image.png\n" +
                           "GENERATED:Assets/path/to/generated_sprite.png\n" +
                           "OBJECT_TYPE:cherry");
            return;
        }

        var content = File.ReadAllText(requestFile);
        var lines = content.Split('\n');

        string referenceName = null, generatedPath = null, objectType = "unknown";
        foreach (var line in lines)
        {
            if (line.StartsWith("REFERENCE:")) referenceName = line.Substring("REFERENCE:".Length).Trim();
            else if (line.StartsWith("GENERATED:")) generatedPath = line.Substring("GENERATED:".Length).Trim();
            else if (line.StartsWith("OBJECT_TYPE:")) objectType = line.Substring("OBJECT_TYPE:".Length).Trim();
        }

        if (string.IsNullOrEmpty(referenceName) || string.IsNullOrEmpty(generatedPath))
        {
            Debug.LogError("[GameDevTools] Compare request file is missing REFERENCE or GENERATED fields.");
            return;
        }

        var referencePath = Path.Combine(projectRoot, "references", referenceName);
        var fullGeneratedPath = Path.Combine(Application.dataPath, "..", generatedPath);

        if (!File.Exists(referencePath))
        {
            Debug.LogError($"[GameDevTools] Reference image not found: {referenceName}");
            return;
        }
        if (!File.Exists(fullGeneratedPath))
        {
            Debug.LogError($"[GameDevTools] Generated sprite not found: {generatedPath}");
            return;
        }

        // Load both images
        var refBytes = File.ReadAllBytes(referencePath);
        var refTex = new Texture2D(2, 2);
        refTex.LoadImage(refBytes);

        var genBytes = File.ReadAllBytes(fullGeneratedPath);
        var genTex = new Texture2D(2, 2);
        genTex.LoadImage(genBytes);

        // Color histogram comparison
        float colorScore = CompareColorHistograms(refTex, genTex);

        // Edge density comparison (shape similarity proxy)
        float shapeScore = CompareEdgeDensity(refTex, genTex);

        // Brightness distribution comparison
        float brightnessScore = CompareBrightnessDistribution(refTex, genTex);

        // Write both images as base64 for Vision comparison
        var refBase64 = Convert.ToBase64String(refBytes);
        var genBase64 = Convert.ToBase64String(genBytes);

        var outputPath = Path.Combine(projectRoot, "references", "_comparison_output.txt");
        File.WriteAllText(outputPath,
            $"REFERENCE_BASE64:{refBase64}\n" +
            $"GENERATED_BASE64:{genBase64}\n" +
            $"OBJECT_TYPE:{objectType}\n" +
            $"VISION_INSTRUCTION:Compare these two game sprite images in detail. " +
            $"Score similarity 0-100 for each: " +
            $"1) Color accuracy (are the colors matching?) " +
            $"2) Shape accuracy (is the outline/silhouette similar?) " +
            $"3) Gradient quality (does the shading look similar?) " +
            $"4) Outline thickness (is the border thickness similar?) " +
            $"5) Highlight/shine position (is the shine in the same place?) " +
            $"6) Face accuracy if present (do the eyes/expression match?) " +
            $"Then give an OVERALL score 0-100. " +
            $"For any score below 80, explain exactly what is different and how to fix it. " +
            $"Return as JSON: {{color:N, shape:N, gradient:N, outline:N, highlight:N, face:N, overall:N, fixes:[]}}");

        float overallLocal = (colorScore + shapeScore + brightnessScore) / 3f;

        Debug.Log($"[GameDevTools] COMPARISON_READY\n" +
                 $"Object: {objectType}\n" +
                 $"Reference: {referenceName}\n" +
                 $"Generated: {generatedPath}\n" +
                 $"Local scores (pixel-based):\n" +
                 $"  Color histogram similarity: {colorScore:F1}%\n" +
                 $"  Shape/edge similarity:       {shapeScore:F1}%\n" +
                 $"  Brightness distribution:     {brightnessScore:F1}%\n" +
                 $"  Local overall estimate:      {overallLocal:F1}%\n" +
                 $"Both images written to references/_comparison_output.txt\n" +
                 $"Read that file and send to Vision API for detailed analysis.\n" +
                 $"{(overallLocal < 85f ? "⚠️ Score below 85 — VisualAgent should refine the sprite." : "✅ Score acceptable.")}");
    }

    // ─────────────────────────────────────────────
    // TOOL 5 — Check visual quality
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
                issues.Add($"[GRAY]       {path}  (saturation={sat:F2})");
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
            Debug.Log($"[GameDevTools] ✅ Visual quality PASSED\n" +
                     $"Checked: {spriteRenderers.Length} sprites, {images.Length} UI images");
        else
            Debug.LogWarning($"[GameDevTools] ⚠️ {issues.Count} visual issue(s):\n{string.Join("\n", issues)}");
    }

    // ─────────────────────────────────────────────
    // TOOL 6 — Capture screenshot
    // MCP: execute_menu_item("GameDevTools/Capture Screenshot")
    // ─────────────────────────────────────────────
    [MenuItem("GameDevTools/Capture Screenshot")]
    public static void CaptureScreenshot()
    {
        var projectRoot = Path.GetDirectoryName(Application.dataPath);
        var outputDir = Path.Combine(projectRoot, "QA", "screenshots");
        if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);

        var filename = $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png";
        var fullPath = Path.Combine(outputDir, filename);
        ScreenCapture.CaptureScreenshot(fullPath);

        Debug.Log($"[GameDevTools] SCREENSHOT_SAVED\n" +
                 $"Path: QA/screenshots/{filename}\n" +
                 $"Full path: {fullPath}");
    }

    // ─────────────────────────────────────────────
    // TOOL 7 — Full QA Report
    // MCP: execute_menu_item("GameDevTools/Run Full QA Report")
    // ─────────────────────────────────────────────
    [MenuItem("GameDevTools/Run Full QA Report")]
    public static void RunFullQAReport()
    {
        Debug.Log("[GameDevTools] ═══ FULL QA REPORT START ═══");
        CheckVisualQuality();
        CheckEventSystem();
        CheckTMPUsage();
        CheckInputSystemSettings();
        ListReferenceImages();
        Debug.Log("[GameDevTools] ═══ FULL QA REPORT END ═══");
    }

    // ─────────────────────────────────────────────
    // TOOL 8 — Validate Fruit Database
    // MCP: execute_menu_item("GameDevTools/Validate Fruit Database")
    // ─────────────────────────────────────────────
    [MenuItem("GameDevTools/Validate Fruit Database")]
    public static void ValidateFruitDatabase()
    {
        var db = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Data/FruitDatabase.asset");
        if (db == null)
        {
            Debug.LogError("[GameDevTools] FruitDatabase.asset not found at Assets/Data/FruitDatabase.asset");
            return;
        }

        var so = new SerializedObject(db);
        var fruitsArray = so.FindProperty("fruits");
        if (fruitsArray == null) { Debug.LogWarning("[GameDevTools] FruitDatabase has no 'fruits' array."); return; }

        var issues = new List<string>();
        if (fruitsArray.arraySize != 11) issues.Add($"Expected 11 tiers, found {fruitsArray.arraySize}");
        for (int i = 0; i < fruitsArray.arraySize; i++)
            if (fruitsArray.GetArrayElementAtIndex(i).objectReferenceValue == null)
                issues.Add($"Tier {i + 1}: NULL");

        if (issues.Count == 0)
            Debug.Log($"[GameDevTools] ✅ FruitDatabase valid — {fruitsArray.arraySize} tiers OK");
        else
            Debug.LogWarning($"[GameDevTools] ⚠️ FruitDatabase issues:\n{string.Join("\n", issues)}");
    }

    // ─────────────────────────────────────────────
    // Image comparison helpers
    // ─────────────────────────────────────────────
    private static float CompareColorHistograms(Texture2D a, Texture2D b)
    {
        var pixA = a.GetPixels32();
        var pixB = b.GetPixels32();

        // Sample up to 10000 pixels for performance
        int step = Mathf.Max(1, pixA.Length / 10000);
        var histA = new int[16, 16, 16];
        var histB = new int[16, 16, 16];

        int countA = 0, countB = 0;
        for (int i = 0; i < pixA.Length; i += step)
        {
            if (pixA[i].a < 10) continue; // skip transparent
            histA[pixA[i].r / 16, pixA[i].g / 16, pixA[i].b / 16]++;
            countA++;
        }
        for (int i = 0; i < pixB.Length; i += step)
        {
            if (pixB[i].a < 10) continue;
            histB[pixB[i].r / 16, pixB[i].g / 16, pixB[i].b / 16]++;
            countB++;
        }

        if (countA == 0 || countB == 0) return 0f;

        float intersection = 0f;
        for (int r = 0; r < 16; r++)
            for (int g = 0; g < 16; g++)
                for (int bl = 0; bl < 16; bl++)
                    intersection += Mathf.Min(
                        (float)histA[r, g, bl] / countA,
                        (float)histB[r, g, bl] / countB);

        return intersection * 100f;
    }

    private static float CompareEdgeDensity(Texture2D a, Texture2D b)
    {
        float edgeDensityA = CalculateEdgeDensity(a);
        float edgeDensityB = CalculateEdgeDensity(b);
        if (edgeDensityA == 0 && edgeDensityB == 0) return 100f;
        float maxDensity = Mathf.Max(edgeDensityA, edgeDensityB);
        float diff = Mathf.Abs(edgeDensityA - edgeDensityB) / maxDensity;
        return Mathf.Clamp01(1f - diff) * 100f;
    }

    private static float CalculateEdgeDensity(Texture2D tex)
    {
        var pixels = tex.GetPixels32();
        int w = tex.width, h = tex.height;
        int edgeCount = 0, total = 0;

        for (int y = 1; y < h - 1; y++)
        {
            for (int x = 1; x < w - 1; x++)
            {
                var c = pixels[y * w + x];
                if (c.a < 10) continue;
                total++;

                var right = pixels[y * w + (x + 1)];
                var up = pixels[(y + 1) * w + x];

                float diffR = Mathf.Abs(c.r - right.r) + Mathf.Abs(c.g - right.g) + Mathf.Abs(c.b - right.b);
                float diffU = Mathf.Abs(c.r - up.r) + Mathf.Abs(c.g - up.g) + Mathf.Abs(c.b - up.b);

                if (diffR > 60 || diffU > 60) edgeCount++;
            }
        }
        return total == 0 ? 0f : (float)edgeCount / total;
    }

    private static float CompareBrightnessDistribution(Texture2D a, Texture2D b)
    {
        float[] bucketA = new float[8];
        float[] bucketB = new float[8];
        int countA = 0, countB = 0;

        foreach (var p in a.GetPixels32())
        {
            if (p.a < 10) continue;
            int bucket = (p.r + p.g + p.b) / 3 / 32;
            bucketA[Mathf.Min(bucket, 7)]++;
            countA++;
        }
        foreach (var p in b.GetPixels32())
        {
            if (p.a < 10) continue;
            int bucket = (p.r + p.g + p.b) / 3 / 32;
            bucketB[Mathf.Min(bucket, 7)]++;
            countB++;
        }

        if (countA == 0 || countB == 0) return 0f;

        float similarity = 0f;
        for (int i = 0; i < 8; i++)
            similarity += Mathf.Min(bucketA[i] / countA, bucketB[i] / countB);

        return similarity * 100f;
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
            Debug.Log($"[GameDevTools] ✅ EventSystem OK — InputSystemModule:{hasNew} StandaloneModule:{hasOld}");
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
        var settingsPath = "ProjectSettings/ProjectSettings.asset";
        if (!File.Exists(settingsPath)) { Debug.LogWarning("[GameDevTools] ProjectSettings.asset not found"); return; }

        var content = File.ReadAllText(settingsPath);
        if (content.Contains("activeInputHandler: 1"))
            Debug.LogError("[GameDevTools] ❌ activeInputHandler = 1 — StandaloneInputModule will crash!");
        else if (content.Contains("activeInputHandler: 2"))
            Debug.Log("[GameDevTools] ✅ activeInputHandler = 2 (Both) — OK");
        else
            Debug.Log("[GameDevTools] ℹ️ activeInputHandler = 0 (Old Input)");
    }

    private static string GetAvailableReferenceImages(string projectRoot)
    {
        var refDir = Path.Combine(projectRoot, "references");
        if (!Directory.Exists(refDir)) return "none (folder missing)";
        var files = new List<string>();
        foreach (var ext in new[] { "*.png", "*.jpg", "*.jpeg" })
            files.AddRange(Directory.GetFileSystemEntries(refDir, ext));
        return files.Count == 0 ? "none" : string.Join(", ", files.ConvertAll(Path.GetFileName));
    }

    private static string GetPath(GameObject go)
    {
        var path = go.name;
        var parent = go.transform.parent;
        while (parent != null) { path = parent.name + "/" + path; parent = parent.parent; }
        return path;
    }
}
