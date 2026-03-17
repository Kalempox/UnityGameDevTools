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
            Debug.LogWarning("[GameDevTools] references/ folder is empty.");
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
    // TOOL 2 — Check visual quality
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
    // TOOL 3 — Capture screenshot
    // MCP: execute_menu_item("GameDevTools/Capture Screenshot")
    // ─────────────────────────────────────────────
    [MenuItem("GameDevTools/Capture Screenshot")]
    public static void CaptureScreenshot()
    {
        var projectRoot = Path.GetDirectoryName(Application.dataPath);
        var outputDir = Path.Combine(projectRoot, "QA", "screenshots");
        if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);

        var filename = $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png";
        ScreenCapture.CaptureScreenshot(Path.Combine(outputDir, filename));
        Debug.Log($"[GameDevTools] Screenshot saved: QA/screenshots/{filename}");
    }

    // ─────────────────────────────────────────────
    // TOOL 4 — Full QA Report
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
        Debug.Log("[GameDevTools] ═══ FULL QA REPORT END ═══");
    }

    // ─────────────────────────────────────────────
    // TOOL 5 — Validate Fruit Database
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
        if (fruitsArray == null)
        {
            Debug.LogWarning("[GameDevTools] FruitDatabase has no 'fruits' array.");
            return;
        }

        var issues = new List<string>();
        if (fruitsArray.arraySize != 11)
            issues.Add($"Expected 11 tiers, found {fruitsArray.arraySize}");

        for (int i = 0; i < fruitsArray.arraySize; i++)
        {
            if (fruitsArray.GetArrayElementAtIndex(i).objectReferenceValue == null)
                issues.Add($"Tier {i + 1}: NULL");
        }

        if (issues.Count == 0)
            Debug.Log($"[GameDevTools] ✅ FruitDatabase valid — {fruitsArray.arraySize} tiers OK");
        else
            Debug.LogWarning($"[GameDevTools] ⚠️ FruitDatabase issues:\n{string.Join("\n", issues)}");
    }

    // ─────────────────────────────────────────────
    // Helper checks
    // ─────────────────────────────────────────────
    private static void CheckEventSystem()
    {
        var es = Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
        if (es == null)
        {
            Debug.LogError("[GameDevTools] ❌ EventSystem NOT FOUND in scene!");
            return;
        }

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
            Debug.LogError($"[GameDevTools] ❌ Legacy Text found ({legacyTexts.Length}):\n{string.Join("\n", names)}");
        }
        else
            Debug.Log("[GameDevTools] ✅ No legacy Text — TMP only");
    }

    private static void CheckInputSystemSettings()
    {
        var settingsPath = "ProjectSettings/ProjectSettings.asset";
        if (!File.Exists(settingsPath))
        {
            Debug.LogWarning("[GameDevTools] Could not find ProjectSettings.asset");
            return;
        }

        var content = File.ReadAllText(settingsPath);
        if (content.Contains("activeInputHandler: 1"))
            Debug.LogError("[GameDevTools] ❌ activeInputHandler = 1 (New Only) — will cause StandaloneInputModule crash! Change to 2 (Both).");
        else if (content.Contains("activeInputHandler: 2"))
            Debug.Log("[GameDevTools] ✅ activeInputHandler = 2 (Both) — OK");
        else
            Debug.Log("[GameDevTools] ℹ️ activeInputHandler = 0 (Old) — consider switching to Both");
    }

    private static string GetPath(GameObject go)
    {
        var path = go.name;
        var parent = go.transform.parent;
        while (parent != null) { path = parent.name + "/" + path; parent = parent.parent; }
        return path;
    }
}
