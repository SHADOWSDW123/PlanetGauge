using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ADOFAI;
using ADOFAI.LevelEditor.Controls;
using HarmonyLib;
using UnityEngine;

namespace PlanetGauge
{
    internal static class PlanetGaugeLevelEventRegistry
    {
        internal const int NumericEventId = 0x5047;
        internal const string EventName = "SetPlanetGauge";

        internal const string AttributeModeKey = "attributeMode";
        internal const string AttributeEnabledKey = "attributeEnabled";
        internal const string DisableOtherAttributesKey = "disableOtherAttributes";
        internal const string MultiplierPercentKey = "multiplierPercent";
        internal const string RecoveryAmountPercentKey = "recoveryAmountPercent";
        internal const string WarningOffsetAngleKey = "warningOffsetAngle";
        internal const string WarningPulseBeatsKey = "warningPulseBeats";
        internal const string FailureProtectionKey = "failureProtection";
        internal const string RecoveryCapEnabledKey = "recoveryCapEnabled";
        internal const string RecoveryCapPercentKey = "recoveryCapPercent";
        internal const string ForceRecoveryCapKey = "forceRecoveryCap";
        internal const string AutoTileRecoveryKey = "autoTileRecovery";
        internal const string HideGaugeBarKey = "hideGaugeBar";
        internal const string HideGaugeValueKey = "hideGaugeValue";
        internal const string HideAttributeTextKey = "hideAttributeText";
        internal const string HideRateTokenKey = "hideRateToken";
        internal const string HideForceRecoveryVisualsKey = "hideForceRecoveryVisuals";
        internal const string MultiplierReuseNoteKey = "multiplierReuseNote";
        internal const string MultiplierReuseLocalizationKey = "planetGauge.multiplierReuseNote";
        internal const string RecoveryAmountNoteKey = "recoveryAmountNote";
        internal const string RecoveryAmountLocalizationKey = "planetGauge.recoveryAmountNote";

        private const string IconRelativePath = "Assets/Gaugeline.png";
        private const float IconPixelsPerUnit = 128f;

        internal static readonly LevelEventType EventType = (LevelEventType)NumericEventId;

        private static LevelEventInfo eventInfo;
        private static Sprite registeredIcon;
        private static Sprite customIcon;
        private static Texture2D customIconTexture;
        private static bool customIconLoadAttempted;

        internal static LevelEventInfo EventInfo
        {
            get
            {
                if (eventInfo == null)
                {
                    eventInfo = CreateEventInfo();
                }

                return eventInfo;
            }
        }

        internal static bool IsRegistered
        {
            get
            {
                if (GCS.levelEventsInfo == null || GCS.levelEventTypeString == null)
                {
                    return false;
                }

                LevelEventInfo registeredInfo;
                string registeredName;
                return GCS.levelEventsInfo.TryGetValue(NumericEventId.ToString(), out registeredInfo)
                    && ReferenceEquals(registeredInfo, eventInfo)
                    && GCS.levelEventTypeString.TryGetValue(EventType, out registeredName)
                    && string.Equals(registeredName, EventName, StringComparison.Ordinal);
            }
        }

        internal static bool TryRegister()
        {
            if (!AreGameRegistriesReady())
            {
                return false;
            }

            LevelEventInfo existingInfo;
            if (GCS.levelEventsInfo.TryGetValue(NumericEventId.ToString(), out existingInfo)
                && !ReferenceEquals(existingInfo, EventInfo))
            {
                throw new InvalidOperationException(
                    "커스텀 이벤트 ID " + NumericEventId + "가 다른 이벤트에서 이미 사용 중입니다.");
            }

            string existingName;
            if (GCS.levelEventTypeString.TryGetValue(EventType, out existingName)
                && !string.Equals(existingName, EventName, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "커스텀 이벤트 ID " + NumericEventId + "가 " + existingName + " 이벤트와 충돌합니다.");
            }

            // 편집기는 levelEventsInfo.Values를 순회한다. 이름/숫자 키를 둘 다 넣으면 버튼이 중복되므로
            // undefined enum의 ToString() 결과인 숫자 키 하나만 등록한다.
            GCS.levelEventsInfo[NumericEventId.ToString()] = EventInfo;
            GCS.levelEventTypeString[EventType] = EventName;
            EnsureIcon();
            return true;
        }

