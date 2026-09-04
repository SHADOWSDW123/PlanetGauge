using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using ADOFAI;
using HarmonyLib;
using UnityEngine;

namespace PlanetGauge
{
    /// <summary>
    /// 태그별 장식 스킨 명령을 보관하고 이미지 장식의 최종 렌더 메시를 게이지처럼 자른다.
    /// 장식 Transform과 PlanetGauge 판정 상태는 수정하지 않는다.
    /// </summary>
    internal static class PlanetGaugeDecorationSkinRuntime
    {
        private sealed class TagCommand
        {
            internal PlanetGaugeSkinGaugeType GaugeType;
            internal long Sequence;
        }

        private sealed class DecorationBinding
        {
            internal scrVisualDecoration Decoration;
            internal MeshFilter Filter;
            internal Mesh OriginalMesh;
            internal Mesh WorkingMesh;
            internal Vector3[] OriginalVertices;
            internal Vector3[] WorkingVertices;
            internal Vector2[] OriginalUvs;
            internal Vector2[] WorkingUvs;
            internal PlanetGaugeSkinGaugeType GaugeType;
            internal PlanetGaugeSkinGaugeType LastGaugeType;
            internal float LastProgress = float.NaN;
            internal float MinimumCoordinate;
            internal float MaximumCoordinate;
            internal float UvAtMinimum;
            internal float UvAtMaximum;
            internal int AlphaTextureId;
            internal float TextureAlphaMinimum;
            internal float TextureAlphaMaximum = 1f;
            internal float AlphaMinimum;
            internal float AlphaMaximum = 1f;
            internal bool UsesLegacyRenderPath;
        }

        private struct TextureAlphaBounds
        {
            internal float MinimumX;
            internal float MaximumX;
            internal float MinimumY;
            internal float MaximumY;
        }

        private static readonly Dictionary<string, TagCommand> commands =
            new Dictionary<string, TagCommand>(StringComparer.Ordinal);
        private static readonly Dictionary<scrVisualDecoration, DecorationBinding> bindings =
            new Dictionary<scrVisualDecoration, DecorationBinding>();
        private static readonly Dictionary<int, TextureAlphaBounds> alphaBoundsCache =
            new Dictionary<int, TextureAlphaBounds>();
        private static readonly HashSet<int> unsupportedDecorationIds = new HashSet<int>();

        private static long nextSequence;

        internal static int ActiveTagCount { get { return commands.Count; } }
        internal static int BoundDecorationCount { get { return bindings.Count; } }
        internal static int LegacyRenderDecorationCount
        {
            get { return bindings.Values.Count(binding => binding.UsesLegacyRenderPath); }
        }

        internal static string DescribeAlphaRange()
        {
            DecorationBinding binding = bindings.Values.FirstOrDefault();
            if (binding == null || binding.AlphaTextureId == 0)
            {
                return LocalizedStrings.Pending;
            }

            return "T:"
                + binding.TextureAlphaMinimum.ToString("0.###", CultureInfo.InvariantCulture)
                + ".."
                + binding.TextureAlphaMaximum.ToString("0.###", CultureInfo.InvariantCulture)
                + " L:"
                + binding.AlphaMinimum.ToString("0.###", CultureInfo.InvariantCulture)
                + ".."
                + binding.AlphaMaximum.ToString("0.###", CultureInfo.InvariantCulture);
        }

        internal static float Progress
        {
            get
            {
                float maximum = GaugeRuntime.RecoveryMaximum;
                return maximum <= 0f
                    ? 0f
                    : Mathf.Clamp01(GaugeRuntime.Current / maximum);
            }
        }

        internal static void ApplyCommand(
            string targetTag,
            bool enabled,
            PlanetGaugeSkinGaugeType gaugeType)
        {
            string[] tags = SplitTags(targetTag);
            if (tags.Length == 0)
            {
                return;
            }

            if (!Enum.IsDefined(typeof(PlanetGaugeSkinGaugeType), gaugeType))
            {
                gaugeType = PlanetGaugeSkinGaugeType.Horizontal;
            }

            for (int index = 0; index < tags.Length; index++)
            {
                string tag = tags[index];
                if (enabled)
                {
                    commands[tag] = new TagCommand
                    {
                        GaugeType = gaugeType,
                        Sequence = ++nextSequence
                    };
                }
                else
                {
                    commands.Remove(tag);
                }
            }

            scrDecorationManager manager = scrDecorationManager.instance;
            if (manager != null)
            {
                RefreshBindings(manager, true);
            }
            else if (commands.Count == 0)
            {
                RestoreAll();
            }
        }

