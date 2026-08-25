using System;
using System.Reflection;
using HarmonyLib;
using UnityModManagerNet;

namespace PlanetGauge
{
    /// <summary>
    /// Unity Mod Manager 진입점이며 모드 활성화, Harmony 패치 수명, 필수 API 검사를 조정한다.
    /// 게임별 동작은 패치와 <see cref="GaugeRuntime"/>에 위임하고 전역 수명만 소유한다.
    /// </summary>
    public static class Main
    {
        internal const string ModId = "PlanetGauge";

        private static Harmony harmony;
        private static bool registeredWithGame;
        private static int temporaryMissRecoveryDepth;

        public static bool IsEnabled { get; private set; }

        public static bool EditorGaugeEnabled { get; private set; }

        internal static PlanetGaugeSettings Settings { get; private set; }

        internal static UnityModManager.ModEntry.ModLogger Logger { get; private set; }

        internal static string ModDirectory { get; private set; }

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            // Load는 등록만 수행한다. 게임 코드를 바꾸는 패치는 사용자가 모드를 켤 때 적용한다.
            Logger = modEntry.Logger;
            ModDirectory = modEntry.Path;
            Settings = UnityModManager.ModSettings.Load<PlanetGaugeSettings>(modEntry)
                ?? new PlanetGaugeSettings();
            Settings.Sanitize();
            modEntry.OnToggle = OnToggle;
            modEntry.OnGUI = OnGui;
            modEntry.OnSaveGUI = OnSaveGui;
            return true;
        }

        private static void OnGui(UnityModManager.ModEntry modEntry)
        {
            if (Settings != null)
            {
                Settings.DrawGui();
            }
        }

        private static void OnSaveGui(UnityModManager.ModEntry modEntry)
        {
            if (Settings != null)
            {
                Settings.Sanitize();
                Settings.Save(modEntry);
            }
        }

        private static bool OnToggle(UnityModManager.ModEntry modEntry, bool enabled)
        {
            try
            {
                if (enabled)
                {
                    Enable(modEntry);
                }
                else
                {
                    Disable();
                }

                return true;
            }
            catch (Exception exception)
            {
                LogException("모드 토글 처리에 실패했습니다.", exception);
                return false;
            }
        }

        private static void Enable(UnityModManager.ModEntry modEntry)
        {
            if (IsEnabled)
            {
                return;
            }

            ValidateRequiredGameApi();

            // 등록부터 런타임 호스트 생성까지 하나의 트랜잭션으로 취급한다.
            harmony = new Harmony(modEntry.Info.Id);
            bool eventWasRegistered = PlanetGaugeLevelEventRegistry.IsRegistered;
            try
            {
                harmony.PatchAll(typeof(Main).Assembly);

                IsEnabled = true;
                EditorGaugeEnabled = false;
                GaugeRuntime.Reset();
                RuntimeHost.Create();

                bool eventRegistered = PlanetGaugeLevelEventRegistry.TryRegister();
                if (!eventRegistered && Logger != null)
                {
                    Logger.Log(
                        "레벨 이벤트 사전 초기화를 기다립니다. "
                        + "ADOStartup.SetupLevelEventsInfo 완료 후 PlanetGauge 설정 이벤트를 등록합니다.");
                }
                else if (eventRegistered && !eventWasRegistered && Logger != null)
                {
                    Logger.Log("PlanetGauge 설정 이벤트를 즉시 등록했습니다.");
                }

                if (!registeredWithGame)
                {
                    ADOStartup.ModWasAdded(ModId);
                    registeredWithGame = true;
                }
            }
            catch
            {
                IsEnabled = false;
                EditorGaugeEnabled = false;
                GaugeRuntime.Reset();
                RuntimeHost.DestroyHost();
                harmony.UnpatchAll(harmony.Id);
                harmony = null;
                if (!eventWasRegistered)
                {
                    PlanetGaugeLevelEventRegistry.RollbackRegistration();
                }

                throw;
            }

            Logger.Log("PlanetGauge가 활성화되었습니다.");
        }

        private static void ValidateRequiredGameApi()
        {
            RequireMethod(typeof(scnEditor), nameof(scnEditor.Play), Type.EmptyTypes);
            RequireMethod(
                typeof(scnEditor),
                nameof(scnEditor.SwitchToEditMode),
                new[] { typeof(bool) });
            RequireMethod(typeof(scrPlanet), nameof(scrPlanet.SwitchChosen), Type.EmptyTypes);
            RequireMethod(
                typeof(scrController),
                nameof(scrController.OnLandOnPortal),
                new[] { typeof(scrPlanet), typeof(Portal), typeof(string) });
            RequireMethod(
                typeof(scrPlayer),
                nameof(scrPlayer.Die),
                new[] { typeof(bool), typeof(bool), typeof(string), typeof(bool) });
            RequireMethod(
                typeof(scrController),
                nameof(scrController.Restart),
                new[] { typeof(bool) });
            RequireMethod(
                typeof(scrController),
                nameof(scrController.ResetCustomLevel),
                new[] { typeof(bool) });

            RequireField(typeof(scnEditor), nameof(scnEditor.buttonNoFail));
            RequireField(typeof(scrController), nameof(scrController.noFail));
            RequireField(typeof(scrController), nameof(scrController.noFailInfiniteMargin));
            RequireField(typeof(scrPlayer), nameof(scrPlayer.failBar));

            PlanetGaugeLevelEventRegistry.ValidateRequiredGameApi();
        }

