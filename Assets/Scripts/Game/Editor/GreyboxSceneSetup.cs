using Scavenger;
using Scavenger.Diagnostics;
using Scavenger.Player;
using Scavenger.Run;
using Scavenger.Segment;
using Scavenger.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
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

        static readonly Color DepthColor = new Color(0.045f, 0.055f, 0.085f);

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

            BuildLight();
            WireSceneReferences(runSystems, spawner, player, cameraObject, flow);
            ApplyAtmosphere();

            EditorSceneManager.SaveScene(scene, ScenePath);

            UnityEngine.Debug.Log($"[Setup] Greybox scene saved: {ScenePath}. Play를 눌러 실행.");
        }

        // -- 프리팹 보장 (없을 때만 생성, 기존 프리팹은 불변) -------------------

        [MenuItem("Scavenger/Ensure Prefabs")]
        public static void EnsureAllPrefabs()
        {
            if (!AssetDatabase.IsValidFolder(PrefabFolder))
                AssetDatabase.CreateFolder("Assets", "Prefabs");

            EnsurePrefab(CameraPrefabPath, BuildCameraTemplate);
            EnsurePrefab(PlayerPrefabPath, BuildPlayerTemplate);
            EnsurePrefab(RunSystemsPrefabPath, BuildRunSystemsTemplate);
            EnsurePrefab(SpawnerPrefabPath, BuildSpawnerTemplate);
            EnsurePrefab(GameFlowPrefabPath, BuildGameFlowTemplate);

            AssetDatabase.SaveAssets();
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

            Camera sceneCamera = cameraObject.AddComponent<Camera>();
            sceneCamera.clearFlags = CameraClearFlags.SolidColor;
            sceneCamera.backgroundColor = DepthColor;
            sceneCamera.farClipPlane = 90f;

            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<FollowCamera>();

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

            // 비주얼: 큐브 2개 (머리 + 몸) - 로직 루트와 분리해 트랙 B에서 교체 가능
            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);

            GameObject body = CreateVisualCube(visual.transform, "Body");
            body.transform.localScale = new Vector3(0.7f, 0.9f, 0.45f);
            body.transform.localPosition = new Vector3(0f, 0.65f, 0f);
            Tint(body, new Color(0.8f, 0.6f, 0.2f));

            GameObject head = CreateVisualCube(visual.transform, "Head");
            head.transform.localScale = new Vector3(0.45f, 0.45f, 0.45f);
            head.transform.localPosition = new Vector3(0f, 1.35f, 0f);
            Tint(head, new Color(0.9f, 0.75f, 0.6f));

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
            systems.AddComponent<RunDebugDashboard>();

            return systems;
        }

        static GameObject BuildSpawnerTemplate()
        {
            GameObject spawnerObject = new GameObject("SegmentSpawner");

            // 배경 인스턴스 렌더러 (ADR-0005) - 스포너와 같은 오브젝트에 상주
            spawnerObject.AddComponent<EnvironmentRenderer>();
            spawnerObject.AddComponent<SegmentSpawner>();

            return spawnerObject;
        }

        static GameObject BuildGameFlowTemplate()
        {
            GameObject flowObject = new GameObject("GameFlow");
            flowObject.AddComponent<GameFlow>();
            flowObject.AddComponent<HudOverlay>();
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
            serialized.FindProperty("segmentSpawner").objectReferenceValue = spawner.GetComponent<SegmentSpawner>();
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

        // 원경 깊이감: 멀수록 어둠에 잠기는 리니어 포그 (ADR-0003).
        // RenderSettings는 씬에 저장되므로 에디트 모드에서 룩 확인 가능
        static void ApplyAtmosphere()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 18f;
            RenderSettings.fogEndDistance = 65f;
            RenderSettings.fogColor = DepthColor;
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

        static void Tint(GameObject cube, Color color)
        {
            Renderer cubeRenderer = cube.GetComponent<Renderer>();

            if (cubeRenderer == null)
                return;

            // 에디트 모드에서는 sharedMaterial 인스턴스를 새로 만들어 틴트
            Material material = new Material(cubeRenderer.sharedMaterial);
            material.color = color;
            cubeRenderer.sharedMaterial = material;
        }
    }
}
