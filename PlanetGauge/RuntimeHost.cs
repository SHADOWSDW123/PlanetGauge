using UnityEngine;

namespace PlanetGauge
{
    /// <summary>
    /// Unity 생명주기와 일반 C# 객체로 구현된 모드 UI를 연결하는 영구 호스트다.
    /// 장면이 바뀌어도 하나만 유지되며, 에디터 인스턴스 교체와 HUD 갱신을 감시한다.
    /// </summary>
    internal sealed class RuntimeHost : MonoBehaviour
    {
        private static RuntimeHost instance;

        private scnEditor observedEditor;
        private MainGaugeHud mainGaugeHud;
        private GaugeDebugHud debugHud;
        private bool debugShortcutWasHeld;

        internal static void Create()
        {
            // 모드 토글이 중복 호출되어도 Update 루프는 하나만 존재해야 한다.
            if (instance != null)
            {
                return;
            }

            GameObject hostObject = new GameObject("PlanetGauge.RuntimeHost");
            instance = hostObject.AddComponent<RuntimeHost>();
            DontDestroyOnLoad(hostObject);
        }

        internal static void DestroyHost()
        {
            if (instance == null)
            {
                EditorGaugeButton.Destroy();
                return;
            }

            Destroy(instance.gameObject);
            instance = null;
        }

        internal static void ResetDebugVisibility()
        {
            if (instance != null && instance.debugHud != null)
            {
                instance.debugHud.ResetVisibility();
            }
        }

        private void Awake()
        {
            mainGaugeHud = new MainGaugeHud();
            debugHud = new GaugeDebugHud();
        }

        private void Update()
        {
            // ForceRecovery 표시 전환은 기존 HUD 색 전환과 같은 unscaled 시간축을 사용한다.
            GaugeVisualTransitions.Tick(Time.unscaledDeltaTime);
            GaugeHudVisibilityTransitions.Tick(Time.unscaledDeltaTime);

            scnEditor editor = scnEditor.instance;
            if (editor != observedEditor)
            {
                // 에디터는 장면 전환 때 새 인스턴스로 교체되므로 이전 UI 참조를 폐기한다.
                observedEditor = editor;
                EditorGaugeButton.Destroy();

                if (editor != null)
                {
                    debugHud.ResetVisibility();
                    Main.BeginEditorSession();
                }
            }

            if (editor != null)
            {
                EditorGaugeButton.Ensure(editor);
                EditorGaugeButton.Sync();
            }
            else
            {
                EditorGaugeButton.Destroy();
            }

            KeyCode debugKey1 = Main.Settings.DebugKey1;
            KeyCode debugKey2 = Main.Settings.DebugKey2;
            bool debugShortcutIsHeld = GetDebugKey(debugKey1) && GetDebugKey(debugKey2);
            if (debugShortcutIsHeld && !debugShortcutWasHeld)
            {
                debugHud.Toggle();
            }
            debugShortcutWasHeld = debugShortcutIsHeld;

            debugHud.Update();
        }

        private static bool GetDebugKey(KeyCode key)
        {
            // 기존 기본 단축키의 "Shift"는 좌우 Shift 모두를 의미했다.
            return key == KeyCode.LeftShift
                ? Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)
                : Input.GetKey(key);
        }

        private void LateUpdate()
        {
            // 게임 HUD의 레이아웃과 애니메이션이 반영된 뒤 게이지 위치를 계산한다.
            if (mainGaugeHud != null)
            {
                mainGaugeHud.Update();
            }
        }

        private void OnDestroy()
        {
            EditorGaugeButton.Destroy();

            if (mainGaugeHud != null)
            {
                mainGaugeHud.Dispose();
                mainGaugeHud = null;
            }

            if (debugHud != null)
            {
                debugHud.Dispose();
                debugHud = null;
            }

            if (instance == this)
            {
                instance = null;
            }
        }
    }
}