        internal static void UpdateAfterDecorations(scrDecorationManager manager)
        {
            if (manager == null)
            {
                return;
            }

            if (!GaugeRuntime.IsGameplayContext(true) || commands.Count == 0)
            {
                RestoreAll();
                return;
            }

            RefreshBindings(manager, false);
        }

        internal static void ApplyAfterShaderUpdate(scrVisualDecoration decoration)
        {
            if (decoration == null || !GaugeRuntime.IsGameplayContext(true))
            {
                return;
            }

            DecorationBinding binding;
            if (!bindings.TryGetValue(decoration, out binding))
            {
                return;
            }

            if (!OwnsWorkingMesh(binding))
            {
                RebuildBindingMesh(binding);
            }

            EnsureVisibleRenderPath(binding);
            ApplyCrop(binding, Progress, true);
        }

        internal static void Reset()
        {
            RestoreAll();
            commands.Clear();
            bindings.Clear();
            alphaBoundsCache.Clear();
            unsupportedDecorationIds.Clear();
            nextSequence = 0;
        }

        internal static string DescribeCurrent()
        {
            if (commands.Count == 0)
            {
                return LocalizedStrings.None;
            }

            return string.Join(
                ", ",
                commands
                    .OrderBy(pair => pair.Value.Sequence)
                    .Select(pair => pair.Key + "=" + pair.Value.GaugeType));
        }

        private static void RefreshBindings(scrDecorationManager manager, bool forceApply)
        {
            Dictionary<scrVisualDecoration, TagCommand> desired =
                new Dictionary<scrVisualDecoration, TagCommand>();

            foreach (KeyValuePair<string, TagCommand> pair in commands)
            {
                IEnumerable<scrDecoration> tagged;
                try
                {
                    tagged = manager.GetTaggedDecorations(pair.Key);
                }
                catch (Exception exception)
                {
                    Main.LogException("장식 태그 조회에 실패했습니다: " + pair.Key, exception);
                    continue;
                }

                foreach (scrDecoration decoration in tagged)
                {
                    scrVisualDecoration visual = decoration as scrVisualDecoration;
                    if (visual == null)
                    {
                        continue;
                    }

                    TagCommand previous;
                    if (!desired.TryGetValue(visual, out previous)
                        || pair.Value.Sequence > previous.Sequence)
                    {
                        desired[visual] = pair.Value;
                    }
                }
            }

            scrVisualDecoration[] previouslyBound = bindings.Keys.ToArray();
            for (int index = 0; index < previouslyBound.Length; index++)
            {
                scrVisualDecoration decoration = previouslyBound[index];
                if (decoration == null || !desired.ContainsKey(decoration))
                {
                    RestoreAndRemove(decoration);
                }
            }

            float progress = Progress;
            foreach (KeyValuePair<scrVisualDecoration, TagCommand> pair in desired)
            {
                DecorationBinding binding;
                if (!bindings.TryGetValue(pair.Key, out binding))
                {
                    binding = CreateBinding(pair.Key);
                    if (binding == null)
                    {
                        continue;
                    }

                    bindings[pair.Key] = binding;
                    forceApply = true;
                }

                bool gaugeTypeChanged = binding.GaugeType != pair.Value.GaugeType;
                binding.GaugeType = pair.Value.GaugeType;

                if (!OwnsWorkingMesh(binding) && !RebuildBindingMesh(binding))
                {
                    RestoreAndRemove(pair.Key);
                    continue;
                }

                EnsureVisibleRenderPath(binding);
                ApplyCrop(binding, progress, forceApply || gaugeTypeChanged);
            }
        }

