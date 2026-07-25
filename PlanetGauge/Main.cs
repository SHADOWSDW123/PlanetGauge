using System;
using System.IO;
using System.Security.Cryptography;
using HarmonyLib;
using UnityModManagerNet;

namespace PlanetGauge
{
    public static class Main
    {
        internal const string ModId = "PlanetGauge";
        internal const string ExpectedGameAssemblySha256 =
            "0C50DDAE9052612AA29D1BFF8878A006A23D8E6AC1105E0C61B78A8A4964D42B";

        private static Harmony harmony;
        private static bool registeredWithGame;

        public static bool IsEnabled { get; private set; }

        public static bool EditorGaugeEnabled { get; private set; }

        // 디버거나 다른 모드 코드에서 현재 값을 읽을 수 있도록 공개한다.
        public static float CurrentGauge
        {
            get { return GaugeRuntime.Current; }
        }

        internal static UnityModManager.ModEntry.ModLogger Logger { get; private set; }

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            Logger = modEntry.Logger;
            modEntry.OnToggle = OnToggle;
            return true;
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

            ValidateGameCompatibility();

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

        private static void ValidateGameCompatibility()
        {
            ValidateRequiredGameApi();

            string assemblyPath = typeof(scrController).Assembly.Location;
            if (string.IsNullOrEmpty(assemblyPath) || !File.Exists(assemblyPath))
            {
                Logger.Log(
                    "[경고] 현재 Assembly-CSharp.dll의 해시를 확인할 수 없습니다. "
                    + "필수 API 검사만 통과한 상태로 실행합니다.");
                return;
            }

            try
            {
                string actualHash;
                using (SHA256 sha256 = SHA256.Create())
                using (FileStream stream = File.OpenRead(assemblyPath))
                {
                    actualHash = BitConverter.ToString(sha256.ComputeHash(stream))
                        .Replace("-", string.Empty);
                }

                if (!string.Equals(
                    actualHash,
                    ExpectedGameAssemblySha256,
                    StringComparison.OrdinalIgnoreCase))
                {
                    Logger.Log(
                        "[경고] 검증된 버전과 다른 Assembly-CSharp.dll입니다. "
                        + "필수 API가 존재하므로 호환 모드로 계속 실행합니다."
                        + Environment.NewLine
                        + "Expected: "
                        + ExpectedGameAssemblySha256
                        + Environment.NewLine
                        + "Actual:   "
                        + actualHash);
                }
            }
            catch (Exception exception)
            {
                Logger.Log(
                    "[경고] Assembly-CSharp.dll 해시 계산에 실패했습니다. "
                    + "필수 API 검사만 통과한 상태로 실행합니다."
                    + Environment.NewLine
                    + exception.Message);
            }
        }

        private static void ValidateRequiredGameApi()
        {
            RequireMethod(typeof(scnEditor), nameof(scnEditor.Play), Type.EmptyTypes);
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

        internal static void LogException(string message, Exception exception)
        {
            if (Logger != null)
            {
                Logger.Error(message + Environment.NewLine + exception);
            }
        }
    }
}
