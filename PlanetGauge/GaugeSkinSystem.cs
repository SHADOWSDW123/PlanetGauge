using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityFileDialog;

namespace PlanetGauge
{
    public enum GaugeSkinFillDirection
    {
        Horizontal = 0,
        Vertical = 1
    }

    internal struct GaugeSkinPixelBounds
    {
        internal GaugeSkinPixelBounds(int minX, int minY, int maxX, int maxY)
        {
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
        }

        internal int MinX { get; }
        internal int MinY { get; }
        internal int MaxX { get; }
        internal int MaxY { get; }
        internal int Width { get { return MaxX - MinX + 1; } }
        internal int Height { get { return MaxY - MinY + 1; } }

        public override string ToString()
        {
            return "X=" + MinX + ".." + MaxX
                + " Y=" + MinY + ".." + MaxY;
        }
    }

    internal struct GaugeSkinRect
    {
        internal GaugeSkinRect(float left, float bottom, float right, float top)
        {
            Left = left;
            Bottom = bottom;
            Right = right;
            Top = top;
        }

        internal float Left { get; }
        internal float Bottom { get; }
        internal float Right { get; }
        internal float Top { get; }
        internal float Width { get { return Right - Left; } }
        internal float Height { get { return Top - Bottom; } }
        internal Vector2 Center { get { return new Vector2((Left + Right) * 0.5f, (Bottom + Top) * 0.5f); } }

        internal static GaugeSkinRect Union(GaugeSkinRect left, GaugeSkinRect right)
        {
            return new GaugeSkinRect(
                Mathf.Min(left.Left, right.Left),
                Mathf.Min(left.Bottom, right.Bottom),
                Mathf.Max(left.Right, right.Right),
                Mathf.Max(left.Top, right.Top));
        }

        internal GaugeSkinRect Offset(Vector2 offset)
        {
            return new GaugeSkinRect(
                Left + offset.x,
                Bottom + offset.y,
                Right + offset.x,
                Top + offset.y);
        }
    }

    internal sealed class GaugeSkinTexture : IDisposable
    {
        internal GaugeSkinTexture(
            Texture2D texture,
            GaugeSkinPixelBounds alphaBounds,
            string sourcePath,
            long sourceBytes)
        {
            Texture = texture;
            AlphaBounds = alphaBounds;
            SourcePath = sourcePath;
            SourceBytes = sourceBytes;
        }

        internal Texture2D Texture { get; private set; }
        internal GaugeSkinPixelBounds AlphaBounds { get; }
        internal string SourcePath { get; }
        internal long SourceBytes { get; }
        internal int Width { get { return Texture == null ? 0 : Texture.width; } }
        internal int Height { get { return Texture == null ? 0 : Texture.height; } }

        internal GaugeSkinRect GetCenteredAlphaRect(Vector2 offset)
        {
            return new GaugeSkinRect(
                AlphaBounds.MinX - Width * 0.5f + offset.x,
                AlphaBounds.MinY - Height * 0.5f + offset.y,
                AlphaBounds.MaxX + 1f - Width * 0.5f + offset.x,
                AlphaBounds.MaxY + 1f - Height * 0.5f + offset.y);
        }

        public void Dispose()
        {
            if (Texture != null)
            {
                UnityEngine.Object.Destroy(Texture);
                Texture = null;
            }
        }
    }

    internal sealed class GaugeSkinAsset : IDisposable
    {
        internal GaugeSkinAsset(
            GaugeSkinTexture health,
            GaugeSkinTexture frame,
            GaugeSkinFillDirection direction)
        {
            Health = health;
            Frame = frame;
            Direction = direction;
            HealthRect = health.GetCenteredAlphaRect(Vector2.zero);
            FrameRect = frame == null
                ? default(GaugeSkinRect)
                : frame.GetCenteredAlphaRect(Vector2.zero);
            ContentRect = frame == null
                ? HealthRect
                : GaugeSkinRect.Union(HealthRect, FrameRect);
        }

        internal GaugeSkinTexture Health { get; }
        internal GaugeSkinTexture Frame { get; }
        internal GaugeSkinFillDirection Direction { get; }
        internal GaugeSkinRect HealthRect { get; }
        internal GaugeSkinRect FrameRect { get; }
        internal GaugeSkinRect ContentRect { get; }

        public void Dispose()
        {
            Health.Dispose();
            if (Frame != null)
            {
                Frame.Dispose();
            }
        }
    }

    internal static class GaugeSkinManager
    {
        private const long LargeFileBytes = 32L * 1024L * 1024L;
        private const int LargeDimension = 4096;
        private const long LargeDecodedBytes = 64L * 1024L * 1024L;

        private static MethodInfo loadImageMethod;
        private static GaugeSkinAsset current;
        private static int revision;