        private static DecorationBinding CreateBinding(scrVisualDecoration decoration)
        {
            if (decoration == null || decoration.meshRendererObj == null)
            {
                LogUnsupported(decoration, "최종 이미지 렌더러가 없습니다.");
                return null;
            }

            MeshFilter filter = decoration.meshRendererObj.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null)
            {
                LogUnsupported(decoration, "최종 이미지 렌더 메시가 없습니다.");
                return null;
            }

            DecorationBinding binding = new DecorationBinding
            {
                Decoration = decoration,
                Filter = filter,
                GaugeType = PlanetGaugeSkinGaugeType.Horizontal,
                LastGaugeType = PlanetGaugeSkinGaugeType.Horizontal
            };

            return RebuildBindingMesh(binding) ? binding : null;
        }

        private static bool RebuildBindingMesh(DecorationBinding binding)
        {
            if (binding == null
                || binding.Decoration == null
                || binding.Decoration.meshRendererObj == null)
            {
                return false;
            }

            MeshFilter filter = binding.Decoration.meshRendererObj.GetComponent<MeshFilter>();
            if (filter == null)
            {
                LogUnsupported(binding.Decoration, "최종 이미지 렌더 메시 필터가 없습니다.");
                return false;
            }

            Mesh source = filter.sharedMesh;
            if (source == binding.WorkingMesh)
            {
                source = binding.OriginalMesh;
            }

            if (source == null)
            {
                LogUnsupported(binding.Decoration, "최종 이미지 렌더 메시가 없습니다.");
                return false;
            }

            DestroyWorkingMesh(binding, false);

            Vector3[] vertices = source.vertices;
            Vector2[] uvs = source.uv;
            if (vertices == null
                || uvs == null
                || vertices.Length < 4
                || vertices.Length != uvs.Length)
            {
                LogUnsupported(binding.Decoration, "직사각형 이미지 메시의 정점/UV 형식이 아닙니다.");
                return false;
            }

            Mesh working = UnityEngine.Object.Instantiate(source);
            working.name = source.name + " (PlanetGauge Crop)";
            working.hideFlags = HideFlags.HideAndDontSave;
            working.MarkDynamic();

            binding.Filter = filter;
            binding.OriginalMesh = source;
            binding.WorkingMesh = working;
            binding.OriginalVertices = vertices;
            binding.WorkingVertices = (Vector3[])vertices.Clone();
            binding.OriginalUvs = uvs;
            binding.WorkingUvs = (Vector2[])uvs.Clone();
            binding.LastProgress = float.NaN;
            filter.sharedMesh = working;
            return true;
        }

        private static void EnsureVisibleRenderPath(DecorationBinding binding)
        {
            if (binding == null || binding.Decoration == null)
            {
                return;
            }

            scrVisualDecoration decoration = binding.Decoration;
            scrController controller = ADOBase.controller;
            bool usesLegacyRenderPath = controller != null && controller.disableV15Features;
            binding.UsesLegacyRenderPath = usesLegacyRenderPath;
            if (!usesLegacyRenderPath)
            {
                return;
            }

            if (decoration.meshRendererObj == null
                || decoration.meshRenderer == null
                || decoration.spriteRenderer == null
                || decoration.spriteRenderer.sprite == null
                || decoration.isMask()
                || !decoration.GetVisible())
            {
                if (decoration.meshRendererObj != null)
                {
                    decoration.meshRendererObj.SetActive(false);
                }

                return;
            }

            Sprite sprite = decoration.spriteRenderer.sprite;
            Texture texture = sprite.texture;
            Material material = decoration.meshRenderer.material;
            material.mainTexture = texture;
            material.SetColor(scrDecorationManager.ShaderProperty_Color, decoration.color);
            material.SetFloat(scrDecorationManager.ShaderProperty_Opacity, decoration.opacity);
            material.SetVector(
                scrDecorationManager.ShaderProperty_Tile,
                new Vector4(decoration.repeatX, decoration.repeatY, 0f, 0f));

            decoration.meshRenderer.transform.localScale = new Vector3(
                texture.width / 100f,
                texture.height / 100f,
                1f);
            decoration.spriteRenderer.enabled = false;
            decoration.meshRendererObj.SetActive(true);
        }

