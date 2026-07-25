using UnityEngine;

namespace PlanetGauge
{
    internal sealed class RuntimeHost : MonoBehaviour
    {
        private static RuntimeHost instance;

        private scnEditor observedEditor;
        private GaugeDebugOverlay overlay;

        internal static void Create()
        {
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

        private void Awake()
        {
            overlay = new GaugeDebugOverlay(transform);
        }

        private void Update()
        {
            scnEditor editor = scnEditor.instance;
            if (editor != observedEditor)
            {
                observedEditor = editor;
                EditorGaugeButton.Destroy();

                if (editor != null)
                {
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

            overlay.Update();
        }

        private void OnDestroy()
        {
            EditorGaugeButton.Destroy();

            if (overlay != null)
            {
                overlay.Dispose();
                overlay = null;
            }

            if (instance == this)
            {
                instance = null;
            }
        }
    }
}
