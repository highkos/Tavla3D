using TavlaAI.Analysis;
using TavlaAI.Board;
using TavlaAI.Bootstrap;
using TavlaAI.Cube;
using TavlaAI.Dice;
using TavlaAI.Hud;
using TavlaAI.Infrastructure;
using TavlaAI.Input;
using TavlaAI.Online;
using TavlaAI.Settings;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TavlaAI.Editor
{
    /// <summary>
    /// Faz 5 Bolum 6.3: <c>Main.unity</c> sahnesini <b>kodla</b> uretir.
    /// </summary>
    /// <remarks>
    /// Sahne elle YAML yazilarak degil, bu betikle uretilir; boylece yapi tek kaynaktan gelir ve
    /// tekrar uretim (idempotent) mumkundur.
    /// </remarks>
    public static class SceneBuilder
    {
        /// <summary>Sahne yolu.</summary>
        public const string ScenePath = "Assets/Scenes/Main.unity";

        /// <summary>Sahneyi uretir ve kaydeder.</summary>
        public static void Build()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Camera camera = CreateCamera();
            CreateEventSystem();

            Canvas canvas = CreateCanvas(camera);
            Transform canvasRoot = canvas.transform;

            CreateBoardRoot(canvasRoot);
            CreateSidebarBackground(canvasRoot);

            // Yerlesim kurali: tahta solda, tum denetimler sagdaki kenar cubugunda.
            // Kenar cubugu tahtanin saginda kaldigi icin hicbir denetim oyun alanini kapatmaz.
            CreateSidebarItem<MatchScoreView>(canvasRoot, "MatchScorePanel", 20f, UiLayout.SidebarWidth, 90f);
            CreateSidebarItem<CrawfordBadgeView>(canvasRoot, "CrawfordBadge", 118f, 160f, 32f);
            CreateSidebarItem<MatchLengthView>(canvasRoot, "MatchLengthPanel", 158f, UiLayout.SidebarWidth, 66f);
            CreateSidebarItem<DiceView>(canvasRoot, "DiceRoot", 236f, 260f, 112f);
            CreateSidebarItem<CubeView>(canvasRoot, "CubeRoot", 360f, 120f, 200f);
            CreateSidebarItem<AnalysisPanelView>(canvasRoot, "AnalysisPanel", 572f, UiLayout.SidebarWidth, 196f);
            CreateSidebarItem<SettingsPanelView>(canvasRoot, "SettingsPanel", 780f, UiLayout.SidebarWidth, 220f);
            CreateStretched<BoardInputController>(canvasRoot, "BoardInput");

            var bootstrapGo = new GameObject("GameBootstrap");
            bootstrapGo.AddComponent<GameBootstrap>();

            // Cevrimici mod koprusu. GameBootstrap'tan sonra calisir (DefaultExecutionOrder -90),
            // boylece Instance hazir olur. Cevrimdisi oyun bu bilesen olmadan da calisir.
            var onlineGo = new GameObject("OnlineGameBridge");
            onlineGo.AddComponent<OnlineGameBridge>();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[TavlaAI] Sahne uretildi: " + ScenePath);
        }

        private static Camera CreateCamera()
        {
            var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            go.tag = "MainCamera";
            var camera = go.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5.4f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.06f, 0.07f, 0.09f, 1f);
            go.transform.position = new Vector3(0f, 0f, -10f);
            return camera;
        }

        private static void CreateEventSystem()
        {
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            go.transform.position = Vector3.zero;
        }

        private static Canvas CreateCanvas(Camera camera)
        {
            var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 5f;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        private static void CreateBoardRoot(Transform parent)
        {
            // Tahta alani: sol/ust/alt bosluklar sabit, sagda kenar cubuguna yer birakilir.
            var areaGo = new GameObject("BoardArea", typeof(RectTransform));
            var area = (RectTransform)areaGo.transform;
            area.SetParent(parent, false);
            area.anchorMin = Vector2.zero;
            area.anchorMax = Vector2.one;
            area.pivot = new Vector2(0.5f, 0.5f);
            area.offsetMin = new Vector2(UiLayout.BoardLeftMargin, UiLayout.BoardBottomMargin);
            area.offsetMax = new Vector2(-UiLayout.SidebarReserved, -UiLayout.BoardTopMargin);

            var go = new GameObject("BoardRoot", typeof(RectTransform), typeof(Image),
                                    typeof(BoardFitter), typeof(BoardView));
            var rect = (RectTransform)go.transform;
            rect.SetParent(area, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(UiLayout.BoardWidth, UiLayout.BoardHeight);

            var image = go.GetComponent<Image>();
            image.color = new Color(0.13f, 0.30f, 0.20f, 1f);
            image.raycastTarget = false;
        }

        private static void CreateSidebarBackground(Transform parent)
        {
            var go = new GameObject("SidebarBackground", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-(UiLayout.SidebarRightMargin - 12f), -8f);
            rect.sizeDelta = new Vector2(UiLayout.SidebarWidth + 24f, 1064f);

            var image = go.GetComponent<Image>();
            image.color = new Color(0.10f, 0.11f, 0.14f, 0.55f);
            image.raycastTarget = false;
        }

        private static T CreateSidebarItem<T>(Transform parent, string name, float y,
                                              float width, float height) where T : Component
        {
            RectTransform rect = UiFactory.CreateRightAnchored(name, parent, UiLayout.SidebarRightMargin,
                                                               y, width, height);
            return rect.gameObject.AddComponent<T>();
        }

        private static T CreateStretched<T>(Transform parent, string name) where T : Component
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return go.AddComponent<T>();
        }

        private static void AddToBuildSettings()
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(
                EditorBuildSettings.scenes);
            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path == ScenePath)
                {
                    scenes[i].enabled = true;
                    EditorBuildSettings.scenes = scenes.ToArray();
                    return;
                }
            }

            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
