using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Custom game dev tools accessible via Unity menu items.
/// Call these through MCP using: execute_menu_item("GameDevTools/...")
/// </summary>
public static class ReferenceAnalyzerTool
{
    // ─────────────────────────────────────────────
    // TOOL 1 — List reference images
    // MCP call: execute_menu_item("GameDevTools/List Reference Images")
    // ─────────────────────────────────────────────
    [MenuItem("GameDevTools/List Reference Images")]
    public static void ListReferenceImages()
    {
        var projectRoot = Path.GetDirectoryName(Application.dataPath);
        var refDir = Path.Combine(projectRoot, "references");

        if (!Directory.Exists(refDir))
        {
            Debug.LogWarning($"[GameDevTools] references/ folder does not exist.\n" +
                           $"Create it at: {refDir}\n" +
                           "Then add PNG/JPG reference images.");
            return;
        }

        var extensions = new[] { "*.png", "*.jpg", "*.jpeg" };
        var files = new List<string>();
        foreach (var ext in extensions)
            files.AddRange(Directory.GetFiles(refDir, ext));

        if (files.Count == 0)
        {
            Debug.LogWarning($"[GameDevTools] references/ folder is empty. Add PNG or JPG images.");
            return;
        }

        var result = $"[GameDevTools] Found {files.Count} reference image(s):\n";
        foreach (var f in files)
        {
            var info = new FileInfo(f);
            result += $"  - {info.Name}  ({info.Length / 1024}KB)\n";
        }
        Debug.Log(result);
    }

    // ─────────────────────────────────────────────
    // TOOL 2 — Check visual quality
    // MCP call: execute_menu_item("GameDevTools/Check Visual Quality")
    // ─────────────────────────────────────────────
    [MenuItem("GameDevTools/Check Visual Quality")]
    public static void CheckVisualQuality()
    {
        var issues = new List<string>();

        // SpriteRenderer check
        var spriteRenderers = GameObject.FindObjectsOfType<SpriteRenderer>(true);
        foreach (var r in spriteRenderers)
        {
            var path = GetPath(r.gameObject);

            if (r.sprite == null)
            {
                issues.Add($"[NO SPRITE]  {path}");
                continue;
            }

            Color.RGBToHSV(r.color, out _, out float sat, out float val);

            if (sat < 0.12f && val > 0.25f)
                issues.Add($"[GRAY]       {path}  (saturation={sat:F2})");
            else if (r.color == Color.white && r.sprite.name.ToLower().Contains("default"))
                issues.Add($"[DEFAULT]    {path}  (white + default sprite)");
        }

        // UI Image check
        var images = GameObject.FindObjectsOfType<Image>(true);
        foreach (var img in images)
        {
            Color.RGBToHSV(img.color, out _, out float sat, out float val);
            if (img.sprite == null && sat < 0.10f && val > 0.20f)
                issues.Add($"[UI-GRAY]    {GetPath(img.gameObject)}  (flat gray panel)");
        }

        if (issues.Count == 0)
        {
            Debug.Log($"[GameDevTools] ✅ Visual quality PASSED\n" +
                     $"Checked: {spriteRenderers.Length} sprites, {images.Length} UI images\n" +
                     "No gray or placeholder objects found.");
        }
        else
        {
            var msg = $"[GameDevTools] ⚠️ Visual quality FAILED — {issues.Count} issue(s):\n\n";
            msg += string.Join("\n", issues);
            msg += $"\n\nTotal checked: {spriteRenderers.Length + images.Length} objects";
            Debug.LogWarning(msg);
        }
    }

    // ─────────────────────────────────────────────
    // TOOL 3 — Capture screenshot
    // MCP call: execute_menu_item("GameDevTools/Capture Screenshot")
    // ─────────────────────────────────────────────
    [MenuItem("GameDevTools/Capture Screenshot")]
    public static void CaptureScreenshot()
    {
        var projectRoot = Path.GetDirectoryName(Application.dataPath);
        var outputDir = Path.Combine(projectRoot, "QA", "screenshots");

        if (!Directory.Exists(outputDir))
            Directory.CreateDirectory(outputDir);

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var filename = $"screenshot_{timestamp}.png";
        var outputPath = Path.Combine(outputDir, filename);

        ScreenCapture.CaptureScreenshot(outputPath);
        Debug.Log($"[GameDevTools] Screenshot saved: QA/screenshots/{filename}");
    }

