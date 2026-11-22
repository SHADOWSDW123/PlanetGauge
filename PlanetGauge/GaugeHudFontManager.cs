using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;
using UnityFileDialog;

namespace PlanetGauge
{
    public enum PlanetGaugeFontMode
    {
        Default = 0,
        Adofai = 1,
        Custom = 2
    }

    /// <summary>
    /// 인게임 HUD, 디버그 HUD, 에디터 SR 라벨의 폰트 모드(기본·ADOFAI·커스텀) 전환,
    /// 내장 기본 폰트(<c>esamanru Bold.ttf</c>) 및 사용자 선택 폰트의
    /// <see cref="TMP_FontAsset"/> 생성·스케일·테두리 머티리얼·캐시·폴백·해제를 관리한다.
    /// UMM IMGUI 설정 화면의 글꼴은 변경하지 않는다.
    /// </summary>
    internal static class GaugeHudFontManager
    {
        private enum MessageKind
        {
            None,
            FileSelectionFailed,
            SelectFontFileFirst,
            FontLoadsWhenEnabled,
            UsingBundledDefaultFont,
            UsingAdofaiFont,
            CustomFontApplied,
            FontApplyFailed
        }

        internal const string BundledFontFileName = "esamanru Bold.ttf";
        internal const string BundledFontRelativePath = @"Assets\esamanru Bold.ttf";
        internal const float BundledDefaultFontScalePercent = 63.4f;
        internal const float BundledDefaultFontScale = 0.634f;
        internal const float DefaultCustomFontScalePercent = 100f;
        internal const float MinCustomFontScalePercent = 10f;
        internal const float MaxCustomFontScalePercent = 200f;

        private const int DefaultSamplingPointSize = 90;
        private const int DefaultAtlasPadding = 9;
        private const int DefaultAtlasWidth = 1024;
        private const int DefaultAtlasHeight = 1024;
        private const float FallbackOutlineWidth = 0.2f;
        private const float FallbackOutlineSoftness = 0.02f;

        private static readonly string[] SupportedFontExtensions = { "ttf", "otf", "ttc" };
        private static readonly Color DefaultOutlineColor = new Color(0f, 0f, 0f, 0.95f);
        private static readonly Dictionary<int, int> textAppliedRevisions =
            new Dictionary<int, int>();

        private static TMP_FontAsset bundledFontAsset;
        private static bool bundledMaterialConfiguredFromDefault;
        private static bool bundledLoadFailed;

        private static TMP_FontAsset customFontAsset;
        private static string loadedCustomFontPath;
        private static string autoReloadFailedCustomPath;
        private static bool customMaterialConfiguredFromDefault;
        private static float appliedCustomFontScale = -1f;

        private static int fontConfigurationRevision = 1;
        private static MessageKind lastMessageKind;
        private static string lastMessageArgument;

        internal static string LastMessage { get { return GetLastMessage(); } }
        internal static bool IsCustomFontLoaded { get { return IsFontAssetValid(customFontAsset); } }

        internal static PlanetGaugeFontMode ResolveEffectiveFontMode(PlanetGaugeSettings settings)
        {
            if (settings == null || !Enum.IsDefined(typeof(PlanetGaugeFontMode), settings.FontMode))
            {
                return PlanetGaugeFontMode.Default;
            }

            return settings.FontMode;
        }

        internal static string ResolveBundledFontPath(string modDirectory = null)
        {
            string baseDirectory = modDirectory;
            if (string.IsNullOrEmpty(baseDirectory))
            {
                baseDirectory = Main.ModDirectory;
            }

            if (string.IsNullOrEmpty(baseDirectory))
            {
                string assemblyLocation = typeof(Main).Assembly.Location;
                if (!string.IsNullOrEmpty(assemblyLocation))
                {
                    baseDirectory = Path.GetDirectoryName(assemblyLocation);
                }
            }

            if (string.IsNullOrEmpty(baseDirectory))
            {
                return string.Empty;
            }

            return Path.Combine(baseDirectory, "Assets", BundledFontFileName);
        }

