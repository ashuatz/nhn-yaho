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
    /// 그레이박스 씬 원클릭 구성. 모든 시스템/플레이어/카메라/라이팅을
    /// 씬 오브젝트로 미리 배치한다 (런타임 부트스트랩 회피 - 프로젝트 규약).
    /// 룩(카메라 각도, 포그, 플레이어 리그)을 에디트 모드에서 바로 확인 가능.
    /// </summary>
    public static class GreyboxSceneSetup
    {
        const string ScenePath = "Assets/Scenes/Greybox.unity";

        static readonly Color DepthColor = new Color(0.045f, 0.055f, 0.085f);

        [MenuItem("Scavenger/Setup Greybox Scene")]
        public static void SetupScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            RunManager runManager = BuildRunSystems();
            SegmentSpawner spawner = BuildSpawner();
            PlayerController player = BuildPlayer();
            FollowCamera followCamera = BuildCamera(player.transform);
            BuildLight();
            BuildFlow(runManager, spawner, player, followCamera);

            ApplyAtmosphere();

            EditorSceneManager.SaveScene(scene, ScenePath);

            UnityEngine.Debug.Log($"[Setup] Greybox scene saved: {ScenePath}. Play를 눌러 실행.");
        }

        static RunManager BuildRunSystems()
        {
            GameObject systems = new GameObject("RunSystems");

            // RequireComponent가 RunStateMachine/RunTimer를 자동 부착
            RunManager runManager = systems.AddComponent<RunManager>();
            systems.AddComponent<RunSettlement>();
            systems.AddComponent<RunDebugDashboard>();

            return runManager;
        }

        static SegmentSpawner BuildSpawner()
        {
            GameObject spawnerObject = new GameObject("SegmentSpawner");
            return spawnerObject.AddComponent<SegmentSpawner>();
        }

        static PlayerController BuildPlayer()
        {
            GameObject root = new GameObject("Player");
            root.transform.position = new Vector3(0f, 0.05f, 1.5f);

            CharacterController controller = root.AddComponent<CharacterController>();
            controller.height = 1.6f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 0.8f, 0f);

            root.AddComponent<PlayerMotor>();
            PlayerController player = root.AddComponent<PlayerController>();

            BuildPlayerVisual(root.transform);

            return player;
        }

        // 비주얼: 큐브 2개 (머리 + 몸) - 로직 루트와 분리해 트랙 B에서 교체 가능
        static void BuildPlayerVisual(Transform parent)
        {
            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(parent, false);

            GameObject body = CreateVisualCube(visual.transform, "Body");
            body.transform.localScale = new Vector3(0.7f, 0.9f, 0.45f);
            body.transform.localPosition = new Vector3(0f, 0.65f, 0f);
            Tint(body, new Color(0.8f, 0.6f, 0.2f));

            GameObject head = CreateVisualCube(visual.transform, "Head");
            head.transform.localScale = new Vector3(0.45f, 0.45f, 0.45f);
            head.transform.localPosition = new Vector3(0f, 1.35f, 0f);
            Tint(head, new Color(0.9f, 0.75f, 0.6f));
        }

        static FollowCamera BuildCamera(Transform target)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";

            Camera sceneCamera = cameraObject.AddComponent<Camera>();
            sceneCamera.clearFlags = CameraClearFlags.SolidColor;
            sceneCamera.backgroundColor = DepthColor;
            sceneCamera.farClipPlane = 90f;
            cameraObject.AddComponent<AudioListener>();

            FollowCamera follow = cameraObject.AddComponent<FollowCamera>();
            follow.target = target;
            follow.SnapAndLook();

            return follow;
        }

        static void BuildLight()
        {
            GameObject lightObject = new GameObject("Directional Light");
            Light directional = lightObject.AddComponent<Light>();
            directional.type = LightType.Directional;
            directional.intensity = 1.1f;
            lightObject.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
        }

        static void BuildFlow(
            RunManager runManager, SegmentSpawner spawner, PlayerController player, FollowCamera followCamera)
        {
            GameObject flowObject = new GameObject("GameFlow");
            GameFlow flow = flowObject.AddComponent<GameFlow>();
            flowObject.AddComponent<HudOverlay>();

            // 직렬화 필드 배선 - 런타임 자동 탐색은 폴백일 뿐
            SerializedObject serialized = new SerializedObject(flow);
            serialized.FindProperty("runManager").objectReferenceValue = runManager;
            serialized.FindProperty("segmentSpawner").objectReferenceValue = spawner;
            serialized.FindProperty("player").objectReferenceValue = player;
            serialized.FindProperty("followCamera").objectReferenceValue = followCamera;
            serialized.ApplyModifiedPropertiesWithoutUndo();
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