        private static void ApplyCrop(
            DecorationBinding binding,
            float progress,
            bool forceApply)
        {
            if (binding == null || binding.WorkingMesh == null)
            {
                return;
            }

            progress = Mathf.Clamp01(progress);
            CalculateAxis(binding);
            bool alphaRangeChanged = RefreshAlphaRange(binding);
            if (!forceApply
                && !alphaRangeChanged
                && binding.LastGaugeType == binding.GaugeType
                && Mathf.Approximately(binding.LastProgress, progress))
            {
                return;
            }

            float range = binding.MaximumCoordinate - binding.MinimumCoordinate;
            if (range <= Mathf.Epsilon)
            {
                LogUnsupported(binding.Decoration, "이미지 메시의 게이지 축 길이가 0입니다.");
                return;
            }

            float cutoff = Mathf.Lerp(
                binding.AlphaMinimum,
                binding.AlphaMaximum,
                progress);
            for (int index = 0; index < binding.OriginalVertices.Length; index++)
            {
                Vector3 originalVertex = binding.OriginalVertices[index];
                Vector2 originalUv = binding.OriginalUvs[index];
                float coordinate = ReadCoordinate(originalVertex, binding.GaugeType);
                float normalized = Mathf.Clamp01(
                    (coordinate - binding.MinimumCoordinate) / range);

                if (normalized > cutoff)
                {
                    float croppedCoordinate = Mathf.Lerp(
                        binding.MinimumCoordinate,
                        binding.MaximumCoordinate,
                        cutoff);
                    float croppedUv = Mathf.Lerp(
                        binding.UvAtMinimum,
                        binding.UvAtMaximum,
                        cutoff);
                    WriteCoordinate(
                        ref binding.WorkingVertices[index],
                        binding.GaugeType,
                        croppedCoordinate);
                    WriteUvCoordinate(
                        ref binding.WorkingUvs[index],
                        binding.GaugeType,
                        croppedUv);
                }
                else
                {
                    binding.WorkingVertices[index] = originalVertex;
                    binding.WorkingUvs[index] = originalUv;
                }
            }

            binding.WorkingMesh.vertices = binding.WorkingVertices;
            binding.WorkingMesh.uv = binding.WorkingUvs;
            binding.WorkingMesh.RecalculateBounds();
            binding.LastGaugeType = binding.GaugeType;
            binding.LastProgress = progress;
        }

        private static bool RefreshAlphaRange(DecorationBinding binding)
        {
            float previousMinimum = binding.AlphaMinimum;
            float previousMaximum = binding.AlphaMaximum;
            int previousTextureId = binding.AlphaTextureId;

            scrVisualDecoration decoration = binding.Decoration;
            Sprite sprite = decoration == null || decoration.spriteRenderer == null
                ? null
                : decoration.spriteRenderer.sprite;
            Texture2D texture = sprite == null ? null : sprite.texture;
            if (texture == null)
            {
                binding.AlphaTextureId = 0;
                binding.AlphaMinimum = 0f;
                binding.AlphaMaximum = 1f;
                return previousTextureId != 0
                    || !Mathf.Approximately(previousMinimum, 0f)
                    || !Mathf.Approximately(previousMaximum, 1f);
            }

            int textureId = texture.GetInstanceID();
            TextureAlphaBounds bounds;
            if (!alphaBoundsCache.TryGetValue(textureId, out bounds))
            {
                bounds = ScanTextureAlphaBounds(texture, decoration);
                alphaBoundsCache[textureId] = bounds;
            }

            float textureMinimum = binding.GaugeType == PlanetGaugeSkinGaugeType.Vertical
                ? bounds.MinimumY
                : bounds.MinimumX;
            float textureMaximum = binding.GaugeType == PlanetGaugeSkinGaugeType.Vertical
                ? bounds.MaximumY
                : bounds.MaximumX;
            binding.TextureAlphaMinimum = textureMinimum;
            binding.TextureAlphaMaximum = textureMaximum;
            float uvRange = binding.UvAtMaximum - binding.UvAtMinimum;
            if (Mathf.Abs(uvRange) <= Mathf.Epsilon)
            {
                binding.AlphaMinimum = 0f;
                binding.AlphaMaximum = 1f;
            }
            else
            {
                float first = (textureMinimum - binding.UvAtMinimum) / uvRange;
                float second = (textureMaximum - binding.UvAtMinimum) / uvRange;
                binding.AlphaMinimum = Mathf.Clamp01(Mathf.Min(first, second));
                binding.AlphaMaximum = Mathf.Clamp01(Mathf.Max(first, second));
                if (binding.AlphaMaximum - binding.AlphaMinimum <= Mathf.Epsilon)
                {
                    binding.AlphaMinimum = 0f;
                    binding.AlphaMaximum = 1f;
                }
            }

            binding.AlphaTextureId = textureId;
            return previousTextureId != textureId
                || !Mathf.Approximately(previousMinimum, binding.AlphaMinimum)
                || !Mathf.Approximately(previousMaximum, binding.AlphaMaximum);
        }