        internal static bool IsSupportedFontExtension(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            try
            {
                string extension = Path.GetExtension(path);
                return string.Equals(extension, ".ttf", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(extension, ".otf", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(extension, ".ttc", StringComparison.OrdinalIgnoreCase);
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        internal static bool IsAlreadyBoldStyle(string styleName)
        {
            if (string.IsNullOrWhiteSpace(styleName))
            {
                return false;
            }

            return styleName.IndexOf("bold", StringComparison.OrdinalIgnoreCase) >= 0
                || styleName.IndexOf("black", StringComparison.OrdinalIgnoreCase) >= 0
                || styleName.IndexOf("heavy", StringComparison.OrdinalIgnoreCase) >= 0
                || styleName.IndexOf("extra", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        internal static float SanitizeCustomFontScalePercent(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return DefaultCustomFontScalePercent;
            }

            return Mathf.Clamp(value, MinCustomFontScalePercent, MaxCustomFontScalePercent);
        }

        internal static float ResolveCustomFontScale(PlanetGaugeSettings settings)
        {
            float percent = settings != null
                ? SanitizeCustomFontScalePercent(settings.CustomFontScalePercent)
                : DefaultCustomFontScalePercent;
            return percent / 100f;
        }

        internal static float ResolveActiveFontScale(PlanetGaugeSettings settings)
        {
            switch (ResolveEffectiveFontMode(settings))
            {
                case PlanetGaugeFontMode.Adofai:
                    return 1f;
                case PlanetGaugeFontMode.Custom:
                    return ResolveCustomFontScale(settings);
                case PlanetGaugeFontMode.Default:
                default:
                    return BundledDefaultFontScale;
            }
        }

        internal static string PickFont(string currentPath, string title)
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
                    LocalizedStrings.FontFileFilter,
                    SupportedFontExtensions,
                    title);
                return string.IsNullOrWhiteSpace(selected)
                    ? currentPath
                    : Path.GetFullPath(selected);
            }
            catch (Exception exception)
            {
                SetLastMessage(MessageKind.FileSelectionFailed, exception.Message);
                Main.LogException("커스텀 폰트 파일 선택에 실패했습니다.", exception);
                return currentPath;
            }
        }

        internal static bool ApplyMode(
            PlanetGaugeSettings settings,
            bool logFailure,
            bool updateStatusMessage = true)
        {
            PlanetGaugeFontMode mode = ResolveEffectiveFontMode(settings);
            if (mode == PlanetGaugeFontMode.Adofai)
            {
                IncrementFontConfigurationRevision();
                if (updateStatusMessage)
                {
                    SetLastMessage(MessageKind.UsingAdofaiFont);
                }

                return true;
            }

            if (mode == PlanetGaugeFontMode.Default)
            {
                if (!Main.IsEnabled)
                {
                    if (updateStatusMessage)
                    {
                        SetLastMessage(MessageKind.FontLoadsWhenEnabled);
                    }

                    return true;
                }

                bundledLoadFailed = false;
                string errorMessage;
                if (EnsureBundledFontAsset(logFailure, out errorMessage) != null)
                {
                    IncrementFontConfigurationRevision();
                    if (updateStatusMessage)
                    {
                        SetLastMessage(MessageKind.UsingBundledDefaultFont);
                    }

                    return true;
                }

                if (updateStatusMessage)
                {
                    SetLastMessage(MessageKind.FontApplyFailed, errorMessage);
                }

                return false;
            }

            if (settings == null || string.IsNullOrWhiteSpace(settings.CustomFontFilePath))
            {
                ClearCustomFontAsset();
                IncrementFontConfigurationRevision();
                if (updateStatusMessage)
                {
                    SetLastMessage(MessageKind.SelectFontFileFirst);
                }

                return false;
            }

            TMP_FontAsset created = null;
            try
            {
                string fullPath = ValidateFontPath(settings.CustomFontFilePath);
                if (!Main.IsEnabled)
                {
                    if (updateStatusMessage)
                    {
                        SetLastMessage(MessageKind.FontLoadsWhenEnabled);
                    }

                    return true;
                }

                float scale = ResolveCustomFontScale(settings);
                bool configuredFromDefault;
                created = CreateFontAssetFromFile(
                    fullPath,
                    "PlanetGauge_CustomFont_",
                    scale,
                    out configuredFromDefault);

                TMP_FontAsset previous = customFontAsset;
                customFontAsset = created;
                created = null;
                loadedCustomFontPath = settings.CustomFontFilePath;
                autoReloadFailedCustomPath = null;
                customMaterialConfiguredFromDefault = configuredFromDefault;
                appliedCustomFontScale = scale;
                IncrementFontConfigurationRevision();
                if (updateStatusMessage)
                {
                    SetLastMessage(MessageKind.CustomFontApplied);
                }

                if (previous != null)
                {
                    DestroyFontAsset(previous);
                }

                return true;
            }
            catch (Exception exception)
            {
                if (created != null)
                {
                    DestroyFontAsset(created);
                }

                if (updateStatusMessage)
                {
                    SetLastMessage(MessageKind.FontApplyFailed, exception.Message);
                }

                if (logFailure)
                {
                    Main.LogException("커스텀 폰트를 적용하지 못했습니다.", exception);
                }

                return false;
            }
        }

        internal static void LoadEnabledSettings(PlanetGaugeSettings settings)
        {
            PlanetGaugeFontMode mode = ResolveEffectiveFontMode(settings);
            if (mode == PlanetGaugeFontMode.Default)
            {
                ApplyMode(settings, true, false);
                return;
            }

            if (mode == PlanetGaugeFontMode.Custom
                && settings != null
                && !string.IsNullOrWhiteSpace(settings.CustomFontFilePath))
            {
                ApplyMode(settings, true, true);
            }
        }

        internal static TMP_FontAsset ResolveActiveFontAsset()
        {
            TMP_FontAsset defaultFont = TMP_Settings.defaultFontAsset;
            PlanetGaugeSettings settings = Main.Settings;
            PlanetGaugeFontMode mode = ResolveEffectiveFontMode(settings);

            if (mode == PlanetGaugeFontMode.Adofai)
            {
                return defaultFont;
            }

            if (mode == PlanetGaugeFontMode.Custom)
            {
                if (settings != null && !string.IsNullOrWhiteSpace(settings.CustomFontFilePath))
                {
                    if (IsFontAssetValid(customFontAsset)
                        && string.Equals(
                            loadedCustomFontPath,
                            settings.CustomFontFilePath,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        EnsureFallbackAndMaterial(
                            customFontAsset,
                            defaultFont,
                            ref customMaterialConfiguredFromDefault);
                        SyncCustomFontScale(customFontAsset, settings);
                        return customFontAsset;
                    }

                    if (customFontAsset != null && !IsFontAssetValid(customFontAsset))
                    {
                        ClearCustomFontAsset();
                        IncrementFontConfigurationRevision();
                    }

                    if (!string.Equals(
                        autoReloadFailedCustomPath,
                        settings.CustomFontFilePath,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        if (!ApplyMode(settings, true, true))
                        {
                            autoReloadFailedCustomPath = settings.CustomFontFilePath;
                        }
                    }

                    if (IsFontAssetValid(customFontAsset))
                    {
                        return customFontAsset;
                    }
                }

                return GetOrCreateBundledFontAsset(defaultFont) ?? defaultFont;
            }

            return GetOrCreateBundledFontAsset(defaultFont) ?? defaultFont;
        }

        internal static void ApplyFont(TMP_Text text)
        {
            if (text == null)
            {
                return;
            }

            TMP_FontAsset targetFont = ResolveActiveFontAsset();
            if (targetFont == null)
            {
                return;
            }

            bool isModManagedFont = (bundledFontAsset != null && targetFont == bundledFontAsset)
                || (customFontAsset != null && targetFont == customFontAsset);
            int instanceId = text.GetInstanceID();
            int appliedRevision;
            bool revisionMismatch = !textAppliedRevisions.TryGetValue(instanceId, out appliedRevision)
                || appliedRevision != fontConfigurationRevision;

            if (text.font != targetFont || revisionMismatch)
            {
                text.font = targetFont;
                if (targetFont.material != null)
                {
                    text.fontSharedMaterial = targetFont.material;
                }

                text.extraPadding = isModManagedFont;
                text.UpdateMeshPadding();
                text.havePropertiesChanged = true;
                text.SetAllDirty();
                textAppliedRevisions[instanceId] = fontConfigurationRevision;
            }
        }

        internal static void Dispose()
        {
            ClearBundledFontAsset();
            ClearCustomFontAsset();
            textAppliedRevisions.Clear();
            IncrementFontConfigurationRevision();
        }

        private static TMP_FontAsset GetOrCreateBundledFontAsset(TMP_FontAsset defaultFont)
        {
            if (IsFontAssetValid(bundledFontAsset))
            {
                EnsureFallbackAndMaterial(
                    bundledFontAsset,
                    defaultFont,
                    ref bundledMaterialConfiguredFromDefault);
                return bundledFontAsset;
            }

            if (bundledFontAsset != null)
            {
                ClearBundledFontAsset();
                IncrementFontConfigurationRevision();
            }

            if (bundledLoadFailed)
            {
                return null;
            }

            string unusedError;
            return EnsureBundledFontAsset(true, out unusedError);
        }

        private static TMP_FontAsset EnsureBundledFontAsset(bool logFailure, out string errorMessage)
        {
            errorMessage = null;
            if (IsFontAssetValid(bundledFontAsset))
            {
                EnsureFallbackAndMaterial(
                    bundledFontAsset,
                    TMP_Settings.defaultFontAsset,
                    ref bundledMaterialConfiguredFromDefault);
                return bundledFontAsset;
            }

            if (bundledFontAsset != null)
            {
                ClearBundledFontAsset();
            }

            TMP_FontAsset created = null;
            try
            {
                string bundledPath = ResolveBundledFontPath();
                string fullPath = ValidateFontPath(bundledPath);
                bool configuredFromDefault;
                created = CreateFontAssetFromFile(
                    fullPath,
                    "PlanetGauge_DefaultFont_",
                    BundledDefaultFontScale,
                    out configuredFromDefault);

                bundledFontAsset = created;
                created = null;
                bundledMaterialConfiguredFromDefault = configuredFromDefault;
                bundledLoadFailed = false;
                return bundledFontAsset;
            }
            catch (Exception exception)
            {
                if (created != null)
                {
                    DestroyFontAsset(created);
                }

                bundledLoadFailed = true;
                errorMessage = exception.Message;
                if (logFailure)
                {
                    Main.LogException("기본 내장 폰트(esamanru Bold.ttf) 로드에 실패했습니다.", exception);
                }

                return null;
            }
        }

        private static string ValidateFontPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new FileNotFoundException(
                    LocalizedStrings.Format(
                        LocalizedStrings.FileNotFound,
                        LocalizedStrings.CustomFontAsset));
            }

            string fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException(
                    LocalizedStrings.Format(
                        LocalizedStrings.FileNotFound,
                        LocalizedStrings.CustomFontAsset),
                    fullPath);
            }

            if (!IsSupportedFontExtension(fullPath))
            {
                throw new InvalidDataException(LocalizedStrings.FontFileInvalidExtension);
            }

            return fullPath;
        }

        private static TMP_FontAsset CreateFontAssetFromFile(
            string fullPath,
            string assetNamePrefix,
            float scale,
            out bool configuredFromDefault)
        {
            TMP_FontAsset created = TMP_FontAsset.CreateFontAsset(
                fullPath,
                0,
                DefaultSamplingPointSize,
                DefaultAtlasPadding,
                GlyphRenderMode.SDFAA,
                DefaultAtlasWidth,
                DefaultAtlasHeight);
            if (!IsFontAssetValid(created))
            {
                if (created != null)
                {
                    DestroyFontAsset(created);
                }

                throw new InvalidDataException(LocalizedStrings.FontAssetCreationFailed);
            }

            created.name = assetNamePrefix + Path.GetFileNameWithoutExtension(fullPath);
            if (IsAlreadyBoldStyle(created.faceInfo.styleName))
            {
                // 이미 굵은 웨이트를 가진 서체에는 TMP 합성 볼드가 중복 적용되지 않도록 한다.
                created.boldStyle = 0f;
                created.boldSpacing = 0f;
            }

            FaceInfo faceInfo = created.faceInfo;
            faceInfo.scale = scale;
            created.faceInfo = faceInfo;

            configuredFromDefault = ConfigureCustomFontMaterial(
                created,
                TMP_Settings.defaultFontAsset);
            PreserveUnityAsset(created);
            PreserveUnityAsset(created.material);
            Texture2D[] atlasTextures = created.atlasTextures;
            if (atlasTextures != null)
            {
                for (int i = 0; i < atlasTextures.Length; i++)
                {
                    PreserveUnityAsset(atlasTextures[i]);
                }
            }

            EnsureFallbackFontConfigured(created);
            return created;
        }

        private static void EnsureFallbackAndMaterial(
            TMP_FontAsset fontAsset,
            TMP_FontAsset defaultFont,
            ref bool configuredFromDefault)
        {
            EnsureFallbackFontConfigured(fontAsset);
            if (!configuredFromDefault && defaultFont != null)
            {
                configuredFromDefault = ConfigureCustomFontMaterial(fontAsset, defaultFont);
            }
        }

        private static void ClearBundledFontAsset()
        {
            TMP_FontAsset previous = bundledFontAsset;
            bundledFontAsset = null;
            bundledMaterialConfiguredFromDefault = false;
            bundledLoadFailed = false;
            if (previous != null)
            {
                DestroyFontAsset(previous);
            }
        }

        private static void ClearCustomFontAsset()
        {
            TMP_FontAsset previous = customFontAsset;
            customFontAsset = null;
            loadedCustomFontPath = null;
            autoReloadFailedCustomPath = null;
            customMaterialConfiguredFromDefault = false;
            appliedCustomFontScale = -1f;
            if (previous != null)
            {
                DestroyFontAsset(previous);
            }
        }

        private static void DestroyFontAsset(TMP_FontAsset fontAsset)
        {
            if (fontAsset == null)
            {
                return;
            }

            Texture2D[] atlasTextures = fontAsset.atlasTextures;
            Material material = fontAsset.material;
            UnityEngine.Object.Destroy(fontAsset);
            if (material != null)
            {
                UnityEngine.Object.Destroy(material);
            }

            if (atlasTextures != null)
            {
                for (int i = 0; i < atlasTextures.Length; i++)
                {
                    if (atlasTextures[i] != null)
                    {
                        UnityEngine.Object.Destroy(atlasTextures[i]);
                    }
                }
            }
        }

        private static void SyncCustomFontScale(TMP_FontAsset fontAsset, PlanetGaugeSettings settings)
        {
            if (fontAsset == null)
            {
                return;
            }

            float targetScale = ResolveCustomFontScale(settings);
            if (Mathf.Abs(appliedCustomFontScale - targetScale) <= 0.0001f)
            {
                return;
            }

            FaceInfo faceInfo = fontAsset.faceInfo;
            faceInfo.scale = targetScale;
            fontAsset.faceInfo = faceInfo;
            appliedCustomFontScale = targetScale;
            IncrementFontConfigurationRevision();
            TMPro_EventManager.ON_FONT_PROPERTY_CHANGED(true, fontAsset);
        }

        private static bool ConfigureCustomFontMaterial(
            TMP_FontAsset fontAsset,
            TMP_FontAsset referenceFontAsset)
        {
            if (fontAsset == null || fontAsset.material == null)
            {
                return false;
            }

            ShaderUtilities.GetShaderPropertyIDs();
            Material material = fontAsset.material;
            Material referenceMaterial = referenceFontAsset != null
                ? referenceFontAsset.material
                : null;

            bool configuredFromDefault = false;
            float outlineWidth = FallbackOutlineWidth;
            Color outlineColor = DefaultOutlineColor;
            float outlineSoftness = FallbackOutlineSoftness;
            float faceDilate = 0f;

            if (referenceMaterial != null)
            {
                configuredFromDefault = true;
                if (referenceMaterial.shader != null)
                {
                    material.shader = referenceMaterial.shader;
                }

                string[] keywords = referenceMaterial.shaderKeywords;
                if (keywords != null && keywords.Length > 0)
                {
                    material.shaderKeywords = keywords;
                }

                if (referenceMaterial.HasProperty(ShaderUtilities.ID_FaceDilate))
                {
                    faceDilate = referenceMaterial.GetFloat(ShaderUtilities.ID_FaceDilate);
                }

                if (referenceMaterial.HasProperty(ShaderUtilities.ID_OutlineWidth))
                {
                    float refOutlineWidth = referenceMaterial.GetFloat(ShaderUtilities.ID_OutlineWidth);
                    if (refOutlineWidth > 0.001f)
                    {
                        outlineWidth = refOutlineWidth;
                        if (referenceMaterial.HasProperty(ShaderUtilities.ID_OutlineColor))
                        {
                            outlineColor = referenceMaterial.GetColor(ShaderUtilities.ID_OutlineColor);
                        }

                        if (referenceMaterial.HasProperty(ShaderUtilities.ID_OutlineSoftness))
                        {
                            outlineSoftness = referenceMaterial.GetFloat(ShaderUtilities.ID_OutlineSoftness);
                        }
                    }
                }

                if (referenceMaterial.IsKeywordEnabled(ShaderUtilities.Keyword_Underlay))
                {
                    material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
                    if (referenceMaterial.HasProperty(ShaderUtilities.ID_UnderlayColor))
                    {
                        material.SetColor(
                            ShaderUtilities.ID_UnderlayColor,
                            referenceMaterial.GetColor(ShaderUtilities.ID_UnderlayColor));
                    }

                    if (referenceMaterial.HasProperty(ShaderUtilities.ID_UnderlayOffsetX))
                    {
                        material.SetFloat(
                            ShaderUtilities.ID_UnderlayOffsetX,
                            referenceMaterial.GetFloat(ShaderUtilities.ID_UnderlayOffsetX));
                    }

                    if (referenceMaterial.HasProperty(ShaderUtilities.ID_UnderlayOffsetY))
                    {
                        material.SetFloat(
                            ShaderUtilities.ID_UnderlayOffsetY,
                            referenceMaterial.GetFloat(ShaderUtilities.ID_UnderlayOffsetY));
                    }

                    if (referenceMaterial.HasProperty(ShaderUtilities.ID_UnderlayDilate))
                    {
                        material.SetFloat(
                            ShaderUtilities.ID_UnderlayDilate,
                            referenceMaterial.GetFloat(ShaderUtilities.ID_UnderlayDilate));
                    }

                    if (referenceMaterial.HasProperty(ShaderUtilities.ID_UnderlaySoftness))
                    {
                        material.SetFloat(
                            ShaderUtilities.ID_UnderlaySoftness,
                            referenceMaterial.GetFloat(ShaderUtilities.ID_UnderlaySoftness));
                    }
                }
            }

            material.SetTexture(ShaderUtilities.ID_MainTex, fontAsset.atlasTexture);
            material.SetFloat(ShaderUtilities.ID_TextureWidth, fontAsset.atlasWidth);
            material.SetFloat(ShaderUtilities.ID_TextureHeight, fontAsset.atlasHeight);
            material.SetFloat(ShaderUtilities.ID_GradientScale, fontAsset.atlasPadding + 1);
            material.SetFloat(ShaderUtilities.ID_WeightNormal, fontAsset.normalStyle);
            material.SetFloat(ShaderUtilities.ID_WeightBold, fontAsset.boldStyle);

            material.EnableKeyword(ShaderUtilities.Keyword_Outline);
            if (material.HasProperty(ShaderUtilities.ID_FaceDilate))
            {
                material.SetFloat(ShaderUtilities.ID_FaceDilate, faceDilate);
            }

            if (material.HasProperty(ShaderUtilities.ID_OutlineColor))
            {
                material.SetColor(ShaderUtilities.ID_OutlineColor, outlineColor);
            }

            if (material.HasProperty(ShaderUtilities.ID_OutlineWidth))
            {
                material.SetFloat(ShaderUtilities.ID_OutlineWidth, outlineWidth);
            }

            if (material.HasProperty(ShaderUtilities.ID_OutlineSoftness))
            {
                material.SetFloat(ShaderUtilities.ID_OutlineSoftness, outlineSoftness);
            }

            ShaderUtilities.UpdateShaderRatios(material);
            IncrementFontConfigurationRevision();
            return configuredFromDefault;
        }

        private static bool IsFontAssetValid(TMP_FontAsset fontAsset)
        {
            return fontAsset != null
                && fontAsset.material != null
                && fontAsset.atlasTexture != null;
        }

        private static void EnsureFallbackFontConfigured(TMP_FontAsset fontAsset)
        {
            if (fontAsset == null)
            {
                return;
            }

            TMP_FontAsset defaultFont = TMP_Settings.defaultFontAsset;
            if (defaultFont == null || defaultFont == fontAsset)
            {
                return;
            }

            if (fontAsset.fallbackFontAssetTable == null)
            {
                fontAsset.fallbackFontAssetTable = new List<TMP_FontAsset>();
            }

            if (!fontAsset.fallbackFontAssetTable.Contains(defaultFont))
            {
                fontAsset.fallbackFontAssetTable.Add(defaultFont);
            }
        }

        private static void PreserveUnityAsset(UnityEngine.Object asset)
        {
            if (asset == null)
            {
                return;
            }

            asset.hideFlags |= HideFlags.DontUnloadUnusedAsset;
            UnityEngine.Object.DontDestroyOnLoad(asset);
        }

        private static void IncrementFontConfigurationRevision()
        {
            unchecked
            {
                fontConfigurationRevision++;
            }
        }

        private static void SetLastMessage(MessageKind kind, string argument = null)
        {
            lastMessageKind = kind;
            lastMessageArgument = argument;
        }

        private static string GetLastMessage()
        {
            switch (lastMessageKind)
            {
                case MessageKind.FileSelectionFailed:
                    return LocalizedStrings.Format(
                        LocalizedStrings.FileSelectionFailed,
                        lastMessageArgument);
                case MessageKind.SelectFontFileFirst:
                    return LocalizedStrings.SelectFontFileFirst;
                case MessageKind.FontLoadsWhenEnabled:
                    return LocalizedStrings.FontLoadsWhenEnabled;
                case MessageKind.UsingBundledDefaultFont:
                    return LocalizedStrings.UsingBundledDefaultFont;
                case MessageKind.UsingAdofaiFont:
                    return LocalizedStrings.UsingAdofaiFont;
                case MessageKind.CustomFontApplied:
                    return LocalizedStrings.CustomFontApplied;
                case MessageKind.FontApplyFailed:
                    return LocalizedStrings.Format(
                        LocalizedStrings.FontApplyFailed,
                        lastMessageArgument);
                default:
                    return null;
            }
        }
    }
}
