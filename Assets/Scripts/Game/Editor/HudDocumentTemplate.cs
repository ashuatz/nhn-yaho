using Scavenger.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Scavenger.EditorTools
{
    /// <summary>
    /// UI Toolkit HUD 자산 생성 (사용자 지시 2026-07-25: HUD를 UI Toolkit으로 전환).
    /// 프로젝트 규약대로 런타임 생성이 아니라 에디터에서 1회 셋업한다 -
    /// UIDocument/PanelSettings/프리팹을 만들고 배선까지 마친다.
    ///
    /// 레이아웃은 Assets/UI/Hud.uxml, 스타일은 Hud.uss가 소유한다 (코드는 수치만 넣는다).
    /// 있으면 덮어쓰지 않는다 - 다듬은 결과가 메뉴 재실행으로 사라지지 않게.
    /// </summary>
    public static class HudDocumentTemplate
    {
        public const string UiFolder = "Assets/UI";
        public const string UxmlPath = "Assets/UI/Hud.uxml";
        public const string UssPath = "Assets/UI/Hud.uss";
        public const string ThemePath = "Assets/UI/UnityDefaultRuntimeTheme.tss";
        public const string PanelSettingsPath = "Assets/UI/HudPanelSettings.asset";
        public const string PrefabPath = "Assets/Prefabs/HudDocument.prefab";

        /// <summary>진단 대시보드 레이아웃 (개발 빌드 전용 패널, HUD 문서에 얹힌다).</summary>
        public const string DashboardUxmlPath = "Assets/UI/Dashboard.uxml";

        // 기준 해상도 - 세로가 짧은 화면에서도 HUD가 잘리지 않게 폭/높이 절충
        static readonly Vector2Int ReferenceResolution = new Vector2Int(1920, 1080);

        [MenuItem("Scavenger/Ensure HUD Document (UI Toolkit)")]
        public static void EnsureHudDocument()
        {
            if (!EnsureSourceAssets())
                return;

            PanelSettings settings = EnsurePanelSettings();
            EnsurePrefab(settings);
            EnsureDashboardView();

            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// 진단 대시보드 뷰를 HUD 프리팹에 보강한다. 프리팹을 덮어쓰지 않고 컴포넌트만
        /// 더하는 마이그레이션 경로다 - 기존 HUD 프리팹에도 대시보드가 붙는다.
        /// 레이아웃 참조가 비어 있을 때만 채운다 (사용자가 다른 UXML을 물릴 수 있게).
        /// </summary>
        static void EnsureDashboardView()
        {
            VisualTreeAsset dashboardTree =
                AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(DashboardUxmlPath);

            if (dashboardTree == null)
            {
                UnityEngine.Debug.LogWarning(
                    $"[Setup] {DashboardUxmlPath} 가 없다. 진단 대시보드는 붙이지 않는다.");
                return;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

            if (prefab == null)
                return;

            GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);

            // 예외가 나도 반드시 언로드 - 프리팹 스테이지 잔존 방지
            try
            {
                Diagnostics.RunDashboardView view =
                    contents.GetComponent<Diagnostics.RunDashboardView>();

                bool changed = false;

                if (view == null)
                {
                    view = contents.AddComponent<Diagnostics.RunDashboardView>();
                    changed = true;
                }

                if (view.dashboardTree == null)
                {
                    view.dashboardTree = dashboardTree;
                    changed = true;
                }

                if (!changed)
                    return;

                PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
                UnityEngine.Debug.Log($"[Setup] 진단 대시보드 뷰 배선: {PrefabPath}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        /// <summary>
        /// UXML/USS/테마가 프로젝트에 있는지 확인한다. 이 세 개는 사람이 편집하는
        /// 텍스트 자산이라 코드로 생성하지 않고, 없으면 안내만 한다.
        /// </summary>
        static bool EnsureSourceAssets()
        {
            if (AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath) == null)
            {
                UnityEngine.Debug.LogError(
                    $"[Setup] {UxmlPath} 가 없다. HUD 레이아웃 파일이 있어야 셋업할 수 있다.");
                return false;
            }

            if (AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath) == null)
            {
                UnityEngine.Debug.LogError(
                    $"[Setup] {ThemePath} 가 없다. 런타임 테마(.tss)가 있어야 패널이 렌더된다.");
                return false;
            }

            return true;
        }

        static PanelSettings EnsurePanelSettings()
        {
            PanelSettings existing = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);

            if (existing != null)
                return existing;

            PanelSettings settings = ScriptableObject.CreateInstance<PanelSettings>();

            settings.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);

            // 해상도에 따라 스케일 (모바일 대응 - 기존 uGUI 캔버스와 같은 정책)
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = ReferenceResolution;
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f;

            AssetDatabase.CreateAsset(settings, PanelSettingsPath);
            UnityEngine.Debug.Log($"[Setup] 패널 설정 생성: {PanelSettingsPath}");

            return settings;
        }

        static void EnsurePrefab(PanelSettings settings)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
                return;

            GameObject root = new GameObject("HudDocument");

            UIDocument document = root.AddComponent<UIDocument>();
            document.panelSettings = settings;
            document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);

            // 패널 갱신 / 조이스틱 / 월드 라벨 - 서로 의존하지 않고 같은 문서를 조회한다
            root.AddComponent<HudView>();
            root.AddComponent<HudJoystickView>();
            root.AddComponent<HudLootLabelView>();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            UnityEngine.Debug.Log($"[Setup] HUD 문서 프리팹 생성: {PrefabPath}");
        }
    }
}