        private static TextureAlphaBounds ScanTextureAlphaBounds(
            Texture2D texture,
            scrVisualDecoration decoration)
        {
            TextureAlphaBounds fallback = new TextureAlphaBounds
            {
                MinimumX = 0f,
                MaximumX = 1f,
                MinimumY = 0f,
                MaximumY = 1f
            };

            try
            {
                Color32[] pixels = TryReadOriginalPngPixels(
                    decoration,
                    out int width,
                    out int height);
                if (pixels == null)
                {
                    width = texture.width;
                    height = texture.height;
                    try
                    {
                        pixels = texture.GetPixels32();
                    }
                    catch (UnityException)
                    {
                        pixels = ReadPixelsThroughGpu(texture);
                    }
                }

                if (pixels == null || pixels.LongLength != (long)width * height)
                {
                    throw new InvalidOperationException(
                        "텍스처 픽셀 수가 이미지 크기와 일치하지 않습니다.");
                }

                int minimumX = width;
                int minimumY = height;
                int maximumX = -1;
                int maximumY = -1;
                for (int y = 0; y < height; y++)
                {
                    int row = y * width;
                    for (int x = 0; x < width; x++)
                    {
                        if (pixels[row + x].a == 0)
                        {
                            continue;
                        }

                        minimumX = Mathf.Min(minimumX, x);
                        minimumY = Mathf.Min(minimumY, y);
                        maximumX = Mathf.Max(maximumX, x);
                        maximumY = Mathf.Max(maximumY, y);
                    }
                }

                if (maximumX < minimumX || maximumY < minimumY)
                {
                    throw new InvalidOperationException("텍스처가 완전히 투명합니다.");
                }

                return new TextureAlphaBounds
                {
                    MinimumX = minimumX / (float)width,
                    MaximumX = (maximumX + 1f) / width,
                    MinimumY = minimumY / (float)height,
                    MaximumY = (maximumY + 1f) / height
                };
            }
            catch (Exception exception)
            {
                Main.LogException(
                    "[PlanetgaugeSkin] 이미지 알파 영역을 감지하지 못해 전체 범위를 사용합니다: "
                    + texture.name,
                    exception);
                return fallback;
            }
        }