    // ─────────────────────────────────────────────
    // TOOL 4 — Validate fruit database (game-specific)
    // MCP call: execute_menu_item("GameDevTools/Validate Fruit Database")
    // ─────────────────────────────────────────────
    [MenuItem("GameDevTools/Validate Fruit Database")]
    public static void ValidateFruitDatabase()
    {
        var db = AssetDatabase.LoadAssetAtPath<ScriptableObject>(
            "Assets/Data/FruitDatabase.asset");

        if (db == null)
        {
            Debug.LogError("[GameDevTools] FruitDatabase.asset not found at Assets/Data/FruitDatabase.asset");
            return;
        }

        // SerializedObject ile kontrol et
        var so = new SerializedObject(db);
        var fruitsArray = so.FindProperty("fruits");

        if (fruitsArray == null)
        {
            Debug.LogWarning("[GameDevTools] FruitDatabase has no 'fruits' array property.");
            return;
        }

        var issues = new List<string>();
        for (int i = 0; i < fruitsArray.arraySize; i++)
        {
            var elem = fruitsArray.GetArrayElementAtIndex(i);
            if (elem.objectReferenceValue == null)
                issues.Add($"Tier {i + 1}: NULL entry");
        }

        if (fruitsArray.arraySize != 11)
            issues.Add($"Expected 11 tiers, found {fruitsArray.arraySize}");

        if (issues.Count == 0)
            Debug.Log($"[GameDevTools] ✅ FruitDatabase valid — {fruitsArray.arraySize} tiers OK");
        else
            Debug.LogWarning($"[GameDevTools] ⚠️ FruitDatabase issues:\n{string.Join("\n", issues)}");
    }

    // ─────────────────────────────────────────────
    // TOOL 5 — Full QA report
    // MCP call: execute_menu_item("GameDevTools/Run Full QA Report")
    // ─────────────────────────────────────────────
    [MenuItem("GameDevTools/Run Full QA Report")]
    public static void RunFullQAReport()
    {
        Debug.Log("[GameDevTools] ═══ FULL QA REPORT ═══");
        CheckVisualQuality();
        CheckEventSystem();
        CheckTMPUsage();
        Debug.Log("[GameDevTools] ═══ QA REPORT COMPLETE ═══");
    }

    // ─────────────────────────────────────────────
    // Helper checks
    // ─────────────────────────────────────────────
    private static void CheckEventSystem()
    {
        var es = GameObject.FindObjectOfType<UnityEngine.EventSystems.EventSystem>();
        if (es == null)
        {
            Debug.LogError("[GameDevTools] ❌ EventSystem NOT FOUND in scene!");
            return;
        }

        bool hasNewInput = es.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>() != null;
        bool hasOldInput = es.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>() != null;

        if (!hasNewInput && !hasOldInput)
            Debug.LogError("[GameDevTools] ❌ EventSystem has no input module!");
        else
            Debug.Log($"[GameDevTools] ✅ EventSystem OK — NewInput:{hasNewInput} OldInput:{hasOldInput}");
    }

    private static void CheckTMPUsage()
    {
        var legacyTexts = GameObject.FindObjectsOfType<UnityEngine.UI.Text>(true);
        if (legacyTexts.Length > 0)
        {
            var names = new List<string>();
            foreach (var t in legacyTexts) names.Add(GetPath(t.gameObject));
            Debug.LogError($"[GameDevTools] ❌ Legacy Text found ({legacyTexts.Length}):\n{string.Join("\n", names)}");
        }
        else
        {
            Debug.Log("[GameDevTools] ✅ No legacy Text components — TMP only");
        }
    }

    private static string GetPath(GameObject go)
    {
        var path = go.name;
        var parent = go.transform.parent;
        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }
        return path;
    }
}
