using System.Collections.Generic;
using Scavenger;
using Scavenger.Player;
using Scavenger.Run;
using Scavenger.Segment;
using Scavenger.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Scavenger.EditorTools
{
    /// <summary>
    /// 그레이박스 씬 원클릭 구성. 시스템은 전부 프리팹 (Assets/Prefabs/)으로 관리하고
    /// 씬에는 프리팹 인스턴스를 배치한다 - 사용자가 프리팹을 직접 수정/튜닝 가능.
    /// 프리팹이 없으면 기본 템플릿으로 1회 생성, 이미 있으면 절대 덮어쓰지 않는다.
    /// </summary>
    public static class GreyboxSceneSetup
    {
        const string ScenePath = "Assets/Scenes/Greybox.unity";
        const string PrefabFolder = "Assets/Prefabs";

        const string CameraPrefabPath = "Assets/Prefabs/Main Camera.prefab";
        const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
        const string RunSystemsPrefabPath = "Assets/Prefabs/RunSystems.prefab";
        const string SpawnerPrefabPath = "Assets/Prefabs/SegmentSpawner.prefab";
        const string GameFlowPrefabPath = "Assets/Prefabs/GameFlow.prefab";
        const string HudCanvasPrefabPath = "Assets/Prefabs/HudCanvas.prefab";

        static readonly Color DepthColor = new Color(0.045f, 0.055f, 0.085f);

        // 이름 있는 그레이박스 머티리얼의 기준색 (색상 + 채도).
        // 밝기는 GreyboxTheme이 정한다 - 밝기 창(Greybox Brightness)이 이 값들로 다시 만든다
        public static readonly Color CommonBaseColor = new Color(0.3f, 0.31f, 0.33f);
        public static readonly Color PlayerBodyBaseColor = new Color(0.8f, 0.6f, 0.2f);
        public static readonly Color PlayerHeadBaseColor = new Color(0.9f, 0.75f, 0.6f);

        [MenuItem("Scavenger/Setup Greybox Scene")]
        public static void SetupScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EnsureAllPrefabs();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject runSystems = InstantiatePrefab(RunSystemsPrefabPath);
            GameObject spawner = InstantiatePrefab(SpawnerPrefabPath);
            GameObject player = InstantiatePrefab(PlayerPrefabPath);
            GameObject cameraObject = InstantiatePrefab(CameraPrefabPath);
            GameObject flow = InstantiatePrefab(GameFlowPrefabPath);

            // HUD는 UI Toolkit 문서 (사용자 지시 2026-07-25). uGUI HudCanvas는 배치하지
            // 않는다 - 둘을 함께 두면 같은 정보가 두 번 그려진다.
            // 프리팹 파일은 롤백용으로 남겨 둔다
            InstantiatePrefab(HudDocumentTemplate.PrefabPath);

            BuildLight();
            WireSceneReferences(runSystems, spawner, player, cameraObject, flow);
            ApplyAtmosphere();
            BuildTiltShiftVolume(cameraObject);

            EditorSceneManager.SaveScene(scene, ScenePath);

            UnityEngine.Debug.Log($"[Setup] Greybox scene saved: {ScenePath}. Play를 눌러 실행.");
        }

        // -- 프리팹 보장 (없을 때만 생성, 기존 프리팹은 불변) -------------------

        [MenuItem("Scavenger/Ensure Prefabs")]
        public static void EnsureAllPrefabs()
        {
            if (!AssetDatabase.IsValidFolder(PrefabFolder))
                AssetDatabase.CreateFolder("Assets", "Prefabs");

            // 필드 리소스 프리팹(존 바닥/파밍 포인트/아이템 오브젝트)을 먼저 -
            // 스포너가 참조한다
            FieldPrefabTemplates.EnsureFieldPrefabs();
            FarmingObjectTemplates.EnsureFarmingObjectPrefabs();

            EnsurePrefab(CameraPrefabPath, BuildCameraTemplate);
            EnsurePrefab(PlayerPrefabPath, BuildPlayerTemplate);
            EnsurePrefab(RunSystemsPrefabPath, BuildRunSystemsTemplate);
            EnsurePrefab(SpawnerPrefabPath, BuildSpawnerTemplate);
            EnsurePrefab(GameFlowPrefabPath, BuildGameFlowTemplate);
            EnsurePrefab(HudCanvasPrefabPath, HudCanvasTemplate.Build);

            // UI Toolkit HUD (현행). uGUI HudCanvas는 위에서 유지만 한다 (롤백 경로)
            HudDocumentTemplate.EnsureHudDocument();

            RepairPrefabs();

            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// 기존 프리팹 제자리 수리. 삭제된 스크립트 참조(missing script)를 걷어내고
        /// 교체된 시스템 컴포넌트를 보강한다 - 사용자가 튜닝한 값은 보존된다.
        /// EnsurePrefab이 기존 프리팹을 덮어쓰지 않으므로 이 경로가 마이그레이션을 담당.
        /// </summary>
        [MenuItem("Scavenger/Repair Prefabs")]
        public static void RepairPrefabs()
        {
            string[] paths =
            {
                CameraPrefabPath, PlayerPrefabPath, RunSystemsPrefabPath,
                SpawnerPrefabPath, GameFlowPrefabPath, HudCanvasPrefabPath,
            };

            foreach (string path in paths)
                RepairPrefab(path);

            AssetDatabase.SaveAssets();
        }

        static void RepairPrefab(string path)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab == null)
                return;

            GameObject contents = PrefabUtility.LoadPrefabContents(path);

            // 예외가 나도 반드시 언로드 - 프리팹 스테이지 잔존 방지 (Codex 교차 검토)
            try
            {
                int changes = 0;

                // 삭제된 시스템(PlayerStamina / CollapseFront / DangerGrid /
                // SegmentSpawner / HudOverlay 등)이 남긴 missing script 정리
                foreach (Transform child in contents.GetComponentsInChildren<Transform>(true))
                    changes += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);

                // 조이스틱은 M5-1에서 HudCanvas 프리팹으로 이사했다
                if (path == GameFlowPrefabPath)
                {
                    foreach (VirtualJoystick joystick in contents.GetComponentsInChildren<VirtualJoystick>(true))
                    {
                        Object.DestroyImmediate(joystick, true);
                        changes += 1;
                    }
                }

                // 존 생성기 교체 (ADR-0009) - 구 SegmentSpawner 자리를 FieldSpawner가 받는다
                if (path == SpawnerPrefabPath)
                {
                    changes += EnsureSpawnerComponents(contents);
                    changes += WireFieldSpawnerResources(contents.GetComponent<Field.FieldSpawner>());
                }

                if (changes == 0)
                    return;

                PrefabUtility.SaveAsPrefabAsset(contents, path);
                UnityEngine.Debug.Log($"[Setup] 프리팹 수리: {path} (변경 {changes}건)");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        /// <summary>
        /// 필드 스포너의 리소스 참조를 배선한다 (비어 있을 때만 - 아트 교체 보존).
        /// 기준 머티리얼은 Assets/Materials/Greybox/Common.mat, 지형은 Field 프리팹.
        /// </summary>
        static int WireFieldSpawnerResources(Field.FieldSpawner spawner)
        {
            if (spawner == null)
                return 0;

            SerializedObject serialized = new SerializedObject(spawner);
            int assigned = 0;

            assigned += AssignIfEmpty(serialized, "greyboxMaterial", LoadCommonMaterial());
            assigned += AssignIfEmpty(serialized, "greyboxTheme", GreyboxThemeAccess.Load());
            assigned += AssignIfEmpty(
                serialized, "floorTileSet",
                AssetDatabase.LoadAssetAtPath<Field.FieldTileSet>(FieldPrefabTemplates.TileSetPath));
            assigned += AssignIfEmpty(
                serialized, "floorRowPrefab", LoadFieldComponent<Field.FloorRow>(
                    FieldPrefabTemplates.FloorRowPath));
            assigned += AssignIfEmpty(
                serialized, "farmingPointTopPrefab", LoadFieldComponent<Field.FarmingPoint>(
                    FieldPrefabTemplates.FarmingPointTopPath));
            assigned += AssignIfEmpty(
                serialized, "farmingPointBottomPrefab", LoadFieldComponent<Field.FarmingPoint>(
                    FieldPrefabTemplates.FarmingPointBottomPath));

            // 아이템 오브젝트는 목록이라 비어 있을 때만 통째로 채운다 (파밍 문서 3.2)
            assigned += AssignListIfEmpty(
                serialized, "farmingObjectPrefabs", FarmingObjectTemplates.LoadAll());

            if (assigned > 0)
                serialized.ApplyModifiedPropertiesWithoutUndo();

            return assigned;
        }

        static int AssignIfEmpty(SerializedObject serialized, string propertyName, Object value)
        {
            if (value == null)
                return 0;

            SerializedProperty property = serialized.FindProperty(propertyName);

            if (property == null || property.objectReferenceValue != null)
                return 0;

            property.objectReferenceValue = value;
            return 1;
        }

        /// <summary>
        /// 목록 프로퍼티를 채운다. 이미 항목이 하나라도 있으면 건드리지 않는다 -
        /// 사용자가 고른 구성을 메뉴 재실행이 되돌리지 않게 하는 것이 목적.
        /// </summary>
        static int AssignListIfEmpty<T>(
            SerializedObject serialized, string propertyName, List<T> values) where T : Object
        {
            if (values == null || values.Count == 0)
                return 0;

            SerializedProperty property = serialized.FindProperty(propertyName);

            if (property == null || !property.isArray)
                return 0;

            if (property.arraySize > 0)
                return 0;

            property.arraySize = values.Count;

            for (int i = 0; i < values.Count; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];

            return 1;
        }

        static Material LoadCommonMaterial()
        {
            const string commonPath = "Assets/Materials/Greybox/Common.mat";

            Material common = AssetDatabase.LoadAssetAtPath<Material>(commonPath);

            if (common != null)
                return common;

            // 없으면 그레이박스 기본 톤으로 1회 생성
            return GreyboxMaterials.Ensure(
                "Common", CommonBaseColor, subfolder: null, Field.GreyboxTone.Field);
        }

        static T LoadFieldComponent<T>(string prefabPath) where T : Component
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            if (prefab == null)
                return null;

            return prefab.GetComponent<T>();
        }

        // 스포너 프리팹에 필요한 컴포넌트를 보강한다 (없을 때만 추가 - 튜닝 보존)
        static int EnsureSpawnerComponents(GameObject contents)
        {
            int added = 0;

            if (contents.GetComponent<EnvironmentRenderer>() == null)
            {
                contents.AddComponent<EnvironmentRenderer>();
                added += 1;
            }

            if (contents.GetComponent<Field.FieldSpawner>() == null)
            {
                contents.AddComponent<Field.FieldSpawner>();
                added += 1;
            }

            if (contents.GetComponent<DepthLighting>() == null)
            {
                contents.AddComponent<DepthLighting>();
                added += 1;
            }

            return added;
        }

        static void EnsurePrefab(string path, System.Func<GameObject> buildTemplate)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (existing != null)
                return;

            GameObject template = buildTemplate();
            PrefabUtility.SaveAsPrefabAsset(template, path);
            Object.DestroyImmediate(template);

            UnityEngine.Debug.Log($"[Setup] Prefab created: {path}");
        }

        static GameObject InstantiatePrefab(string path)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            return (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        }

        // -- 프리팹 템플릿 (최초 1회만 사용) -----------------------------------

        static GameObject BuildCameraTemplate()
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";

            // 하늘로 지운다 - 단색이면 지형이 끝나는 곳이 빈 화면으로 읽힌다
            // (사용자 지시 2026-07-26). 하늘 머티리얼은 GreyboxSkySetup이 만든다
            Camera sceneCamera = cameraObject.AddComponent<Camera>();
            sceneCamera.clearFlags = CameraClearFlags.Skybox;
            sceneCamera.backgroundColor = DepthColor;

            cameraObject.AddComponent<AudioListener>();

            // FollowCamera 기본값(pitch30/yaw-45/fov18)이 아이소 리그를 이미 담고 있다.
            // 투영/줌/클립 평면은 FollowCamera가 매 프레임 강제하므로 여기서 잡지 않는다
            // (두 곳에서 잡으면 어느 쪽이 이기는지가 실행 순서에 달린다)
            cameraObject.AddComponent<FollowCamera>();

            // 피격/위협 피드백 쉐이크. 수치는 프리팹에서 튜닝
            cameraObject.AddComponent<CameraShake>();

            return cameraObject;
        }

        static GameObject BuildPlayerTemplate()
        {
            GameObject root = new GameObject("Player");

            CharacterController controller = root.AddComponent<CharacterController>();
            controller.height = 1.6f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 0.8f, 0f);

            root.AddComponent<PlayerMotor>();
            root.AddComponent<PlayerController>();

            // 무게 과적 -> 이동속도 배율 (M2-1). 임계/배율은 프리팹에서 튜닝
            root.AddComponent<CarryLoad>();

            // 체력 (HP). 수치는 프리팹에서 튜닝. 스태미나는 별도 문서 확정 후 도입
            root.AddComponent<PlayerHealth>();

            // 비주얼: 큐브 2개 (머리 + 몸) - 로직 루트와 분리해 트랙 B에서 교체 가능
            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);

            // 머티리얼은 반드시 디스크 에셋 - 인메모리 머티리얼은 프리팹 저장 시
            // 참조가 깨져 마젠타가 된다 (실제 발생, GreyboxMaterials 참조)
            GameObject body = CreateVisualCube(visual.transform, "Body");
            body.transform.localScale = new Vector3(0.7f, 0.9f, 0.45f);
            body.transform.localPosition = new Vector3(0f, 0.65f, 0f);
            AssignMaterial(body, GreyboxMaterials.Ensure(
                "PlayerBody", PlayerBodyBaseColor, subfolder: null, Field.GreyboxTone.Character));

            GameObject head = CreateVisualCube(visual.transform, "Head");
            head.transform.localScale = new Vector3(0.45f, 0.45f, 0.45f);
            head.transform.localPosition = new Vector3(0f, 1.35f, 0f);
            AssignMaterial(head, GreyboxMaterials.Ensure(
                "PlayerHead", PlayerHeadBaseColor, subfolder: null, Field.GreyboxTone.Character));

            // 스텝 연출 (스쿼시/스트레치 + 홉). Awake에서 Visual 자식 자동 탐색
            root.AddComponent<PlayerStepAnimator>();

            return root;
        }

        static GameObject BuildRunSystemsTemplate()
        {
            GameObject systems = new GameObject("RunSystems");

            // RequireComponent가 RunStateMachine/RunTimer를 자동 부착
            systems.AddComponent<RunManager>();
            systems.AddComponent<RunSettlement>();

            // 진단 대시보드는 HUD 문서(UI Toolkit)로 이사했다 - RunDashboardView

            return systems;
        }

        static GameObject BuildSpawnerTemplate()
        {
            GameObject spawnerObject = new GameObject("FieldSpawner");

            // 배경 인스턴스 렌더러 (ADR-0005) - 필드 스포너와 동거.
            // 기존 프리팹에는 런타임 GetComponent/AddComponent 폴백이 보강한다
            spawnerObject.AddComponent<EnvironmentRenderer>();
            spawnerObject.AddComponent<Field.FieldSpawner>();

            // 깊이별 조도 (M4-2). 수치는 프리팹에서 튜닝
            spawnerObject.AddComponent<DepthLighting>();

            // 필드 리소스 참조 배선 (기준 머티리얼 + 존/파밍 포인트 프리팹)
            WireFieldSpawnerResources(spawnerObject.GetComponent<Field.FieldSpawner>());

            return spawnerObject;
        }

        // HUD/조이스틱은 M5-1부터 HudCanvas 프리팹 소속 (uGUI)
        static GameObject BuildGameFlowTemplate()
        {
            GameObject flowObject = new GameObject("GameFlow");
            flowObject.AddComponent<GameFlow>();
            return flowObject;
        }

        // -- 씬 배선 ----------------------------------------------------------

        static void WireSceneReferences(
            GameObject runSystems, GameObject spawner, GameObject player,
            GameObject cameraObject, GameObject flow)
        {
            // 플레이어 시작 위치 (씬 인스턴스 오버라이드)
            player.transform.position = new Vector3(0f, 0.05f, 1.5f);

            // 사용자 프리팹에 FollowCamera가 없을 수도 있으므로 보강
            FollowCamera followCamera = cameraObject.GetComponent<FollowCamera>();

            if (followCamera == null)
                followCamera = cameraObject.AddComponent<FollowCamera>();

            followCamera.target = player.transform;
            followCamera.SnapAndLook();

            GameFlow gameFlow = flow.GetComponent<GameFlow>();
            SerializedObject serialized = new SerializedObject(gameFlow);
            serialized.FindProperty("runManager").objectReferenceValue = runSystems.GetComponent<RunManager>();
            serialized.FindProperty("fieldSpawner").objectReferenceValue = spawner.GetComponent<Field.FieldSpawner>();
            serialized.FindProperty("player").objectReferenceValue = player.GetComponent<PlayerController>();
            serialized.FindProperty("followCamera").objectReferenceValue = followCamera;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void BuildLight()
        {
            GameObject lightObject = new GameObject("Directional Light");
            Light directional = lightObject.AddComponent<Light>();
            directional.type = LightType.Directional;
            directional.intensity = 1.1f;
            lightObject.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
        }

        // 전방 시야 차단 포그 (M1-2, 기획 확정: 전방은 포그로 가려짐).
        // 연출용이 아니라 정보 차단용 - 다음 선택지 노드(구간 끝)가 근접 전까지
        // 식별되지 않도록 가시 한계를 짧게 잡는다. 거리 신호가 유일한 힌트가 된다.
        // RenderSettings는 씬에 저장되므로 에디트 모드에서 룩 확인 가능
        static void ApplyAtmosphere()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 10f;
            RenderSettings.fogEndDistance = 42f;
            RenderSettings.fogColor = DepthColor;

            // 하늘 (지형이 끝나는 곳이 빈 화면으로 보이지 않게)
            GreyboxSkySetup.EnsureGreyboxSky();
        }

        // 틸트 시프트 뷰 (ADR-0006): 가우시안 DoF로 미니어처 룩.
        // 초점 대역(start/end)은 프로파일 에셋에서 사용자가 튜닝
        static void BuildTiltShiftVolume(GameObject cameraObject)
        {
            const string profilePath = "Assets/Settings/TiltShiftProfile.asset";

            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);

            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, profilePath);

                DepthOfField depthOfField = profile.Add<DepthOfField>(true);
                depthOfField.mode.Override(DepthOfFieldMode.Gaussian);
                depthOfField.gaussianStart.Override(14f);
                depthOfField.gaussianEnd.Override(30f);
                depthOfField.gaussianMaxRadius.Override(1.2f);

                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssets();
            }

            GameObject volumeObject = new GameObject("TiltShift Volume");
            Volume volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;

            // 카메라 포스트 프로세싱 활성화 (씬 인스턴스 오버라이드)
            UniversalAdditionalCameraData cameraData =
                cameraObject.GetComponent<UniversalAdditionalCameraData>();

            if (cameraData == null)
                cameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>();

            cameraData.renderPostProcessing = true;
        }

        static GameObject CreateVisualCube(Transform parent, string cubeName)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = cubeName;
            cube.transform.SetParent(parent, false);

            Collider cubeCollider = cube.GetComponent<Collider>();

            if (cubeCollider != null)
                Object.DestroyImmediate(cubeCollider);

            return cube;
        }

        static void AssignMaterial(GameObject cube, Material material)
        {
            Renderer cubeRenderer = cube.GetComponent<Renderer>();

            if (cubeRenderer == null)
                return;

            cubeRenderer.sharedMaterial = material;
        }

        // (일회성 마젠타 리페어 메뉴는 사용 완료 후 제거 - 2026-07-22)
    }
}
