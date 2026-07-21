using System.Collections.Generic;
using Scavenger.Diagnostics;
using Scavenger.Loot;
using Scavenger.Player;
using Scavenger.Run;
using Scavenger.Segment;
using Scavenger.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Scavenger
{
    /// <summary>
    /// 그레이박스 부트스트랩. 빈 씬의 오브젝트 하나에 붙이면 전체 시스템을 코드로 조립한다.
    /// 씬 에셋 의존을 최소화하기 위한 v0.1 전용 구성 - 정식 씬 구성은 트랙 C에서.
    /// 단계(S1..S7)가 진행되며 조립 대상이 늘어난다.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        [Header("비우면 기본값으로 생성 (그레이박스 편의)")]
        [SerializeField] RunSettings runSettings;
        [SerializeField] SegmentDefinition segmentDefinition;
        [SerializeField] DepthCurve depthCurve;

        static GameBootstrap active;

        RunManager runManager;
        SegmentSpawner segmentSpawner;
        PlayerController player;
        FollowCamera followCamera;
        bool worldInitialized;

        static readonly Vector3 PlayerStart = new Vector3(0f, 0.05f, 1.5f);

        void Awake()
        {
            // 씬에 부트스트랩이 둘이면 두 번째는 조립을 시작하지 않는다
            // (반쯤 조립된 시스템 NRE 방지 - Codex 검토 반영)
            if (active != null && active != this)
            {
                UnityEngine.Debug.LogError("[Bootstrap] Duplicate GameBootstrap - destroying this one.");
                Destroy(gameObject);
                return;
            }

            active = this;

            BuildRunSystems();
            BuildWorldSystems();

            runManager.RunStarted += OnRunStarted;
        }

        void Start()
        {
            runManager.StartRun();
        }

        void Update()
        {
            HandleContinueInput();
        }

        void OnDestroy()
        {
            if (active == this)
                active = null;

            if (runManager != null)
                runManager.RunStarted -= OnRunStarted;
        }

        // -- 조립 ----------------------------------------------------------

        void BuildRunSystems()
        {
            GameObject systems = new GameObject("RunSystems");
            systems.transform.SetParent(transform);

            runManager = systems.AddComponent<RunManager>();
            systems.AddComponent<RunDebugDashboard>();

            RunSettlement settlement = systems.AddComponent<RunSettlement>();
            settlement.Attach(runManager);

            if (runSettings == null)
                runSettings = RunSettings.CreateDefault();

            runManager.Configure(runSettings);
        }

        void BuildWorldSystems()
        {
            if (segmentDefinition == null)
                segmentDefinition = SegmentDefinition.CreateDefault();

            if (depthCurve == null)
                depthCurve = DepthCurve.CreateDefault();

            List<LootDefinition> lootCatalog = LootCatalog.CreateDefaults();

            GameObject spawnerObject = new GameObject("SegmentSpawner");
            spawnerObject.transform.SetParent(transform);
            segmentSpawner = spawnerObject.AddComponent<SegmentSpawner>();
            segmentSpawner.Configure(segmentDefinition, lootCatalog, depthCurve);

            player = PlayerFactory.Create(PlayerStart);
            player.Motor.corridorHalfWidth = segmentDefinition.corridorHalfWidth;

            HudOverlay hud = gameObject.AddComponent<HudOverlay>();
            hud.Player = player;

            followCamera = BuildCamera();
            followCamera.target = player.transform;
            followCamera.SnapAndLook();

            BuildAtmosphere(followCamera.GetComponent<Camera>());
        }

        // 원경 깊이감: 멀수록 어둠에 잠기는 리니어 포그 (ADR-0003)
        static void BuildAtmosphere(Camera sceneCamera)
        {
            Color depthColor = new Color(0.045f, 0.055f, 0.085f);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 18f;
            RenderSettings.fogEndDistance = 65f;
            RenderSettings.fogColor = depthColor;

            if (sceneCamera == null)
                return;

            sceneCamera.clearFlags = CameraClearFlags.SolidColor;
            sceneCamera.backgroundColor = depthColor;
            sceneCamera.farClipPlane = 90f;
        }

        static FollowCamera BuildCamera()
        {
            Camera sceneCamera = Camera.main;

            // 빈 씬이면 카메라부터 생성
            if (sceneCamera == null)
            {
                GameObject cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                sceneCamera = cameraObject.AddComponent<Camera>();
            }

            FollowCamera follow = sceneCamera.GetComponent<FollowCamera>();

            if (follow == null)
                follow = sceneCamera.gameObject.AddComponent<FollowCamera>();

            return follow;
        }

        // -- 런 수명 주기 ----------------------------------------------------

        // 심리스 라운드 (ADR-0003): 텔레포트 없이 플레이어 현재 위치 앞으로
        // 월드를 재생성한다. 첫 라운드만 원점 기준.
        void OnRunStarted()
        {
            SignalEmitter.Clear();

            float startZ = 0f;

            if (worldInitialized)
                startZ = player.transform.position.z - 2f;
            else
                worldInitialized = true;

            segmentSpawner.DespawnAll();
            segmentSpawner.BuildSegment(runManager.Depth, startZ);

            player.ResetForNewRun();
            followCamera.SnapAndLook();
        }

        // 라운드 종료(탈출/사망) 후 클릭/스페이스 한 번으로 다음 라운드 (ADR-0003)
        void HandleContinueInput()
        {
            RunState state = runManager.StateMachine.Current;

            if (state != RunState.Extracted && state != RunState.Dead)
                return;

            if (!ReadContinuePressed())
                return;

            if (!runManager.StateMachine.TryTransition(RunState.Ready))
                return;

            runManager.StartRun();
        }

        static bool ReadContinuePressed()
        {
            Keyboard keyboard = Keyboard.current;

            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
                return true;

            Mouse mouse = Mouse.current;

            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                return true;

            return false;
        }
    }
}
