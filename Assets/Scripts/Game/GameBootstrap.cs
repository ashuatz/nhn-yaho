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

        RunManager runManager;
        SegmentSpawner segmentSpawner;
        PlayerController player;
        FollowCamera followCamera;

        static readonly Vector3 PlayerStart = new Vector3(0f, 0.05f, 1.5f);

        void Awake()
        {
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
            HandleRestartInput();
        }

        void OnDestroy()
        {
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

        void OnRunStarted()
        {
            SignalEmitter.Clear();

            segmentSpawner.DespawnAll();
            segmentSpawner.BuildSegment(runManager.Depth, 0f);

            TeleportPlayer(PlayerStart);
            player.ResetForNewRun();
            followCamera.SnapAndLook();
        }

        void TeleportPlayer(Vector3 position)
        {
            // CharacterController는 활성 상태에서 transform 이동을 무시하므로 잠시 끈다
            CharacterController controller = player.GetComponent<CharacterController>();
            controller.enabled = false;

            player.transform.position = position;

            controller.enabled = true;
        }

        // 그레이박스 편의: 런 종료(탈출/사망) 후 R로 재시작
        void HandleRestartInput()
        {
            Keyboard keyboard = Keyboard.current;

            if (keyboard == null)
                return;

            if (!keyboard.rKey.wasPressedThisFrame)
                return;

            RunState state = runManager.StateMachine.Current;

            if (state != RunState.Extracted && state != RunState.Dead)
                return;

            if (!runManager.StateMachine.TryTransition(RunState.Ready))
                return;

            runManager.StartRun();
        }
    }
}