        internal static GaugeSkinAsset Current { get { return current; } }
        internal static int Revision { get { return revision; } }
        internal static string LastMessage { get; private set; }
        internal static string LastWarning { get; private set; }

        internal static string PickPng(string currentPath, string title)
        {
            try
            {
                string directory = Main.ModDirectory;
                if (!string.IsNullOrWhiteSpace(currentPath))
                {
                    string candidate = Path.GetDirectoryName(currentPath);
                    if (!string.IsNullOrWhiteSpace(candidate) && Directory.Exists(candidate))
                    {
                        directory = candidate;
                    }
                }

                string selected = FileBrowser.PickFile(
                    directory,
                    "PNG Image",
                    new[] { "png" },
                    title);
                return string.IsNullOrWhiteSpace(selected)
                    ? currentPath
                    : Path.GetFullPath(selected);
            }
            catch (Exception exception)
            {
                LastMessage = "파일 선택 실패: " + exception.Message;
                Main.LogException("게이지 스킨 PNG 파일 선택에 실패했습니다.", exception);
                return currentPath;
            }
        }

        internal static bool TryApply(PlanetGaugeSettings settings, bool logFailure)
        {
            if (settings == null || string.IsNullOrWhiteSpace(settings.HealthSkinImagePath))
            {
                LastMessage = "체력 PNG를 먼저 선택하세요.";
                LastWarning = null;
                return false;
            }
            if (!Main.IsEnabled)
            {
                settings.CustomGaugeSkinEnabled = true;
                LastMessage = "모드를 활성화하면 선택한 스킨을 로드합니다.";
                LastWarning = null;
                return true;
            }

            GaugeSkinTexture health = null;
            GaugeSkinTexture frame = null;
            try
            {
                List<string> warnings = new List<string>();
                health = LoadTexture(settings.HealthSkinImagePath, "체력 PNG", warnings);
                if (!string.IsNullOrWhiteSpace(settings.FrameSkinImagePath))
                {
                    frame = LoadTexture(settings.FrameSkinImagePath, "바 PNG", warnings);
                }

                GaugeSkinAsset next = new GaugeSkinAsset(
                    health,
                    frame,
                    settings.SkinFillDirection);
                health = null;
                frame = null;

                GaugeSkinAsset previous = current;
                current = next;
                revision++;
                settings.CustomGaugeSkinEnabled = true;
                LastMessage = "커스텀 게이지 스킨을 적용했습니다.";
                LastWarning = warnings.Count == 0 ? null : string.Join("\n", warnings.ToArray());
                if (previous != null)
                {
                    previous.Dispose();
                }

                return true;
            }
            catch (Exception exception)
            {
                if (health != null)
                {
                    health.Dispose();
                }
                if (frame != null)
                {
                    frame.Dispose();
                }

                LastMessage = "스킨 적용 실패: " + exception.Message;
                LastWarning = null;
                if (logFailure)
                {
                    Main.LogException("커스텀 게이지 스킨을 적용하지 못했습니다.", exception);
                }
                return false;
            }
        }

        internal static void LoadEnabledSettings(PlanetGaugeSettings settings)
        {
            if (settings != null && settings.CustomGaugeSkinEnabled)
            {
                TryApply(settings, true);
            }
        }

        internal static void ResetToDefault(PlanetGaugeSettings settings)
        {
            GaugeSkinAsset previous = current;
            current = null;
            revision++;
            if (settings != null)
            {
                settings.CustomGaugeSkinEnabled = false;
            }
            LastMessage = "기본 게이지 스킨을 사용합니다.";
            LastWarning = null;
            if (previous != null)
            {
                previous.Dispose();
            }
        }

        internal static void Dispose()
        {
            GaugeSkinAsset previous = current;
            current = null;
            revision++;
            if (previous != null)
            {
                previous.Dispose();
            }
        }

        internal static string DescribeCurrent()
        {
            GaugeSkinAsset skin = current;
            if (skin == null)
            {
                return "Default";
            }

            return "Custom " + skin.Direction
                + " Health=" + skin.Health.Width + "x" + skin.Health.Height
                + " Bounds=" + skin.Health.AlphaBounds
                + (skin.Frame == null
                    ? " Frame=None"
                    : " Frame=" + skin.Frame.Width + "x" + skin.Frame.Height
                        + " Bounds=" + skin.Frame.AlphaBounds);
        }