        private static Color32[] TryReadOriginalPngPixels(
            scrVisualDecoration decoration,
            out int width,
            out int height)
        {
            width = 0;
            height = 0;
            if (decoration == null || decoration.sourceLevelEvent == null)
            {
                return null;
            }

            string imageName = decoration.sourceLevelEvent.GetString("decorationImage");
            string levelPath = ADOBase.levelPath;
            if (string.IsNullOrWhiteSpace(imageName)
                || imageName.StartsWith("prefab:", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(levelPath))
            {
                return null;
            }

            string levelDirectory = Path.GetDirectoryName(levelPath);
            if (string.IsNullOrWhiteSpace(levelDirectory))
            {
                return null;
            }

            string imagePath = Path.Combine(levelDirectory, imageName);
            LoadResult status;
            byte[] bytes = RDFile.ReadAllBytes(imagePath, out status);
            if (bytes == null || bytes.Length == 0 || status != LoadResult.Successful)
            {
                return null;
            }

            Texture2D readable = null;
            try
            {
                readable = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                readable.hideFlags = HideFlags.HideAndDontSave;
                if (!GaugeSkinManager.LoadPng(readable, bytes))
                {
                    return null;
                }

                width = readable.width;
                height = readable.height;
                return readable.GetPixels32();
            }
            finally
            {
                if (readable != null)
                {
                    UnityEngine.Object.Destroy(readable);
                }
            }
        }

        private static Color32[] ReadPixelsThroughGpu(Texture2D source)
        {
            RenderTexture temporary = null;
            Texture2D readable = null;
            RenderTexture previousActive = RenderTexture.active;
            try
            {
                temporary = RenderTexture.GetTemporary(
                    source.width,
                    source.height,
                    0,
                    RenderTextureFormat.ARGB32);
                Graphics.Blit(source, temporary);
                RenderTexture.active = temporary;

                readable = new Texture2D(
                    source.width,
                    source.height,
                    TextureFormat.RGBA32,
                    false);
                readable.hideFlags = HideFlags.HideAndDontSave;
                readable.ReadPixels(
                    new Rect(0f, 0f, source.width, source.height),
                    0,
                    0,
                    false);
                readable.Apply(false, false);
                return readable.GetPixels32();
            }
            finally
            {
                RenderTexture.active = previousActive;
                if (temporary != null)
                {
                    RenderTexture.ReleaseTemporary(temporary);
                }

                if (readable != null)
                {
                    UnityEngine.Object.Destroy(readable);
                }
            }
        }

        private static void CalculateAxis(DecorationBinding binding)
        {
            float minimum = float.PositiveInfinity;
            float maximum = float.NegativeInfinity;
            for (int index = 0; index < binding.OriginalVertices.Length; index++)
            {
                float coordinate = ReadCoordinate(
                    binding.OriginalVertices[index],
                    binding.GaugeType);
                minimum = Mathf.Min(minimum, coordinate);
                maximum = Mathf.Max(maximum, coordinate);
            }

            float tolerance = Mathf.Max((maximum - minimum) * 0.001f, 0.00001f);
            float minimumUvTotal = 0f;
            float maximumUvTotal = 0f;
            int minimumCount = 0;
            int maximumCount = 0;
            for (int index = 0; index < binding.OriginalVertices.Length; index++)
            {
                float coordinate = ReadCoordinate(
                    binding.OriginalVertices[index],
                    binding.GaugeType);
                float uv = ReadUvCoordinate(binding.OriginalUvs[index], binding.GaugeType);
                if (Mathf.Abs(coordinate - minimum) <= tolerance)
                {
                    minimumUvTotal += uv;
                    minimumCount++;
                }

                if (Mathf.Abs(coordinate - maximum) <= tolerance)
                {
                    maximumUvTotal += uv;
                    maximumCount++;
                }
            }

            binding.MinimumCoordinate = minimum;
            binding.MaximumCoordinate = maximum;
            binding.UvAtMinimum = minimumCount == 0 ? 0f : minimumUvTotal / minimumCount;
            binding.UvAtMaximum = maximumCount == 0 ? 1f : maximumUvTotal / maximumCount;
        }

        private static float ReadCoordinate(
            Vector3 vertex,
            PlanetGaugeSkinGaugeType gaugeType)
        {
            return gaugeType == PlanetGaugeSkinGaugeType.Vertical ? vertex.y : vertex.x;
        }

        private static void WriteCoordinate(
            ref Vector3 vertex,
            PlanetGaugeSkinGaugeType gaugeType,
            float value)
        {
            if (gaugeType == PlanetGaugeSkinGaugeType.Vertical)
            {
                vertex.y = value;
            }
            else
            {
                vertex.x = value;
            }
        }

        private static float ReadUvCoordinate(
            Vector2 uv,
            PlanetGaugeSkinGaugeType gaugeType)
        {
            return gaugeType == PlanetGaugeSkinGaugeType.Vertical ? uv.y : uv.x;
        }

        private static void WriteUvCoordinate(
            ref Vector2 uv,
            PlanetGaugeSkinGaugeType gaugeType,
            float value)
        {
            if (gaugeType == PlanetGaugeSkinGaugeType.Vertical)
            {
                uv.y = value;
            }
            else
            {
                uv.x = value;
            }
        }

        private static bool OwnsWorkingMesh(DecorationBinding binding)
        {
            return binding != null
                && binding.Filter != null
                && binding.WorkingMesh != null
                && binding.Filter.sharedMesh == binding.WorkingMesh;
        }

        private static void RestoreAndRemove(scrVisualDecoration decoration)
        {
            DecorationBinding binding;
            if (decoration != null && bindings.TryGetValue(decoration, out binding))
            {
                bindings.Remove(decoration);
                Restore(binding);
                return;
            }

            bindings.Remove(decoration);
        }

        private static void RestoreAll()
        {
            DecorationBinding[] previousBindings = bindings.Values.ToArray();
            bindings.Clear();
            for (int index = 0; index < previousBindings.Length; index++)
            {
                Restore(previousBindings[index]);
            }
        }

        private static void Restore(DecorationBinding binding)
        {
            DestroyWorkingMesh(binding, true);

            scrVisualDecoration decoration = binding == null ? null : binding.Decoration;
            if (decoration == null)
            {
                return;
            }

            if (binding.UsesLegacyRenderPath)
            {
                if (decoration.meshRendererObj != null)
                {
                    decoration.meshRendererObj.SetActive(false);
                }

                if (decoration.spriteRenderer != null)
                {
                    decoration.spriteRenderer.enabled = !decoration.isMask()
                        && decoration.GetVisible();
                }

                return;
            }

            scrController controller = ADOBase.controller;
            decoration.UpdateShader(controller != null && controller.disableV15Features);
        }

        private static void DestroyWorkingMesh(DecorationBinding binding, bool restoreOriginal)
        {
            if (binding == null)
            {
                return;
            }

            if (restoreOriginal
                && binding.Filter != null
                && binding.Filter.sharedMesh == binding.WorkingMesh)
            {
                binding.Filter.sharedMesh = binding.OriginalMesh;
            }

            if (binding.WorkingMesh != null)
            {
                UnityEngine.Object.Destroy(binding.WorkingMesh);
            }

            binding.WorkingMesh = null;
            binding.OriginalMesh = null;
            binding.OriginalVertices = null;
            binding.WorkingVertices = null;
            binding.OriginalUvs = null;
            binding.WorkingUvs = null;
            binding.LastProgress = float.NaN;
        }

        private static void LogUnsupported(scrVisualDecoration decoration, string reason)
        {
            if (decoration == null)
            {
                return;
            }

            int instanceId = decoration.GetInstanceID();
            if (unsupportedDecorationIds.Add(instanceId))
            {
                Main.Logger.Warning(
                    "[PlanetgaugeSkin] 이미지 장식을 적용하지 못했습니다: " + reason);
            }
        }

        private static string[] SplitTags(string targetTag)
        {
            if (string.IsNullOrWhiteSpace(targetTag))
            {
                return Array.Empty<string>();
            }

            return targetTag.Split(
                    new[] { ' ', '\t', '\r', '\n' },
                    StringSplitOptions.RemoveEmptyEntries)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }
    }

    [HarmonyPatch]
    internal static class PlanetGaugeDecorationManagerLateUpdatePatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(scrDecorationManager), "LateUpdate");
        }

        private static void Postfix(scrDecorationManager __instance)
        {
            if (Main.IsEnabled)
            {
                PlanetGaugeDecorationSkinRuntime.UpdateAfterDecorations(__instance);
            }
        }
    }

    [HarmonyPatch]
    internal static class PlanetGaugeVisualDecorationShaderPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(scrVisualDecoration),
                nameof(scrVisualDecoration.UpdateShader),
                new[] { typeof(bool) });
        }

        private static void Postfix(scrVisualDecoration __instance)
        {
            if (Main.IsEnabled)
            {
                PlanetGaugeDecorationSkinRuntime.ApplyAfterShaderUpdate(__instance);
            }
        }
    }
}
