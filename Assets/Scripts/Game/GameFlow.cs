using System.Collections.Generic;
using Scavenger.Loot;
using Scavenger.Player;
using Scavenger.Run;
using Scavenger.Segment;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Scavenger
{
    /// <summary>
    /// 런 흐름 배선만 담당하는 씬 컴포넌트. 오브젝트 생성은 하지 않는다 -
    /// 모든 시스템은 씬에 미리 배치된다 (프로젝트 규약: 런타임 부트스트랩 회피).
    /// 씬 구성은 에디터 메뉴 Scavenger > Setup Greybox Scene 사용.
    /// </summary>
    public sealed class GameFlow : MonoBehaviour
    {
        [Header("씬 참조 (비우면 씬에서 자동 탐색)")]
        [SerializeField] RunManager runManager;
        [SerializeField] SegmentSpawner segmentSpawner;
        [SerializeField] PlayerController player;
        [SerializeField] FollowCamera followCamera;

        [Header("데이터 에셋 (비우면 기본값 생성)")]
        [SerializeField] RunSettings runSettings;
        [SerializeField] SegmentDefinition segmentDefinition;
        [SerializeField] DepthCurve depthCurve;

        bool worldInitialized;
        CollapseFront collapseFront;

        void Awake()
        {
            if (!ResolveReferences())
            {
                enabled = false;
                return;
            }

            if (runSettings == null)
                runSettings = RunSettings.CreateDefault();

            if (segmentDefinition == null)
                segmentDefinition = SegmentDefinition.CreateDefault();

            if (depthCurve == null)
                depthCurve = DepthCurve.CreateDefault();

            List<LootDefinition> lootCatalog = LootCatalog.CreateDefaults();

            runManager.Configure(runSettings);
            segmentSpawner.Configure(segmentDefinition, lootCatalog, depthCurve);
            segmentSpawner.SetViewCamera(followCamera);
            player.Motor.corridorHalfWidth = segmentDefinition.corridorHalfWidth;

            // 바닥 붕괴 (ADR-0006): 스포너와 같은 오브젝트에 상주
            collapseFront = segmentSpawner.GetComponent<CollapseFront>();

            if (collapseFront == null)
                collapseFront = segmentSpawner.gameObject.AddComponent<CollapseFront>();

            collapseFront.Track(player);

            // 가상 D-패드 (좌하단 조작계) - 기존 프리팹에는 폴백으로 보강
            if (FindFirstObjectByType<UI.VirtualDPad>() == null)
                gameObject.AddComponent<UI.VirtualDPad>();

            // 무게 과적 (M2-1) - 기존 Player 프리팹에는 폴백으로 보강
            if (player.GetComponent<CarryLoad>() == null)
                player.gameObject.AddComponent<CarryLoad>();

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
            if (runManager != null)
                runManager.RunStarted -= OnRunStarted;
        }

        bool ResolveReferences()
        {
            if (runManager == null)
                runManager = FindFirstObjectByType<RunManager>();

            if (segmentSpawner == null)
                segmentSpawner = FindFirstObjectByType<SegmentSpawner>();

            if (player == null)
                player = FindFirstObjectByType<PlayerController>();

            if (followCamera == null)
                followCamera = FindFirstObjectByType<FollowCamera>();

            if (runManager != null && segmentSpawner != null && player != null && followCamera != null)
                return true;

            UnityEngine.Debug.LogError(
                "[Flow] Missing scene systems. Run 'Scavenger > Setup Greybox Scene' to author the scene.");
            return false;
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

            // 현재 + 다음 구간을 함께 생성 - 다음 스테이지 확정 노출 (ADR-0004)
            segmentSpawner.BuildInitialChain(runManager.Depth, startZ);

            // 붕괴 전선을 플레이어 뒤로 리셋 (ADR-0006)
            collapseFront.ResetFront(player.transform.position.z - 8f);

            // 낙사 후 재개: 구멍 아래에 있으면 바닥 위로 복귀
            RecoverPlayerIfFallen(startZ);

            player.ResetForNewRun();
            followCamera.SnapAndLook();
        }

        void RecoverPlayerIfFallen(float startZ)
        {
            Vector3 position = player.transform.position;

            if (position.y >= -0.5f)
                return;

            position.y = 0.05f;
            position.x = Mathf.Clamp(
                position.x, -segmentDefinition.corridorHalfWidth, segmentDefinition.corridorHalfWidth);
            position.z = Mathf.Max(position.z, startZ + 2f);

            // CharacterController는 활성 상태에서 transform 이동을 무시하므로 잠시 끈다
            CharacterController controller = player.GetComponent<CharacterController>();
            controller.enabled = false;

            player.transform.position = position;

            controller.enabled = true;
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