        private static GaugeSkinTexture LoadTexture(
            string path,
            string label,
            List<string> warnings)
        {
            string fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException(label + " 파일을 찾을 수 없습니다.", fullPath);
            }
            if (!string.Equals(Path.GetExtension(fullPath), ".png", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(label + "는 PNG 파일이어야 합니다.");
            }

            byte[] bytes = File.ReadAllBytes(fullPath);
            int headerWidth;
            int headerHeight;
            ReadPngHeader(bytes, out headerWidth, out headerHeight);
            long decodedBytes = (long)headerWidth * headerHeight * 4L;
            if (headerWidth > LargeDimension
                || headerHeight > LargeDimension
                || bytes.LongLength > LargeFileBytes
                || decodedBytes > LargeDecodedBytes)
            {
                warnings.Add(
                    label + "가 매우 큽니다 ("
                    + headerWidth + "x" + headerHeight
                    + ", 파일 " + FormatMiB(bytes.LongLength)
                    + " MiB, RGBA " + FormatMiB(decodedBytes)
                    + " MiB). 메모리 사용량이 크게 증가할 수 있습니다.");
            }

            Texture2D texture = null;
            try
            {
                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                texture.name = "PlanetGauge.Skin." + label;
                if (!LoadPng(texture, bytes))
                {
                    throw new InvalidDataException(label + "를 Unity Texture2D로 변환하지 못했습니다.");
                }
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.filterMode = FilterMode.Bilinear;

                Color32[] pixels = texture.GetPixels32();
                GaugeSkinPixelBounds bounds = FindAlphaBounds(
                    pixels,
                    texture.width,
                    texture.height,
                    label);
                texture.Apply(false, true);
                GaugeSkinTexture result = new GaugeSkinTexture(
                    texture,
                    bounds,
                    fullPath,
                    bytes.LongLength);
                texture = null;
                return result;
            }
            finally
            {
                if (texture != null)
                {
                    UnityEngine.Object.Destroy(texture);
                }
            }
        }

        private static GaugeSkinPixelBounds FindAlphaBounds(
            Color32[] pixels,
            int width,
            int height,
            string label)
        {
            int minX = width;
            int minY = height;
            int maxX = -1;
            int maxY = -1;
            for (int y = 0; y < height; y++)
            {
                int row = y * width;
                for (int x = 0; x < width; x++)
                {
                    if (pixels[row + x].a == 0)
                    {
                        continue;
                    }

                    minX = Mathf.Min(minX, x);
                    minY = Mathf.Min(minY, y);
                    maxX = Mathf.Max(maxX, x);
                    maxY = Mathf.Max(maxY, y);
                }
            }

            if (maxX < minX || maxY < minY)
            {
                throw new InvalidDataException(label + "가 완전히 투명합니다.");
            }

            return new GaugeSkinPixelBounds(minX, minY, maxX, maxY);
        }

        internal static bool LoadPng(Texture2D texture, byte[] bytes)
        {
            if (loadImageMethod == null)
            {
                Type imageConversionType = Type.GetType(
                    "UnityEngine.ImageConversion, UnityEngine.ImageConversionModule",
                    false);
                loadImageMethod = imageConversionType == null
                    ? null
                    : imageConversionType.GetMethod(
                        "LoadImage",
                        BindingFlags.Public | BindingFlags.Static,
                        null,
                        new[] { typeof(Texture2D), typeof(byte[]), typeof(bool) },
                        null);
            }
            if (loadImageMethod == null || loadImageMethod.ReturnType != typeof(bool))
            {
                throw new MissingMethodException(
                    "UnityEngine.ImageConversion.LoadImage(Texture2D, byte[], bool)을 찾을 수 없습니다.");
            }

            return (bool)loadImageMethod.Invoke(null, new object[] { texture, bytes, false });
        }

        private static void ReadPngHeader(byte[] bytes, out int width, out int height)
        {
            byte[] signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
            if (bytes == null || bytes.Length < 24)
            {
                throw new InvalidDataException("PNG 헤더가 너무 짧습니다.");
            }
            for (int index = 0; index < signature.Length; index++)
            {
                if (bytes[index] != signature[index])
                {
                    throw new InvalidDataException("PNG 시그니처가 올바르지 않습니다.");
                }
            }
            if (bytes[12] != (byte)'I'
                || bytes[13] != (byte)'H'
                || bytes[14] != (byte)'D'
                || bytes[15] != (byte)'R')
            {
                throw new InvalidDataException("PNG IHDR 청크를 찾을 수 없습니다.");
            }

            uint rawWidth = ReadUInt32BigEndian(bytes, 16);
            uint rawHeight = ReadUInt32BigEndian(bytes, 20);
            if (rawWidth == 0 || rawHeight == 0 || rawWidth > int.MaxValue || rawHeight > int.MaxValue)
            {
                throw new InvalidDataException("PNG 크기가 유효하지 않습니다.");
            }
            width = (int)rawWidth;
            height = (int)rawHeight;
        }

        private static uint ReadUInt32BigEndian(byte[] bytes, int offset)
        {
            return ((uint)bytes[offset] << 24)
                | ((uint)bytes[offset + 1] << 16)
                | ((uint)bytes[offset + 2] << 8)
                | bytes[offset + 3];
        }

        private static string FormatMiB(long bytes)
        {
            return (bytes / (1024d * 1024d)).ToString("0.##", CultureInfo.InvariantCulture);
        }
    }
}
