using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace NAV.Editor
{
    /// <summary>
    /// Cuts the developer's UI kit sheets (generated in ChatGPT, 2026-10-24) into sprites for the
    /// UI Toolkit theme. Sources: Assets/NAV/Art/UI/Source~/Kit_*.png (the "~" folder is not
    /// imported by Unity). Two sheets have real alpha; the others came with the transparency
    /// checkerboard painted into the image, so the background is found by flood-filling light,
    /// colourless pixels from the sheet's edges. Pixels right next to it are faded against the
    /// background colour (anti-aliased edges), and on the glow elements (hover button, selected
    /// slot) a wide band is, so the golden glow becomes semi-transparent instead of sitting on grey.
    ///
    /// Each element is cropped by its rectangle (pixels from the sheet's top-left), halved (the
    /// sheets are about 2x the size the UI shows them at 1080p) and written to Art/UI/Kit as a
    /// sprite with mipmaps and its 9-slice border, so frames and buttons stretch to any size.
    /// Re-run after replacing a sheet; adjust the table if the layout of a sheet changed.
    /// </summary>
    internal static class UiKitSetup
    {
        private const string SourceFolder = "Assets/NAV/Art/UI/Source~/";
        private const string OutputFolder = "Assets/NAV/Art/UI/Kit";
        private const int Padding = 3;
        private const float Scale = 0.5f;
        private const int EdgeBand = 2;
        private const int GlowBand = 48;

        private struct Element
        {
            public string Sheet;
            public string Name;
            public RectInt Rect;
            public bool Glow;
            public RectInt Solid;
            public Vector4 Border;
            // Also clear light colourless areas enclosed by the element (the inside of a ring).
            public bool ClearEnclosed;
            // Sheet rows (top-based, inclusive) dropped from the crop - stitches a strip around
            // something in its middle (the scrollbar track without its thumb).
            public Vector2Int SkipRows;
            // Transparent margin (source px, each side) added around the crop - gives the plain
            // button/slot the same size as its glowing hover/selected version, so states swap in place.
            public Vector2Int Margin;

            public Element(string sheet, string name, int x0, int top0, int x1, int top1, bool glow = false, Vector4 border = default)
            {
                Sheet = sheet;
                Name = name;
                Rect = new RectInt(x0, top0, x1 - x0 + 1, top1 - top0 + 1);
                Glow = glow;
                Solid = default;
                Border = border;
                ClearEnclosed = false;
                SkipRows = default;
                Margin = default;
            }
        }

        // Border = 9-slice (left, bottom, right, top) in OUTPUT pixels (after halving).
        private static readonly Element[] Elements =
        {
            new("TitlePlaque", "TitlePlaque", 87, 182, 1449, 460),
            new("TitlePlaque", "Divider", 145, 607, 1391, 659),
            new("TitlePlaque", "DividerOrnate", 103, 785, 1432, 871),

            new("Panels", "PanelWood", 24, 25, 1044, 491, border: new Vector4(40, 40, 40, 40)),
            new("Panels", "PanelParchment", 24, 522, 1044, 988, border: new Vector4(48, 48, 48, 48)),
            new("Panels", "DialogParchment", 1084, 624, 1510, 932, border: new Vector4(34, 34, 34, 34)),

            new("Buttons", "Button", 155, 34, 1377, 159, border: new Vector4(57, 0, 57, 0)) { Margin = new Vector2Int(26, 25) },
            new("Buttons", "ButtonHover", 129, 182, 1403, 358, glow: true, border: new Vector4(57, 0, 57, 0)) { Solid = new RectInt(155, 207, 1223, 126) },
            new("Buttons", "ButtonPressed", 155, 368, 1377, 495, border: new Vector4(57, 0, 57, 0)) { Margin = new Vector2Int(26, 25) },
            new("Buttons", "ButtonDisabled", 155, 534, 1377, 658, border: new Vector4(57, 0, 57, 0)) { Margin = new Vector2Int(26, 25) },
            new("Buttons", "ButtonDanger", 155, 700, 1377, 827, border: new Vector4(57, 0, 57, 0)) { Margin = new Vector2Int(26, 25) },
            new("Buttons", "ButtonSquare", 691, 849, 845, 999, border: new Vector4(22, 22, 22, 22)),

            new("Slots", "SlotOrnament", 135, 150, 505, 500) { Margin = new Vector2Int(20, 19) },
            new("Slots", "Slot", 582, 150, 950, 500) { Margin = new Vector2Int(20, 19) },
            new("Slots", "SlotSelected", 1008, 130, 1415, 517, glow: true) { Solid = new RectInt(1028, 148, 369, 352) },
            new("Slots", "HotbarCapLeft", 131, 573, 244, 894),
            new("Slots", "HotbarCapRight", 1289, 573, 1403, 894),

            new("InputTooltipScroll", "InputField", 101, 176, 1078, 300, border: new Vector4(26, 14, 44, 14)),
            new("InputTooltipScroll", "Tooltip", 100, 369, 1078, 881, border: new Vector4(40, 40, 40, 40)),
            new("InputTooltipScroll", "ScrollUp", 1181, 88, 1270, 174),
            new("InputTooltipScroll", "ScrollDown", 1181, 701, 1270, 790),
            new("InputTooltipScroll", "ScrollThumb", 1195, 329, 1257, 475),
            new("InputTooltipScroll", "ScrollTrack", 1181, 175, 1270, 700, border: new Vector4(0, 20, 0, 20)) { SkipRows = new Vector2Int(322, 482) },
            new("InputTooltipScroll", "Badge", 1176, 837, 1396, 925, border: new Vector4(18, 0, 18, 0)),

            new("Bars", "BarVertical", 47, 14, 213, 982, border: new Vector4(0, 75, 0, 75)),
            new("Bars", "BarHorizontal", 292, 149, 1442, 317, border: new Vector4(75, 0, 75, 0)),
            new("Bars", "FillRed", 413, 431, 1332, 510, border: new Vector4(6, 6, 6, 6)),
            new("Bars", "FillGold", 413, 580, 1333, 655, border: new Vector4(6, 6, 6, 6)),
            new("Bars", "BarSmall", 524, 806, 1167, 889, border: new Vector4(26, 0, 26, 0)),

            new("FoodDay", "FoodSlot", 206, 0, 715, 600),
            new("FoodDay", "FoodRing", 851, 92, 1309, 548) { ClearEnclosed = true },
            new("FoodDay", "DaySign", 168, 640, 1361, 962),

            new("Icons", "IconWeight", 44, 27, 364, 343) { ClearEnclosed = true },
            new("Icons", "IconDice", 438, 38, 733, 357),
            new("Icons", "IconWarning", 803, 33, 1123, 364),
            new("Icons", "IconEmpty", 1180, 28, 1493, 370),
            new("Icons", "IconClose", 220, 385, 520, 681),
            new("Icons", "IconSettings", 613, 383, 917, 686),
            new("Icons", "IconBack", 1009, 385, 1308, 680),
            new("Icons", "WeatherSun", 18, 716, 266, 966),
            new("Icons", "WeatherSunCloud", 272, 716, 515, 965),
            new("Icons", "WeatherCloud", 522, 717, 764, 966),
            new("Icons", "WeatherRain", 771, 716, 1015, 966),
            new("Icons", "WeatherStorm", 1022, 716, 1264, 966),
            new("Icons", "WeatherNight", 1270, 716, 1517, 966),

            // The game logo (developer's image, real alpha) - main menu and loading screen.
            new("Logo", "Logo", 100, 99, 1671, 791),
        };

        [MenuItem("NAV/UI/Build UI Kit Sprites")]
        private static void Build()
        {
            Directory.CreateDirectory(OutputFolder);
            var sheets = new Dictionary<string, Sheet>();
            var written = new List<(string path, Vector4 border)>();
            foreach (Element element in Elements)
            {
                if (!sheets.TryGetValue(element.Sheet, out Sheet sheet))
                {
                    sheet = Sheet.Load($"{SourceFolder}Kit_{element.Sheet}.png");
                    if (sheet == null)
                    {
                        Debug.LogError($"UiKitSetup: missing source sheet Kit_{element.Sheet}.png in {SourceFolder}.");
                        return;
                    }

                    sheets[element.Sheet] = sheet;
                }

                if (element.ClearEnclosed)
                {
                    sheet.ClearEnclosed(element.Rect);
                }

                Color32[] cut = sheet.Cut(element.Rect, element.Glow, element.Solid, out int width, out int height);
                if (element.SkipRows != default)
                {
                    cut = DropRows(cut, width, ref height, element.Rect.y - Padding, element.SkipRows);
                }

                if (element.Margin != default)
                {
                    cut = AddMargin(cut, ref width, ref height, element.Margin);
                }

                Color32[] scaled = Downscale(cut, width, height, out int outWidth, out int outHeight);
                var texture = new Texture2D(outWidth, outHeight, TextureFormat.RGBA32, false);
                texture.SetPixels32(scaled);
                string path = $"{OutputFolder}/{element.Name}.png";
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                written.Add((path, element.Border));
            }

            AssetDatabase.Refresh();
            foreach ((string path, Vector4 border) in written)
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = true;
                importer.filterMode = FilterMode.Trilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.spriteBorder = border;
                importer.SaveAndReimport();
            }

            AssignWeatherIcons();
            Debug.Log($"UiKitSetup: {written.Count} UI kit sprites written to {OutputFolder}; weather icons assigned.");
        }

        // The HUD's day sign shows WeatherDefinition.GetIcon.
        private static void AssignWeatherIcons()
        {
            SetWeatherIcons("ClearWeather", "WeatherSun", "WeatherNight");
            SetWeatherIcons("RainWeather", "WeatherRain", null);
            SetWeatherIcons("HeavyRainWeather", "WeatherRain", null);
            SetWeatherIcons("ThunderstormWeather", "WeatherStorm", null);
            AssetDatabase.SaveAssets();
        }

        private static void SetWeatherIcons(string weather, string icon, string nightIcon)
        {
            var definition = AssetDatabase.LoadAssetAtPath<ScriptableObject>($"Assets/NAV/ScriptableObjects/Weather/{weather}.asset");
            if (definition == null)
            {
                Debug.LogWarning($"UiKitSetup: weather '{weather}' not found - no HUD icon assigned.");
                return;
            }

            var serialized = new SerializedObject(definition);
            serialized.FindProperty("_icon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>($"{OutputFolder}/{icon}.png");
            serialized.FindProperty("_nightIcon").objectReferenceValue = nightIcon != null ? AssetDatabase.LoadAssetAtPath<Sprite>($"{OutputFolder}/{nightIcon}.png") : null;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
        }

        private static Color32[] AddMargin(Color32[] pixels, ref int width, ref int height, Vector2Int margin)
        {
            int newWidth = width + margin.x * 2;
            int newHeight = height + margin.y * 2;
            var result = new Color32[newWidth * newHeight];
            for (int y = 0; y < height; y++)
            {
                System.Array.Copy(pixels, y * width, result, (y + margin.y) * newWidth + margin.x, width);
            }

            width = newWidth;
            height = newHeight;
            return result;
        }

        // Removes the rows whose sheet row (top-based) is inside skip; cropTop is the sheet row of
        // the crop's first (top) row. Rows are stored bottom-up.
        private static Color32[] DropRows(Color32[] pixels, int width, ref int height, int cropTop, Vector2Int skip)
        {
            var kept = new List<Color32>(pixels.Length);
            for (int row = 0; row < height; row++)
            {
                int sheetTop = cropTop + (height - 1 - row);
                if (sheetTop < skip.x || sheetTop > skip.y)
                {
                    for (int x = 0; x < width; x++)
                    {
                        kept.Add(pixels[row * width + x]);
                    }
                }
            }

            height = kept.Count / width;
            return kept.ToArray();
        }

        // Halves the image with a 2x2 box filter, alpha-weighted so transparent pixels don't
        // darken the edges.
        private static Color32[] Downscale(Color32[] source, int width, int height, out int outWidth, out int outHeight)
        {
            int step = Mathf.RoundToInt(1f / Scale);
            outWidth = width / step;
            outHeight = height / step;
            var result = new Color32[outWidth * outHeight];
            for (int y = 0; y < outHeight; y++)
            {
                for (int x = 0; x < outWidth; x++)
                {
                    float r = 0f, g = 0f, b = 0f, a = 0f;
                    for (int dy = 0; dy < step; dy++)
                    {
                        for (int dx = 0; dx < step; dx++)
                        {
                            Color32 p = source[(y * step + dy) * width + x * step + dx];
                            float weight = p.a / 255f;
                            r += p.r * weight;
                            g += p.g * weight;
                            b += p.b * weight;
                            a += weight;
                        }
                    }

                    result[y * outWidth + x] = a > 0f
                        ? new Color32((byte)(r / a), (byte)(g / a), (byte)(b / a), (byte)(a / (step * step) * 255f))
                        : new Color32(0, 0, 0, 0);
                }
            }

            return result;
        }

        private sealed class Sheet
        {
            private Color32[] _pixels;
            private bool[] _background;
            private int[] _distance;
            private int _width;
            private int _height;
            private bool _hasAlpha;

            public static Sheet Load(string path)
            {
                if (!File.Exists(path))
                {
                    return null;
                }

                var texture = new Texture2D(2, 2);
                texture.LoadImage(File.ReadAllBytes(path));
                var sheet = new Sheet
                {
                    _pixels = texture.GetPixels32(),
                    _width = texture.width,
                    _height = texture.height,
                };
                Object.DestroyImmediate(texture);
                sheet._hasAlpha = sheet._pixels[0].a == 0;
                sheet.FindBackground();
                return sheet;
            }

            // Flood fill from the borders over background-like pixels, then a BFS distance (in
            // pixels) from the background for the edge treatment.
            private void FindBackground()
            {
                int count = _width * _height;
                _background = new bool[count];
                var queue = new Queue<int>();
                for (int x = 0; x < _width; x++)
                {
                    queue.Enqueue(x);
                    queue.Enqueue((_height - 1) * _width + x);
                }

                for (int y = 0; y < _height; y++)
                {
                    queue.Enqueue(y * _width);
                    queue.Enqueue(y * _width + _width - 1);
                }

                while (queue.Count > 0)
                {
                    int i = queue.Dequeue();
                    if (_background[i] || !LooksLikeBackground(_pixels[i]))
                    {
                        continue;
                    }

                    _background[i] = true;
                    EnqueueNeighbours(queue, i);
                }

                ComputeDistances();
            }

            // BFS distance from the background, capped at GlowBand.
            private void ComputeDistances()
            {
                int count = _width * _height;
                var queue = new Queue<int>();
                _distance = new int[count];
                for (int i = 0; i < count; i++)
                {
                    _distance[i] = _background[i] ? 0 : int.MaxValue;
                    if (_background[i])
                    {
                        queue.Enqueue(i);
                    }
                }

                while (queue.Count > 0)
                {
                    int i = queue.Dequeue();
                    int next = _distance[i] + 1;
                    if (next > GlowBand)
                    {
                        continue;
                    }

                    int x = i % _width;
                    int y = i / _width;
                    Relax(queue, x > 0 ? i - 1 : -1, next);
                    Relax(queue, x < _width - 1 ? i + 1 : -1, next);
                    Relax(queue, y > 0 ? i - _width : -1, next);
                    Relax(queue, y < _height - 1 ? i + _width : -1, next);
                }
            }

            /// <summary>Marks light colourless areas inside rect (top-based) as background when they
            /// are bigger than a highlight - the inside of a ring, gaps between chains.</summary>
            public void ClearEnclosed(RectInt rect)
            {
                var seen = new bool[_width * _height];
                var region = new List<int>();
                var stack = new Stack<int>();
                for (int top = rect.y; top < rect.yMax; top++)
                {
                    for (int x = rect.x; x < rect.xMax; x++)
                    {
                        int start = (_height - 1 - top) * _width + x;
                        if (seen[start] || _background[start] || !LooksLikeBackground(_pixels[start]))
                        {
                            continue;
                        }

                        region.Clear();
                        stack.Push(start);
                        seen[start] = true;
                        while (stack.Count > 0)
                        {
                            int i = stack.Pop();
                            region.Add(i);
                            int px = i % _width;
                            int py = i / _width;
                            foreach (int j in new[] { px > 0 ? i - 1 : -1, px < _width - 1 ? i + 1 : -1, py > 0 ? i - _width : -1, py < _height - 1 ? i + _width : -1 })
                            {
                                if (j >= 0 && !seen[j] && !_background[j] && LooksLikeBackground(_pixels[j]))
                                {
                                    seen[j] = true;
                                    stack.Push(j);
                                }
                            }
                        }

                        if (region.Count >= 60)
                        {
                            foreach (int i in region)
                            {
                                _background[i] = true;
                            }
                        }
                    }
                }

                ComputeDistances();
            }

            private void Relax(Queue<int> queue, int j, int distance)
            {
                if (j >= 0 && distance < _distance[j])
                {
                    _distance[j] = distance;
                    queue.Enqueue(j);
                }
            }

            private void EnqueueNeighbours(Queue<int> queue, int i)
            {
                int x = i % _width;
                int y = i / _width;
                if (x > 0) queue.Enqueue(i - 1);
                if (x < _width - 1) queue.Enqueue(i + 1);
                if (y > 0) queue.Enqueue(i - _width);
                if (y < _height - 1) queue.Enqueue(i + _width);
            }

            private bool LooksLikeBackground(Color32 p)
            {
                if (_hasAlpha)
                {
                    return p.a < 10;
                }

                int max = Mathf.Max(p.r, Mathf.Max(p.g, p.b));
                int min = Mathf.Min(p.r, Mathf.Min(p.g, p.b));
                return min >= 205 && max - min <= 14;
            }

            /// <summary>The element's pixels (bottom-up rows, as Texture2D wants), background
            /// cleared and edges faded against the light background - on glow elements everything outside the Solid rectangle (the halo) too.</summary>
            public Color32[] Cut(RectInt topLeftRect, bool glow, RectInt solid, out int width, out int height)
            {
                int x0 = Mathf.Max(0, topLeftRect.x - Padding);
                int x1 = Mathf.Min(_width - 1, topLeftRect.xMax - 1 + Padding);
                int top0 = Mathf.Max(0, topLeftRect.y - Padding);
                int top1 = Mathf.Min(_height - 1, topLeftRect.yMax - 1 + Padding);
                width = x1 - x0 + 1;
                height = top1 - top0 + 1;
                var result = new Color32[width * height];
                for (int top = top0; top <= top1; top++)
                {
                    int sheetRow = _height - 1 - top;
                    int outRow = height - 1 - (top - top0);
                    for (int x = x0; x <= x1; x++)
                    {
                        int i = sheetRow * _width + x;
                        Color32 p = _pixels[i];
                        if (_background[i])
                        {
                            p = new Color32(0, 0, 0, 0);
                        }
                        else if (!_hasAlpha && (_distance[i] <= EdgeBand || (glow && _distance[i] <= GlowBand && !solid.Contains(new Vector2Int(x, top)))))
                        {
                            p = FadeAgainstLight(p);
                        }

                        result[outRow * width + (x - x0)] = p;
                    }
                }

                return result;
            }

            // "Colour to alpha" against a light grey (the painted checkerboard averages ~245):
            // the smallest alpha that explains the pixel as colour over that background.
            private static Color32 FadeAgainstLight(Color32 p)
            {
                const float background = 245f;
                float alpha = 0f;
                foreach (float channel in new float[] { p.r, p.g, p.b })
                {
                    alpha = Mathf.Max(alpha, Mathf.Clamp01((background - channel) / background));
                }

                if (alpha < 0.08f)
                {
                    return new Color32(0, 0, 0, 0);
                }

                byte Unblend(byte c) => (byte)Mathf.Clamp((c - (1f - alpha) * background) / alpha, 0f, 255f);
                return new Color32(Unblend(p.r), Unblend(p.g), Unblend(p.b), (byte)(alpha * 255f));
            }
        }
    }
}