        private static void RequireMethod(Type type, string name, Type[] parameterTypes)
        {
            if (AccessTools.Method(type, name, parameterTypes) == null)
            {
                throw new MissingMethodException(
                    "호환성에 필요한 게임 메서드를 찾을 수 없습니다: "
                    + type.FullName
                    + "."
                    + name);
            }
        }

        private static void RequireField(Type type, string name)
        {
            if (AccessTools.Field(type, name) == null)
            {
                throw new MissingFieldException(
                    "호환성에 필요한 게임 필드를 찾을 수 없습니다: "
                    + type.FullName
                    + "."
                    + name);
            }
        }

        private static void Disable()
        {
            if (!IsEnabled)
            {
                return;
            }

            IsEnabled = false;
            EditorGaugeEnabled = false;
            GaugeRuntime.Reset();
            RuntimeHost.DestroyHost();

            // 레벨/버튼이 이미 이 메타데이터를 참조할 수 있어 현재 프로세스에서는 등록 사전을 유지한다.
            // Harmony와 런타임 호스트는 제거되므로 비활성 상태에서는 이벤트가 실행되지 않는다.

            // 이 Harmony ID가 설치한 패치만 제거해 다른 모드의 패치를 보존한다.
            if (harmony != null)
            {
                harmony.UnpatchAll(harmony.Id);
                harmony = null;
            }

            if (Logger != null)
            {
                Logger.Log("PlanetGauge가 비활성화되었습니다.");
            }
        }

        internal static void BeginEditorSession()
        {
            EditorGaugeEnabled = false;
            GaugeRuntime.Reset();
        }

        internal static void SetEditorGaugeEnabled(bool enabled)
        {
            EditorGaugeEnabled = enabled;
            GaugeRuntime.Reset();
        }

        /// <summary>
        /// 결과 문자열을 생성하는 짧은 구간에만 noFail을 켜 실패 상세 행을 포함시킨다.
        /// 실제 클리어 및 저장 판정에는 영향을 주지 않도록 호출 종료 시 즉시 원복한다.
        /// </summary>
        [HarmonyPatch]
        private static class DetailedResultsFailureRowsPatch
        {
            private struct ResultsState
            {
                internal scrController Controller;
                internal bool RestoreNoFail;
                internal bool OriginalNoFail;
            }

            private static MethodBase TargetMethod()
            {
                return AccessTools.Method(
                    typeof(DetailedResults),
                    "GenerateResults",
                    new[] { typeof(scrMarginTracker) });
            }

            [HarmonyPrepare]
            private static bool Prepare()
            {
                if (TargetMethod() != null)
                {
                    return true;
                }

                if (Logger != null)
                {
                    Logger.Log(
                        "[경고] 이 게임 버전에는 DetailedResults.GenerateResults 메서드가 없습니다. "
                        + "모드는 계속 실행하지만 결과 화면의 실패 상세 행 보정은 비활성화합니다.");
                }

                return false;
            }

            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            private static void Prefix(ref ResultsState __state)
            {
                __state = default(ResultsState);

                if (!GaugeRuntime.ShouldHandle())
                {
                    return;
                }

                scrController controller = scrController.instance;
                if (controller == null || controller.noFail)
                {
                    return;
                }

                // GenerateResults는 noFail일 때만 놓침/과부하 행을 만든다.
                // 결과 문자열을 만드는 동안에만 플래그를 빌려 일반 클리어/저장 판정은 유지한다.
                __state.Controller = controller;
                __state.RestoreNoFail = true;
                __state.OriginalNoFail = controller.noFail;
                controller.noFail = true;
            }

            [HarmonyPostfix]
            private static void Postfix(ref ResultsState __state)
            {
                RestoreNoFail(ref __state);
            }

            [HarmonyFinalizer]
            private static Exception Finalizer(Exception __exception, ref ResultsState __state)
            {
                RestoreNoFail(ref __state);
                return __exception;
            }

            private static void RestoreNoFail(ref ResultsState state)
            {
                if (state.RestoreNoFail && state.Controller != null)
                {
                    state.Controller.noFail = state.OriginalNoFail;
                    state.RestoreNoFail = false;
                }
            }
        }

        /// <summary>
        /// 놓침 뒤 바닐라의 후처리가 멈추지 않도록 CheckPostHoldFail 실행 중에만 noFail을 대여한다.
        /// 중첩 호출은 <see cref="temporaryMissRecoveryDepth"/>로 추적한다.
        /// </summary>
        [HarmonyPatch]
        private static class CheckPostHoldFailRecoveryPatch
        {
            private struct RecoveryState
            {
                internal scrController Controller;
                internal bool RestoreNoFail;
                internal bool OriginalNoFail;
            }

