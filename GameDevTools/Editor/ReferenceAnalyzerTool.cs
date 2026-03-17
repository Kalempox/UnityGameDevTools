using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.ComponentModel;

[McpPluginToolType]
public class ReferenceAnalyzerTool
{
    // ─────────────────────────────────────────────
    // TOOL 1 — Referans görseli oku ve analiz için hazırla
    // ─────────────────────────────────────────────
    [McpPluginTool("analyze_reference_image")]
    [Description(
        "Reads an image from the references/ folder at project root. " +
        "Returns base64 image data with analysis instructions. " +
        "Use this before UIDesignAgent runs — pass the result to Vision analysis " +
        "to extract colors, layout, UI patterns for ui_design_spec.json.")]
    public string AnalyzeReferenceImage(string imageName)
    {
        var projectRoot = Path.GetDirectoryName(Application.dataPath);
        var path = Path.Combine(projectRoot, "references", imageName);

        if (!File.Exists(path))
        {
            var available = Directory.Exists(Path.Combine(projectRoot, "references"))
                ? string.Join(", ", Directory.GetFiles(Path.Combine(projectRoot, "references"), "*.png"))
                : "references/ folder does not exist";
            return $"ERROR: '{imageName}' not found.\nAvailable: {available}";
        }

        var bytes = File.ReadAllBytes(path);
        var base64 = Convert.ToBase64String(bytes);
        var ext = Path.GetExtension(imageName).ToLower();
        var mimeType = ext == ".jpg" || ext == ".jpeg" ? "image/jpeg" : "image/png";

        return $"REFERENCE_IMAGE_READY\n" +
               $"file: {imageName}\n" +
               $"size: {bytes.Length / 1024}KB\n" +
               $"mime: {mimeType}\n" +
               $"base64_length: {base64.Length}\n" +
               $"instruction: Analyze this game screenshot or UI reference. Extract exactly:\n" +
               $"  - background_color (hex)\n" +
               $"  - panel_color (hex)\n" +
               $"  - panel_corner_radius (px estimate)\n" +
               $"  - primary_button_color (hex)\n" +
               $"  - accent_color (hex)\n" +
               $"  - text_color (hex)\n" +
               $"  - score_panel_shape (cloud/pill/flat/ribbon/number-only)\n" +
               $"  - score_panel_position (top-center/top-left/top-right)\n" +
               $"  - button_style (flat/3d-raised/outlined)\n" +
               $"  - game_over_style (banner/card/overlay)\n" +
               $"  - font_weight (thin/regular/bold/black)\n" +
               $"  - full_color_palette (list of 5-8 hex codes)\n" +
               $"Return as JSON matching ui_design_spec.json structure.\n" +
               $"BASE64:{base64}";
    }

    // ─────────────────────────────────────────────
    // TOOL 2 — Görselden sprite crop et, Unity'ye import et
    // ─────────────────────────────────────────────
    [McpPluginTool("extract_sprite_from_reference")]
    [Description(
        "Crops a rectangular region from a reference image and imports it as a sprite. " +
        "Saves to Assets/Art/Extracted/[outputName].png. " +
        "Use pixel coordinates from the source image. " +
        "Example: extract the watermelon from a Suika Game screenshot.")]
    public string ExtractSpriteFromReference(
        string imageName,
        int x, int y,
        int width, int height,
        string outputName)
    {
        var projectRoot = Path.GetDirectoryName(Application.dataPath);
        var inputPath = Path.Combine(projectRoot, "references", imageName);

        if (!File.Exists(inputPath))
            return $"ERROR: '{imageName}' not found in references/ folder";

        if (width <= 0 || height <= 0)
            return "ERROR: width and height must be greater than 0";

        // Görseli yükle
        var bytes = File.ReadAllBytes(inputPath);
        var tex = new Texture2D(2, 2);
        if (!tex.LoadImage(bytes))
            return "ERROR: Failed to load image — check file format (PNG or JPG only)";

        // Sınır kontrolü
        x = Mathf.Clamp(x, 0, tex.width - 1);
        y = Mathf.Clamp(y, 0, tex.height - 1);
        width = Mathf.Clamp(width, 1, tex.width - x);
        height = Mathf.Clamp(height, 1, tex.height - y);

        // Unity'nin koordinat sistemi Y-flipped
        int flippedY = tex.height - y - height;

        // Crop
        var cropped = new Texture2D(width, height, TextureFormat.RGBA32, false);
        var pixels = tex.GetPixels(x, flippedY, width, height);
        cropped.SetPixels(pixels);
        cropped.Apply();

        // Kaydet
        var outputDir = Path.Combine(Application.dataPath, "Art", "Extracted");
        if (!Directory.Exists(outputDir))
            Directory.CreateDirectory(outputDir);

        var outputPath = Path.Combine(outputDir, $"{outputName}.png");
        File.WriteAllBytes(outputPath, cropped.EncodeToPNG());
        AssetDatabase.Refresh();

        // Sprite olarak import et
        var assetPath = $"Assets/Art/Extracted/{outputName}.png";
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 100;
            importer.filterMode = FilterMode.Bilinear;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        return $"SUCCESS: Sprite extracted and imported\n" +
               $"path: {assetPath}\n" +
               $"size: {width}x{height}px\n" +
               $"usage: Load with Resources.Load or assign in inspector";
    }