        private static bool AreGameRegistriesReady()
        {
            if (GCS.levelEventsInfo == null || GCS.levelEventTypeString == null)
            {
                return false;
            }

            LevelEventInfo nativeInfo;
            string nativeName;
            return GCS.levelEventsInfo.TryGetValue(nameof(LevelEventType.SetSpeed), out nativeInfo)
                && nativeInfo != null
                && GCS.levelEventTypeString.TryGetValue(LevelEventType.SetSpeed, out nativeName)
                && string.Equals(nativeName, nameof(LevelEventType.SetSpeed), StringComparison.Ordinal);
        }

        internal static void RollbackRegistration()
        {
            if (GCS.levelEventsInfo != null)
            {
                LevelEventInfo registeredInfo;
                if (GCS.levelEventsInfo.TryGetValue(NumericEventId.ToString(), out registeredInfo)
                    && ReferenceEquals(registeredInfo, eventInfo))
                {
                    GCS.levelEventsInfo.Remove(NumericEventId.ToString());
                }
            }

            if (GCS.levelEventTypeString != null)
            {
                string registeredName;
                if (GCS.levelEventTypeString.TryGetValue(EventType, out registeredName)
                    && string.Equals(registeredName, EventName, StringComparison.Ordinal))
                {
                    GCS.levelEventTypeString.Remove(EventType);
                }
            }

            if (GCS.levelEventIcons != null && registeredIcon != null)
            {
                Sprite currentIcon;
                if (GCS.levelEventIcons.TryGetValue(EventType, out currentIcon)
                    && ReferenceEquals(currentIcon, registeredIcon))
                {
                    GCS.levelEventIcons.Remove(EventType);
                }
            }

            registeredIcon = null;
            DestroyCustomIcon();
            customIconLoadAttempted = false;
        }

        internal static void EnsureIcon()
        {
            if (GCS.levelEventIcons == null || GCS.levelEventIcons.ContainsKey(EventType))
            {
                return;
            }

            Sprite icon = GetCustomIcon();
            if (icon != null)
            {
                registeredIcon = icon;
                GCS.levelEventIcons[EventType] = icon;
                return;
            }

            if (!GCS.levelEventIcons.TryGetValue(LevelEventType.EventSettings, out icon)
                && !GCS.levelEventIcons.TryGetValue(LevelEventType.SetSpeed, out icon))
            {
                foreach (KeyValuePair<LevelEventType, Sprite> pair in GCS.levelEventIcons)
                {
                    icon = pair.Value;
                    break;
                }
            }

            if (icon != null)
            {
                // 외부 아이콘이 없거나 손상된 경우에만 게임의 설정 이벤트 아이콘을 빌린다.
                registeredIcon = icon;
                GCS.levelEventIcons[EventType] = icon;
            }
        }

        private static Sprite GetCustomIcon()
        {
            if (customIcon != null)
            {
                return customIcon;
            }

            if (customIconLoadAttempted)
            {
                return null;
            }

            customIconLoadAttempted = true;
            string iconPath = string.IsNullOrEmpty(Main.ModDirectory)
                ? null
                : Path.Combine(Main.ModDirectory, IconRelativePath);
            if (string.IsNullOrEmpty(iconPath) || !File.Exists(iconPath))
            {
                if (Main.Logger != null)
                {
                    Main.Logger.Warning(
                        "PlanetGauge 이벤트 아이콘을 찾지 못해 기본 아이콘을 사용합니다: "
                        + (iconPath ?? IconRelativePath));
                }

                return null;
            }

            Texture2D texture = null;
            try
            {
                byte[] pngBytes = File.ReadAllBytes(iconPath);
                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                texture.name = "PlanetGauge.EventIcon.Texture";
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.filterMode = FilterMode.Bilinear;
                if (!LoadPng(texture, pngBytes))
                {
                    throw new InvalidDataException("PNG 데이터를 Unity Texture2D로 변환하지 못했습니다.");
                }

                /*
                 * 원본이 현재 300x300이어도 에디터의 고정 아이콘 슬롯이 크기를 맞춘다.
                 * 128 PPU를 사용해 추후 128x128 파일로 교체해도 동일한 스케일 계약을 유지한다.
                 */
                customIcon = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    IconPixelsPerUnit);
                if (customIcon == null)
                {
                    throw new InvalidOperationException("Texture2D에서 Sprite를 생성하지 못했습니다.");
                }

                customIcon.name = "PlanetGauge.EventIcon";
                customIconTexture = texture;
                return customIcon;
            }
            catch (Exception exception)
            {
                if (customIcon != null)
                {
                    UnityEngine.Object.Destroy(customIcon);
                }

                if (texture != null)
                {
                    UnityEngine.Object.Destroy(texture);
                }

                customIcon = null;
                customIconTexture = null;
                Main.LogException(
                    "PlanetGauge 이벤트 아이콘 로드에 실패해 기본 아이콘을 사용합니다: " + iconPath,
                    exception);
                return null;
            }
        }

