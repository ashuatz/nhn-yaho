using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Scavenger.EditorTools
{
    /// <summary>
    /// 그레이박스 하늘 셋업 (사용자 지시 2026-07-26: 플레이 영역 아래/뒤가 뻥 뚫려 보인다).
    ///
    /// 카메라가 단색(Solid Color)으로 지우고 있어서 지형이 끝나는 곳이 "빈 화면"으로
    /// 읽혔다. 지평선 쪽은 포그색, 위로 갈수록 조금 어두운 그라디언트 하늘을 깔면
    /// 같은 밝기라도 "대기"로 읽힌다.
    ///
    /// LUT 포그가 하늘 픽셀을 skyDensity(기본 0.85)만큼 자기 색으로 덮으므로
    /// 하늘은 은은하게만 남는다 - 그래서 강한 그림 대신 그라디언트를 쓴다.
    /// 아트 파노라마가 나오면 이 머티리얼의 셰이더만 Skybox/Panoramic으로 바꾸면 된다.
    /// </summary>
    public static class GreyboxSkySetup
    {
        public const string SkyMaterialPath = "Assets/Materials/Sky/GreyboxSky.mat";

        const string SkyFolder = "Assets/Materials/Sky";
        const string CameraPrefabPath = "Assets/Prefabs/Main Camera.prefab";

        // 지평선 = 포그가 삼키는 색과 같은 톤, 위로 갈수록 깊어진다
        static readonly Color HorizonColor = new Color(0.72f, 0.75f, 0.8f);
        static readonly Color SkyTint = new Color(0.5f, 0.56f, 0.66f);

        // 대기 두께 - 값이 크면 지평선 띠가 두꺼워진다 (포그와 이어지게 조금 두껍게)
        const float AtmosphereThickness = 0.75f;
        const float SkyExposure = 1.05f;

        [MenuItem("Scavenger/Ensure Greybox Sky")]
        public static void EnsureGreyboxSky()
        {
            // 플레이 중에는 씬을 더럽힐 수 없고, 바꿔도 플레이가 끝나면 되돌아간다
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                UnityEngine.Debug.LogWarning("[Sky] 플레이 모드에서는 하늘 셋업을 건너뛴다.");
                return;
            }

            Material sky = EnsureSkyMaterial();

            if (sky == null)
                return;

            RenderSettings.skybox = sky;

            ApplyToCameraPrefab();
            ApplyToSceneCameras();

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkAllScenesDirty();

            UnityEngine.Debug.Log($"[Sky] 하늘 셋업 완료: {SkyMaterialPath}");
        }

        /// <summary>
        /// 하늘 머티리얼. 있으면 덮어쓰지 않는다 - 아트가 다듬은 값이
        /// 메뉴 재실행으로 사라지지 않게 (프로젝트 규약).
        /// </summary>
        public static Material EnsureSkyMaterial()
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(SkyMaterialPath);

            if (existing != null)
                return existing;

            Shader shader = Shader.Find("Skybox/Procedural");

            if (shader == null)
            {
                UnityEngine.Debug.LogError("[Sky] Skybox/Procedural 셰이더를 찾지 못했다.");
                return null;
            }

            if (!AssetDatabase.IsValidFolder("Assets/Materials"))
                AssetDatabase.CreateFolder("Assets", "Materials");

            if (!AssetDatabase.IsValidFolder(SkyFolder))
                AssetDatabase.CreateFolder("Assets/Materials", "Sky");

            Material sky = new Material(shader);

            // 해 없음 - 그레이박스에 태양 원반이 뜨면 아이소 화면에서 눈에 거슬린다
            sky.SetFloat("_SunDisk", 0f);
            sky.SetFloat("_AtmosphereThickness", AtmosphereThickness);
            sky.SetFloat("_Exposure", SkyExposure);
            sky.SetColor("_SkyTint", SkyTint);
            sky.SetColor("_GroundColor", HorizonColor);

            AssetDatabase.CreateAsset(sky, SkyMaterialPath);
            UnityEngine.Debug.Log($"[Sky] 하늘 머티리얼 생성: {SkyMaterialPath}");

            return sky;
        }

        /// <summary>
        /// 카메라 프리팹의 지우기 방식을 스카이박스로 바꾼다.
        /// 단색으로 두면 하늘이 그려지지 않아 지형 밖이 다시 빈 화면이 된다.
        /// </summary>
        static void ApplyToCameraPrefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CameraPrefabPath);

            if (prefab == null)
                return;

            GameObject contents = PrefabUtility.LoadPrefabContents(CameraPrefabPath);

            // 예외가 나도 반드시 언로드 - 프리팹 스테이지 잔존 방지
            try
            {
                Camera camera = contents.GetComponent<Camera>();

                if (camera == null || camera.clearFlags == CameraClearFlags.Skybox)
                    return;

                camera.clearFlags = CameraClearFlags.Skybox;

                PrefabUtility.SaveAsPrefabAsset(contents, CameraPrefabPath);
                UnityEngine.Debug.Log($"[Sky] 카메라 프리팹 지우기 방식 변경: {CameraPrefabPath}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        // 열려 있는 씬의 카메라 인스턴스도 함께 맞춘다 (프리팹 오버라이드가 있을 수 있다)
        static void ApplyToSceneCameras()
        {
            Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);

            foreach (Camera camera in cameras)
            {
                if (camera.clearFlags == CameraClearFlags.Skybox)
                    continue;

                camera.clearFlags = CameraClearFlags.Skybox;
                EditorUtility.SetDirty(camera);
            }
        }
    }
}
