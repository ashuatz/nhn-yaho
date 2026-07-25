using System.Collections.Generic;
using Scavenger.Field;
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
        [SerializeField] FieldSpawner fieldSpawner;
        [SerializeField] PlayerController player;
        [SerializeField] FollowCamera followCamera;

        [Header("데이터 에셋 (비우면 기본값 생성)")]
        [SerializeField] RunSettings runSettings;
        [SerializeField] ZoneDefinition zoneDefinition;
        [SerializeField] BagDefinition bagDefinition;

        bool worldInitialized;
        CarryLoad carryLoad;

        void Awake()
        {
            if (!ResolveReferences())
            {
                enabled = false;
                return;
            }

            if (runSettings == null)
                runSettings = RunSettings.CreateDefault();

            if (zoneDefinition == null)
                zoneDefinition = ZoneDefinition.CreateDefault();

            if (bagDefinition == null)
                bagDefinition = BagDefinition.CreateDefault();

            List<LootDefinition> lootCatalog = LootCatalog.CreateDefaults();

            // 파밍 아이템은 아이템 오브젝트에서만 나오는 고가치 타입 (파밍 문서 4장)
            List<LootDefinition> farmingCatalog = FarmingItemCatalog.CreateDefaults();

            runManager.Configure(runSettings);

            // 가방 컬럼: 슬롯 한도와 합성 기본값은 인벤토리가, 최대 무게는 CarryLoad가 쓴다
            runManager.Inventory.Configure(
                bagDefinition.slotCountDefault, bagDefinition.mergeCountDefault);
            fieldSpawner.Configure(zoneDefinition, lootCatalog, farmingCatalog);
            fieldSpawner.SetViewCamera(followCamera);
            fieldSpawner.Track(player);
            // 존 너비가 곧 보행 가능 폭이므로, 캐릭터 반경만큼 안쪽으로 클램프한다 -
            // 반폭과 같게 두면 캡슐 절반이 바닥 밖으로 걸친다
            CharacterController playerController = player.GetComponent<CharacterController>();
            float bodyRadius = playerController != null ? playerController.radius : 0f;

            player.Motor.corridorHalfWidth = Mathf.Max(
                0.5f, zoneDefinition.corridorHalfWidth - bodyRadius);

            // HUD는 UI Toolkit 문서 (HudDocument 프리팹)로 씬에 배치된다.
            // 런타임 생성 금지 규약 - 없으면 경고만 (씬 재구성 안내)
            if (FindFirstObjectByType<UI.HudView>() == null)
                UnityEngine.Debug.LogWarning(
                    "[Flow] HudDocument 프리팹이 씬에 없다. " +
                    "'Scavenger > Ensure HUD Document (UI Toolkit)' 후 씬에 배치할 것.");

            // 무게 과적 (M2-1) - 기존 Player 프리팹에는 폴백으로 보강
            carryLoad = player.GetComponent<CarryLoad>();

            if (carryLoad == null)
                carryLoad = player.gameObject.AddComponent<CarryLoad>();

            carryLoad.Configure(bagDefinition);

            // 체력 (HP, 데미지형 장애물의 전제) - 기존 Player 프리팹에는 폴백으로 보강
            if (player.GetComponent<PlayerHealth>() == null)
                player.gameObject.AddComponent<PlayerHealth>();

            // 카메라 쉐이크 (피격/위협 피드백) - 기존 카메라 프리팹에는 폴백으로 보강
            if (followCamera.GetComponent<CameraShake>() == null)
                followCamera.gameObject.AddComponent<CameraShake>();

            // 깊이별 조도 (M4-2) - 기존 스포너 프리팹에는 폴백으로 보강
            if (fieldSpawner.GetComponent<DepthLighting>() == null)
                fieldSpawner.gameObject.AddComponent<DepthLighting>();

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

            if (fieldSpawner == null)
                fieldSpawner = FindFirstObjectByType<FieldSpawner>();

            if (player == null)
                player = FindFirstObjectByType<PlayerController>();

            if (followCamera == null)
                followCamera = FindFirstObjectByType<FollowCamera>();

            if (runManager != null && fieldSpawner != null && player != null && followCamera != null)
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
            float startZ = 0f;

            if (worldInitialized)
                startZ = player.transform.position.z - 2f;
            else
                worldInitialized = true;

            // 스테이지 진입: 존 개수 확정 + 현재/다음 존 생성 (필드 규칙 3.1)
            fieldSpawner.StartStage(startZ);

            // 낙사 후 재개: 구멍 아래에 있으면 바닥 위로 복귀
            RecoverPlayerIfFallen(startZ);

            player.ResetForNewRun();

            // 인벤토리는 StartRun에서 이미 비워졌다 - 이전 런의 과적 배율이
            // 첫 이동 프레임에 남지 않게 즉시 재판정 (Codex 검토 반영)
            carryLoad.RefreshNow();

            followCamera.SnapAndLook();
        }

        void RecoverPlayerIfFallen(float startZ)
        {
            Vector3 position = player.transform.position;

            if (position.y >= -0.5f)
                return;

            position.y = 0.05f;
            position.x = Mathf.Clamp(
                position.x, -zoneDefinition.corridorHalfWidth, zoneDefinition.corridorHalfWidth);
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
