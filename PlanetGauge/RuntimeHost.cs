using UnityEngine;

namespace PlanetGauge
{
    internal sealed class RuntimeHost : MonoBehaviour
    {
        private static RuntimeHost instance;

        private scnEditor observedEditor;
        private MainGaugeHud mainGaugeHud;

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
            mainGaugeHud = new MainGaugeHud();
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
        }

        private void LateUpdate()
        {
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

            if (instance == this)
            {
                instance = null;
            }
        }
    }
}