        private static bool LoadPng(Texture2D texture, byte[] pngBytes)
        {
            // 설치본의 ImageConversionModule은 netstandard 2.1을 참조하므로 net48에서 직접 참조하지 않는다.
            // 아이콘은 선택 기능이므로 확인된 정확한 오버로드만 런타임에 찾아 호출하고, 없으면 폴백한다.
            Type imageConversionType = Type.GetType(
                "UnityEngine.ImageConversion, UnityEngine.ImageConversionModule",
                false);
            MethodInfo loadImage = imageConversionType == null
                ? null
                : imageConversionType.GetMethod(
                    "LoadImage",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(Texture2D), typeof(byte[]), typeof(bool) },
                    null);
            if (loadImage == null)
            {
                throw new MissingMethodException(
                    "UnityEngine.ImageConversion.LoadImage(Texture2D, byte[], bool)을 찾을 수 없습니다.");
            }

            object result = loadImage.Invoke(null, new object[] { texture, pngBytes, true });
            return result is bool && (bool)result;
        }

        private static void DestroyCustomIcon()
        {
            if (customIcon != null)
            {
                UnityEngine.Object.Destroy(customIcon);
                customIcon = null;
            }

            if (customIconTexture != null)
            {
                UnityEngine.Object.Destroy(customIconTexture);
                customIconTexture = null;
            }
        }

        internal static void ValidateRequiredGameApi()
        {
            RequireMethod(typeof(ADOStartup), nameof(ADOStartup.SetupLevelEventsInfo), Type.EmptyTypes);
            RequireMethod(typeof(scnEditor), nameof(scnEditor.LoadEditorProperties), Type.EmptyTypes);
            RequireMethod(
                typeof(scnEditor),
                nameof(scnEditor.GetFloorEvents),
                new[] { typeof(int), typeof(LevelEventType) });
            RequireMethod(
                typeof(InspectorPanel),
                nameof(InspectorPanel.ShowTabsForFloor),
                new[] { typeof(int) });
            RequireMethod(
                typeof(ADOFAI.PropertyInfo),
                nameof(ADOFAI.PropertyInfo.Validate),
                new[] { typeof(float) });
            RequireMethod(
                typeof(PropertyControl_Toggle),
                nameof(PropertyControl_Toggle.SelectVar),
                new[] { typeof(string) });
            RequireMethod(
                typeof(scnGame),
                nameof(scnGame.ApplyEvent),
                new[]
                {
                    typeof(LevelEvent),
                    typeof(float),
                    typeof(float),
                    typeof(List<scrFloor>),
                    typeof(float),
                    typeof(int?)
                });

            if (FindParseEnumMethod() == null)
            {
                throw new MissingMethodException(
                    "호환성에 필요한 RDUtils.ParseEnum<LevelEventType> 메서드를 찾을 수 없습니다.");
            }

            if (FindLevelEventConstructor() == null)
            {
                throw new MissingMethodException(
                    "호환성에 필요한 LevelEvent 생성자를 찾을 수 없습니다.");
            }

            RequireMethod(
                typeof(LevelEvent),
                nameof(LevelEvent.Decode),
                new[] { typeof(Dictionary<string, object>), typeof(string), typeof(bool) });
            RequireMethod(
                typeof(LevelEvent),
                nameof(LevelEvent.Encode),
                new[] { typeof(bool) });
            RequireMethod(
                typeof(RDString),
                nameof(RDString.GetWithCheck),
                new[] { typeof(string), typeof(bool).MakeByRefType(), typeof(Dictionary<string, object>) });
        }

        internal static MethodInfo FindParseEnumMethod()
        {
            MethodInfo[] methods = typeof(RDUtils).GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

            for (int index = 0; index < methods.Length; index++)
            {
                MethodInfo method = methods[index];
                Type[] genericArguments = method.GetGenericArguments();
                ParameterInfo[] parameters = method.GetParameters();
                if (method.Name == "ParseEnum"
                    && method.IsGenericMethodDefinition
                    && genericArguments.Length == 1
                    && parameters.Length == 2
                    && parameters[0].ParameterType == typeof(string)
                    && parameters[1].ParameterType == genericArguments[0]
                    && method.ReturnType == genericArguments[0])
                {
                    return method.MakeGenericMethod(typeof(LevelEventType));
                }
            }

            return null;
        }

        internal static ConstructorInfo FindLevelEventConstructor()
        {
            return AccessTools.Constructor(
                typeof(LevelEvent),
                new[]
                {
                    typeof(int),
                    typeof(LevelEventType),
                    typeof(LevelEventInfo),
                    typeof(Dictionary<string, object>),
                    typeof(Dictionary<string, bool>),
                    typeof(bool),
                    typeof(bool),
                    typeof(bool)
                });
        }

        private static LevelEventInfo CreateEventInfo()
        {
            LevelEventInfo info = new LevelEventInfo
            {
                name = EventName,
                type = EventType,
                pro = false,
                taroDLC = false,
                executionTime = LevelEventExecutionTime.OnBar,
                allowFirstFloor = true,
                isDecoration = false,
                useGroups = false,
                stretchViewport = false,
                categories = new List<LevelEventCategory> { LevelEventCategory.Gameplay },
                propertiesInfo = new Dictionary<string, ADOFAI.PropertyInfo>()
            };

            ADOFAI.PropertyInfo attributeMode = CreateProperty(
                info,
                AttributeModeKey,
                "Enum:" + typeof(PlanetGaugeAttributeMode).AssemblyQualifiedName,
                PlanetGaugeAttributeMode.Normal.ToString(),
                "속성 설정");
            MakeOptional(attributeMode, true);
            AddProperty(info, attributeMode, 0);

            ADOFAI.PropertyInfo attributeEnabled = CreateProperty(
                info,
                AttributeEnabledKey,
                "Bool",
                true,
                "선택 속성 켜기");
            AddAttributeShowConditions(attributeEnabled);
            AddProperty(info, attributeEnabled, 1);

            ADOFAI.PropertyInfo disableOthers = CreateProperty(
                info,
                DisableOtherAttributesKey,
                "Bool",
                false,
                "다른 속성 설정 끄기");
            AddAttributeShowConditions(disableOthers);
            disableOthers.showIfVals.Add(Tuple.Create(
                AttributeModeKey,
                PlanetGaugeAttributeMode.Normal.ToString()));
            disableOthers.showIfVals.Add(Tuple.Create(
                AttributeModeKey,
                PlanetGaugeAttributeMode.ForceRecovery.ToString()));
            AddProperty(info, disableOthers, 2);

            ADOFAI.PropertyInfo hideGaugeBar = CreateProperty(
                info,
                HideGaugeBarKey,
                "Bool",
                true,
                "게이지 바 끄기");
            AddHudHideShowCondition(hideGaugeBar);
            AddProperty(info, hideGaugeBar, 3);

            ADOFAI.PropertyInfo hideGaugeValue = CreateProperty(
                info,
                HideGaugeValueKey,
                "Bool",
                true,
                "체력 숫자 끄기");
            AddHudHideShowCondition(hideGaugeValue);
            AddProperty(info, hideGaugeValue, 4);

            ADOFAI.PropertyInfo hideAttributeText = CreateProperty(
                info,
                HideAttributeTextKey,
                "Bool",
                true,
                "적용 속성 문구 끄기");
            AddHudHideShowCondition(hideAttributeText);
            AddProperty(info, hideAttributeText, 5);

            ADOFAI.PropertyInfo hideRateToken = CreateProperty(
                info,
                HideRateTokenKey,
                "Bool",
                true,
                "배율 토큰 끄기");
            AddHudHideShowCondition(hideRateToken);
            AddProperty(info, hideRateToken, 6);

            ADOFAI.PropertyInfo hideForceRecoveryVisuals = CreateProperty(
                info,
                HideForceRecoveryVisualsKey,
                "Bool",
                true,
                "강제 회복 표시 끄기");
            AddHudHideShowCondition(hideForceRecoveryVisuals);
            AddProperty(info, hideForceRecoveryVisuals, 7);

            ADOFAI.PropertyInfo multiplier = CreateProperty(
                info,
                MultiplierPercentKey,
                "Float",
                100f,
                "변경값 설정");
            multiplier.unit = "%";
            multiplier.float_min = 0f;
            multiplier.float_max = 1000f;
            multiplier.showIfVals.Add(Tuple.Create(AttributeModeKey, PlanetGaugeAttributeMode.AmplifyDecrease.ToString()));
            multiplier.showIfVals.Add(Tuple.Create(AttributeModeKey, PlanetGaugeAttributeMode.AmplifyIncrease.ToString()));
            multiplier.showIfVals.Add(Tuple.Create(AttributeModeKey, PlanetGaugeAttributeMode.AmplifyBoth.ToString()));
            MakeOptional(multiplier, false);
            AddProperty(info, multiplier, 8);

            ADOFAI.PropertyInfo multiplierNote = CreateNoteProperty(
                info,
                MultiplierReuseNoteKey,
                MultiplierReuseLocalizationKey);
            multiplierNote.showIfVals.Add(Tuple.Create(AttributeModeKey, PlanetGaugeAttributeMode.AmplifyDecrease.ToString()));
            multiplierNote.showIfVals.Add(Tuple.Create(AttributeModeKey, PlanetGaugeAttributeMode.AmplifyIncrease.ToString()));
            multiplierNote.showIfVals.Add(Tuple.Create(AttributeModeKey, PlanetGaugeAttributeMode.AmplifyBoth.ToString()));
            AddProperty(info, multiplierNote, 9);

            ADOFAI.PropertyInfo recoveryAmount = CreateProperty(
                info,
                RecoveryAmountPercentKey,
                "Float",
                0f,
                "회복량 설정");
            recoveryAmount.unit = "%";
            recoveryAmount.float_min = -1000f;
            recoveryAmount.float_max = 1000f;
            recoveryAmount.showIfVals.Add(Tuple.Create(
                AttributeModeKey,
                PlanetGaugeAttributeMode.ForceRecovery.ToString()));
            AddProperty(info, recoveryAmount, 10);

            ADOFAI.PropertyInfo warningOffset = CreateProperty(
                info,
                WarningOffsetAngleKey,
                "Float",
                0f,
                "사전 경고 각도 오프셋");
            warningOffset.unit = "°";
            warningOffset.float_max = 0f;
            warningOffset.showIfVals.Add(Tuple.Create(
                AttributeModeKey,
                PlanetGaugeAttributeMode.ForceRecovery.ToString()));
            AddProperty(info, warningOffset, 11);

            ADOFAI.PropertyInfo warningPulse = CreateProperty(
                info,
                WarningPulseBeatsKey,
                "Float",
                0.5f,
                "점멸 주기");
            warningPulse.unit = "beats";
            warningPulse.float_min = 0.125f;
            warningPulse.float_max = 16f;
            warningPulse.showIfVals.Add(Tuple.Create(
                AttributeModeKey,
                PlanetGaugeAttributeMode.ForceRecovery.ToString()));
            AddProperty(info, warningPulse, 12);

            ADOFAI.PropertyInfo recoveryAmountNote = CreateNoteProperty(
                info,
                RecoveryAmountNoteKey,
                RecoveryAmountLocalizationKey);
            recoveryAmountNote.showIfVals.Add(Tuple.Create(
                AttributeModeKey,
                PlanetGaugeAttributeMode.ForceRecovery.ToString()));
            AddProperty(info, recoveryAmountNote, 13);

            ADOFAI.PropertyInfo failureProtection = CreateProperty(
                info,
                FailureProtectionKey,
                "Bool",
                true,
                "실패 방지");
            MakeOptional(failureProtection, false);
            AddProperty(info, failureProtection, 14);

            ADOFAI.PropertyInfo recoveryCapEnabled = CreateProperty(
                info,
                RecoveryCapEnabledKey,
                "Bool",
                false,
                "회복 상한 설정");
            MakeOptional(recoveryCapEnabled, false);
            AddProperty(info, recoveryCapEnabled, 15);

            ADOFAI.PropertyInfo recoveryCap = CreateProperty(
                info,
                RecoveryCapPercentKey,
                "Float",
                100f,
                "회복 상한");
            recoveryCap.unit = "%";
            recoveryCap.float_min = 0.1f;
            recoveryCap.float_max = 1000f;
            // PropertyInfo.ValueMatch는 Bool 조건에서 소문자 "true"를 요구한다.
            recoveryCap.showIfVals.Add(Tuple.Create(RecoveryCapEnabledKey, "true"));
            AddProperty(info, recoveryCap, 16);

            ADOFAI.PropertyInfo forceCap = CreateProperty(
                info,
                ForceRecoveryCapKey,
                "Bool",
                true,
                "체력 상한 강제 제한");
            forceCap.showIfVals.Add(Tuple.Create(RecoveryCapEnabledKey, "true"));
            AddProperty(info, forceCap, 17);

            ADOFAI.PropertyInfo autoTileRecovery = CreateProperty(
                info,
                AutoTileRecoveryKey,
                "Bool",
                false,
                "자동 플레이 타일 체력 회복");
            MakeOptional(autoTileRecovery, false);
            AddProperty(info, autoTileRecovery, 18);

            return info;
        }

        private static void AddAttributeShowConditions(ADOFAI.PropertyInfo property)
        {
            property.showIfVals.Add(Tuple.Create(AttributeModeKey, PlanetGaugeAttributeMode.BlockRecovery.ToString()));
            property.showIfVals.Add(Tuple.Create(AttributeModeKey, PlanetGaugeAttributeMode.AmplifyDecrease.ToString()));
            property.showIfVals.Add(Tuple.Create(AttributeModeKey, PlanetGaugeAttributeMode.AmplifyIncrease.ToString()));
            property.showIfVals.Add(Tuple.Create(AttributeModeKey, PlanetGaugeAttributeMode.AmplifyBoth.ToString()));
            property.showIfVals.Add(Tuple.Create(AttributeModeKey, PlanetGaugeAttributeMode.Blindfold.ToString()));
            property.showIfVals.Add(Tuple.Create(AttributeModeKey, PlanetGaugeAttributeMode.HideGaugeHud.ToString()));
        }

        private static void AddHudHideShowCondition(ADOFAI.PropertyInfo property)
        {
            property.showIfVals.Add(Tuple.Create(
                AttributeModeKey,
                PlanetGaugeAttributeMode.HideGaugeHud.ToString()));
        }

        private static ADOFAI.PropertyInfo CreateProperty(
            LevelEventInfo info,
            string name,
            string type,
            object defaultValue,
            string label)
        {
            Dictionary<string, object> schema = new Dictionary<string, object>
            {
                { "name", name },
                { "type", type },
                { "default", defaultValue },
                { "customLabel", label }
            };

            return new ADOFAI.PropertyInfo(schema, info);
        }

        private static ADOFAI.PropertyInfo CreateNoteProperty(
            LevelEventInfo info,
            string name,
            string noteKey)
        {
            Dictionary<string, object> schema = new Dictionary<string, object>
            {
                { "name", name },
                { "type", "Note" },
                { "noteKey", noteKey },
                { "encode", false }
            };

            return new ADOFAI.PropertyInfo(schema, info);
        }

        private static void MakeOptional(ADOFAI.PropertyInfo property, bool startEnabled)
        {
            property.canBeDisabled = true;
            property.startEnabled = startEnabled;
        }

        private static void AddProperty(LevelEventInfo info, ADOFAI.PropertyInfo property, int order)
        {
            property.order = order;
            info.propertiesInfo.Add(property.name, property);
        }

        private static void RequireMethod(Type type, string name, Type[] parameterTypes)
        {
            if (AccessTools.Method(type, name, parameterTypes) == null)
            {
                throw new MissingMethodException(
                    "호환성에 필요한 게임 메서드를 찾을 수 없습니다: " + type.FullName + "." + name);
            }
        }
    }

}