    // ─────────────────────────────────────────────
    // TOOL 3 — Sahnedeki görsel kaliteyi kontrol et
    // ─────────────────────────────────────────────
    [McpPluginTool("check_visual_quality")]
    [Description(
        "Scans ALL renderers and UI images in the current scene. " +
        "Reports: missing sprites, gray/uncolored objects, default white panels, " +
        "placeholder visuals. Run this after every visual agent completes. " +
        "A clean result means no gray or placeholder objects exist.")]
    public string CheckVisualQuality()
    {
        var issues = new List<string>();
        var ok = new List<string>();

        // SpriteRenderer kontrolü
        var spriteRenderers = GameObject.FindObjectsOfType<SpriteRenderer>(true);
        foreach (var r in spriteRenderers)
        {
            var name = GetPath(r.gameObject);

            if (r.sprite == null)
            {
                issues.Add($"[NO SPRITE]  {name}");
                continue;
            }

            Color.RGBToHSV(r.color, out _, out float sat, out float val);

            if (sat < 0.12f && val > 0.25f)
                issues.Add($"[GRAY]       {name}  (saturation={sat:F2}, likely uncolored)");
            else if (r.color == Color.white && r.sprite.name.ToLower().Contains("default"))
                issues.Add($"[DEFAULT]    {name}  (white + default sprite — placeholder)");
            else
                ok.Add($"✓ {name}");
        }

        // UI Image kontrolü
        var images = GameObject.FindObjectsOfType<Image>(true);
        foreach (var img in images)
        {
            var name = GetPath(img.gameObject);
            Color.RGBToHSV(img.color, out _, out float sat, out float val);

            if (img.sprite == null && sat < 0.10f && val > 0.20f)
                issues.Add($"[UI-GRAY]    {name}  (flat gray panel, no sprite)");
        }

        if (issues.Count == 0)
            return $"✅ Visual quality PASSED\n" +
                   $"Checked: {spriteRenderers.Length} sprite renderers, {images.Length} UI images\n" +
                   $"Result: No gray or placeholder objects found";

        return $"⚠️ Visual quality FAILED — {issues.Count} issue(s) found:\n\n" +
               string.Join("\n", issues) +
               $"\n\nFix all issues before QA passes.\n" +
               $"Total checked: {spriteRenderers.Length + images.Length} objects";
    }

    // ─────────────────────────────────────────────
    // TOOL 4 — Game view screenshot al
    // ─────────────────────────────────────────────
    [McpPluginTool("capture_game_view")]
    [Description(
        "Captures the current Scene view as a PNG screenshot. " +
        "Saves to QA/screenshots/ folder in project root. " +
        "Returns the file path. Use after each agent completes " +
        "to visually verify the build state.")]
    public string CaptureGameView()
    {
        var projectRoot = Path.GetDirectoryName(Application.dataPath);
        var outputDir = Path.Combine(projectRoot, "QA", "screenshots");

        if (!Directory.Exists(outputDir))
            Directory.CreateDirectory(outputDir);

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var filename = $"screenshot_{timestamp}.png";
        var outputPath = Path.Combine(outputDir, filename);

        try
        {
            // Scene view üzerinden screenshot
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
                return "WARNING: No active Scene view found. Open the Scene view window first.";

            int width  = (int)sceneView.position.width;
            int height = (int)sceneView.position.height;

            var rt = new RenderTexture(width, height, 24);
            var prev = RenderTexture.active;

            sceneView.camera.targetTexture = rt;
            sceneView.camera.Render();
            RenderTexture.active = rt;

            var screenshot = new Texture2D(width, height, TextureFormat.RGB24, false);
            screenshot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            screenshot.Apply();

            RenderTexture.active = prev;
            sceneView.camera.targetTexture = null;
            rt.Release();

            File.WriteAllBytes(outputPath, screenshot.EncodeToPNG());

            return $"SUCCESS: Screenshot saved\n" +
                   $"path: QA/screenshots/{filename}\n" +
                   $"size: {width}x{height}px\n" +
                   $"Note: Open this file to visually verify the build state";
        }
        catch (Exception e)
        {
            // Fallback: ScreenCapture
            ScreenCapture.CaptureScreenshot(outputPath);
            return $"Screenshot saved (fallback method)\n" +
                   $"path: QA/screenshots/{filename}\n" +
                   $"Note: {e.Message}";
        }
    }

    // ─────────────────────────────────────────────
    // TOOL 5 — references/ klasöründeki dosyaları listele
    // ─────────────────────────────────────────────
    [McpPluginTool("list_reference_images")]
    [Description(
        "Lists all image files in the references/ folder at project root. " +
        "Call this first to see what reference images are available " +
        "before calling analyze_reference_image.")]
    public string ListReferenceImages()
    {
        var projectRoot = Path.GetDirectoryName(Application.dataPath);
        var refDir = Path.Combine(projectRoot, "references");

        if (!Directory.Exists(refDir))
            return "references/ folder does not exist.\n" +
                   $"Create it at: {refDir}\n" +
                   "Then add PNG/JPG reference images (e.g. suika_screenshot.png)";

        var extensions = new[] { "*.png", "*.jpg", "*.jpeg", "*.webp" };
        var files = new List<string>();

        foreach (var ext in extensions)
            files.AddRange(Directory.GetFiles(refDir, ext));

        if (files.Count == 0)
            return $"references/ folder exists but is empty.\n" +
                   $"Add PNG or JPG images to: {refDir}";

        var result = $"Found {files.Count} reference image(s):\n";
        foreach (var f in files)
        {
            var info = new FileInfo(f);
            result += $"  - {info.Name}  ({info.Length / 1024}KB)\n";
        }
        return result;
    }

    // ─────────────────────────────────────────────
    // Yardımcı — GameObject hiyerarşi yolu
    // ─────────────────────────────────────────────
    private string GetPath(GameObject go)
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