            private static MethodBase TargetMethod()
            {
                return FindCheckPostHoldFailMethod();
            }

            [HarmonyPrepare]
            private static bool Prepare()
            {
                if (FindCheckPostHoldFailMethod() != null)
                {
                    return true;
                }

                if (Logger != null)
                {
                    Logger.Log(
                        "[경고] 이 게임 버전에는 scrPlayer.CheckPostHoldFail 메서드가 없습니다. "
                        + "모드는 계속 실행하지만 놓침 각도 복구 보정은 비활성화합니다.");
                }

                return false;
            }

            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            private static void Prefix(scrPlayer __instance, ref RecoveryState __state)
            {
                __state = default(RecoveryState);

                if (!GaugeRuntime.EventSettings.FailureProtection
                    || GaugeRuntime.Current <= 0f
                    || GaugeRuntime.IsAutoPlay(__instance)
                    || !GaugeRuntime.ShouldHandle(__instance))
                {
                    return;
                }

                scrController controller = scrController.instance;
                if (controller == null || controller.noFail)
                {
                    return;
                }

                __state.Controller = controller;
                __state.RestoreNoFail = true;
                __state.OriginalNoFail = controller.noFail;
                temporaryMissRecoveryDepth++;
                controller.noFail = true;
            }

            [HarmonyPostfix]
            private static void Postfix(ref RecoveryState __state)
            {
                RestoreTemporaryNoFail(ref __state);
            }

            [HarmonyFinalizer]
            private static Exception Finalizer(Exception __exception, ref RecoveryState __state)
            {
                RestoreTemporaryNoFail(ref __state);
                return __exception;
            }

            private static void RestoreTemporaryNoFail(ref RecoveryState state)
            {
                if (!state.RestoreNoFail)
                {
                    return;
                }

                if (state.Controller != null)
                {
                    state.Controller.noFail = state.OriginalNoFail;
                }

                if (temporaryMissRecoveryDepth > 0)
                {
                    temporaryMissRecoveryDepth--;
                }

                state.RestoreNoFail = false;
            }
        }

        /// <summary>
        /// 위 복구 구간에서 호출된 Die가 실제 noFail 설정으로 오인되지 않도록 임시 플래그를 잠시 해제한다.
        /// </summary>
        [HarmonyPatch(typeof(scrPlayer), nameof(scrPlayer.Die))]
        private static class TemporaryNoFailDieBridgePatch
        {
            private struct BridgeState
            {
                internal scrController Controller;
                internal bool RestoreTemporaryNoFail;
                internal bool OriginalNoFail;
            }

            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            private static void Prefix(
                scrPlayer __instance,
                bool hitbox,
                ref BridgeState __state)
            {
                __state = default(BridgeState);

                if (temporaryMissRecoveryDepth <= 0
                    || hitbox
                    || GaugeRuntime.IsAutoPlay(__instance)
                    || !GaugeRuntime.ShouldHandle(__instance))
                {
                    return;
                }

                scrController controller = scrController.instance;
                if (controller == null || !controller.noFail)
                {
                    return;
                }

                // CheckPostHoldFail에 빌려준 noFail은 실제 실패 방지 설정보다 우선하면 안 된다.
                // Die 패치가 FailMiss를 차감한 뒤, 게이지가 남았을 때만 다시 noFail 복구로 진입한다.
                __state.Controller = controller;
                __state.RestoreTemporaryNoFail = true;
                __state.OriginalNoFail = controller.noFail;
                controller.noFail = false;
            }

            [HarmonyPostfix]
            [HarmonyPriority(Priority.First)]
            private static void Postfix(ref BridgeState __state)
            {
                RestoreTemporaryNoFail(ref __state);
            }

            [HarmonyFinalizer]
            [HarmonyPriority(Priority.First)]
            private static Exception Finalizer(Exception __exception, ref BridgeState __state)
            {
                RestoreTemporaryNoFail(ref __state);
                return __exception;
            }

            private static void RestoreTemporaryNoFail(ref BridgeState state)
            {
                if (state.RestoreTemporaryNoFail && state.Controller != null)
                {
                    state.Controller.noFail = state.OriginalNoFail;
                    state.RestoreTemporaryNoFail = false;
                }
            }
        }

        private static MethodInfo FindCheckPostHoldFailMethod()
        {
            MethodInfo[] methods = typeof(scrPlayer).GetMethods(
                BindingFlags.Instance
                | BindingFlags.Static
                | BindingFlags.Public
                | BindingFlags.NonPublic);

            for (int index = 0; index < methods.Length; index++)
            {
                MethodInfo method = methods[index];
                ParameterInfo[] parameters = method.GetParameters();
                if (string.Equals(method.Name, "CheckPostHoldFail", StringComparison.Ordinal)
                    && !method.IsStatic
                    && method.ReturnType == typeof(void)
                    && parameters.Length == 1
                    && parameters[0].ParameterType == typeof(ulong?))
                {
                    return method;
                }
            }

            return null;
        }

        internal static void LogException(string message, Exception exception)
        {
            if (Logger != null)
            {
                Logger.Error(message + Environment.NewLine + exception);
            }
        }
    }
}
