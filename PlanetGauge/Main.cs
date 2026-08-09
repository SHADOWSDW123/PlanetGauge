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

        // 디버그용: false로 바꾸면 CheckPostHoldFail의 바닐라 실패 복구 보정만 비활성화된다.
        private static readonly bool EnableMissAngleRecovery = true;

        private static Harmony harmony;
        private static bool registeredWithGame;
        private static int temporaryMissRecoveryDepth;

        public static bool IsEnabled { get; private set; }

        public static bool EditorGaugeEnabled { get; private set; }

        // 디버거나 다른 모드 코드에서 현재 값을 읽을 수 있도록 공개한다.
        public static float CurrentGauge
        {
            get { return GaugeRuntime.Current; }
        }

        internal static PlanetGaugeSettings Settings { get; private set; }

        internal static UnityModManager.ModEntry.ModLogger Logger { get; private set; }

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            // Load는 등록만 수행한다. 게임 코드를 바꾸는 패치는 사용자가 모드를 켤 때 적용한다.
            Logger = modEntry.Logger;
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

            // 필수 API 확인이 끝난 뒤에만 패치해 부분 활성화 상태를 만들지 않는다.
            harmony = new Harmony(modEntry.Info.Id);
            harmony.PatchAll(typeof(Main).Assembly);

            IsEnabled = true;
            EditorGaugeEnabled = false;
            GaugeRuntime.Reset();
            RuntimeHost.Create();

            if (!registeredWithGame)
            {
                ADOStartup.ModWasAdded(ModId);
                registeredWithGame = true;
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
        /// TooLate 중간 판정을 무시하고 뒤이어 확정되는 FailMiss만 차감하도록 하는 게이지 전처리다.
        /// </summary>
        [HarmonyPatch(typeof(GaugeRuntime), nameof(GaugeRuntime.ApplyJudgement))]
        private static class IgnoreTooLateGaugePatch
        {
            private static bool Prefix(HitMargin judgement, ref bool __result)
            {
                if (judgement != HitMargin.TooLate
                    || GaugeRuntime.IsAutoPlay()
                    || !GaugeRuntime.ShouldHandle())
                {
                    return true;
                }

                // TooLate는 입력을 진행시키지 못한 중간 상태다.
                // 이 시점에는 차감하지 않고, 뒤이어 확정되는 FailMiss 한 번만 반영한다.
                GaugeRuntime.ClearPendingDieCharge();
                __result = false;
                return false;
            }
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
                    state.Controller.noFail = false;
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
            }

            private static MethodBase TargetMethod()
            {
                return FindMethodByName(typeof(scrPlayer), "CheckPostHoldFail");
            }

            [HarmonyPrepare]
            private static bool Prepare()
            {
                if (!EnableMissAngleRecovery)
                {
                    return false;
                }

                if (FindMethodByName(typeof(scrPlayer), "CheckPostHoldFail") != null)
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

                if (!EnableMissAngleRecovery
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
                    state.Controller.noFail = false;
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
                    state.Controller.noFail = true;
                    state.RestoreTemporaryNoFail = false;
                }
            }
        }

        private static MethodInfo FindMethodByName(Type type, string name)
        {
            MethodInfo[] methods = type.GetMethods(
                BindingFlags.Instance
                | BindingFlags.Static
                | BindingFlags.Public
                | BindingFlags.NonPublic);

            for (int index = 0; index < methods.Length; index++)
            {
                if (string.Equals(methods[index].Name, name, StringComparison.Ordinal))
                {
                    return methods[index];
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
