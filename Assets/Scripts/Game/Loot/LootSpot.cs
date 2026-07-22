using System.Collections.Generic;
using Scavenger.Player;
using Scavenger.Run;
using UnityEngine;

namespace Scavenger.Loot
{
    /// <summary>
    /// 씬 배치 파밍 요소. 범위 내에서 E 홀드로 루팅한다.
    /// 규칙 (ADR-0001): 루팅 시작 = 플레이어 정지, 좌우 입력 또는 홀드 해제 = 취소.
    /// 취소 시 진행도는 리셋된다 (부분 진행 보존 없음 - 고가치일수록 정지 리스크 유지).
    /// 루팅 참여자는 트리거 존재와 무관하게 lootingPlayer로 추적한다 -
    /// 스팟이 침몰/이동해 트리거를 벗어나도 반드시 EndLoot로 풀어준다 (소프트락 방지).
    /// </summary>
    public sealed class LootSpot : MonoBehaviour
    {
        /// <summary>씬의 모든 스팟. HUD 아이템 라벨이 순회한다 (M5-1).</summary>
        public static readonly List<LootSpot> All = new List<LootSpot>();

        /// <summary>HUD 게이지가 참조하는 현재 루팅 중인 스팟.</summary>
        public static LootSpot Active { get; private set; }

        /// <summary>
        /// 상호작용 키 프롬프트 대상 (사용자 지시: 범위 안이면 우하단에 키 표시).
        /// 시작 가능 조건(범위/높이/상태)을 충족한 스팟만 올라온다.
        /// </summary>
        public static LootSpot PromptTarget { get; private set; }

        public LootDefinition Definition { get; private set; }

        /// <summary>단차 위 스팟용: 이 높이 아래의 플레이어는 루팅 시작 불가 (옆에서 도둑질 방지).</summary>
        public float requiredMinPlayerY = -999f;

        /// <summary>완료 시 조각이 흩어지는 반경. 단차 위 스팟은 좁게 (상판 이탈 방지).</summary>
        public float scatterRadiusMin = 0.6f;
        public float scatterRadiusMax = 1.3f;

        /// <summary>조각 착지 x 클램프 반폭 (복도 밖 낙하 방지). 0 = 클램프 없음.</summary>
        public float scatterClampHalfWidth;

        public float Progress01
        {
            get
            {
                if (Definition == null || Definition.holdSeconds <= 0f)
                    return 0f;

                return Mathf.Clamp01(holdElapsed / Definition.holdSeconds);
            }
        }

        const float CancelLateralThreshold = 0.1f;

        // 트리거 반경 + CC 반경/접촉 여유. 진행 중 이탈 재검증에 사용
        const float RangeRevalidateSlack = 0.7f;

        // 파밍 연출 (사용자 지시): 뒤지는 동안 파편이 주기적으로 튄다
        const float RummageBurstInterval = 0.28f;
        const int RummageChipCount = 3;
        static readonly Color RummageColor = new Color(0.55f, 0.5f, 0.44f);

        // 완료 시 튀어나오는 조각 수. 가치/무게는 조각으로 분배된다 (합계 보존)
        const int DropPieces = 3;
        const float DropFlightSecondsMin = 0.45f;
        const float DropFlightSecondsMax = 0.7f;
        const float DropOriginHeight = 0.45f;

        float rummageTimer;
        float holdElapsed;
        float interactRadius;
        bool looted;
        PlayerController playerInRange;
        PlayerController lootingPlayer;

        public void Initialize(LootDefinition definition, float interactRadius)
        {
            Definition = definition;
            this.interactRadius = interactRadius;

            SphereCollider trigger = gameObject.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = interactRadius;
        }

        void OnTriggerEnter(Collider other)
        {
            PlayerController player = other.GetComponent<PlayerController>();

            if (player == null)
                return;

            playerInRange = player;
        }

        void OnTriggerExit(Collider other)
        {
            PlayerController player = other.GetComponent<PlayerController>();

            if (player == null)
                return;

            if (player == playerInRange)
                playerInRange = null;
        }

        void Update()
        {
            if (looted)
                return;

            if (Active == this)
            {
                TickLooting();
                return;
            }

            UpdatePromptEligibility();

            if (playerInRange == null)
                return;

            TryBegin();
        }

        void OnEnable()
        {
            All.Add(this);
        }

        // Destroy는 프레임 끝까지 지연되므로 비활성화 시점에 즉시 정리한다
        // (런 재시작/침몰 시 이전 상호작용 잔존 방지 - Codex 검토 반영)
        void OnDisable()
        {
            All.Remove(this);

            if (PromptTarget == this)
                PromptTarget = null;

            Release();
        }

        // 우하단 상호작용 프롬프트 대상 갱신 - 시작 가능 조건과 동일 기준
        void UpdatePromptEligibility()
        {
            bool eligible = playerInRange != null
                && Active == null
                && playerInRange.State == PlayerState.Advancing
                && playerInRange.transform.position.y >= requiredMinPlayerY;

            if (eligible)
            {
                PromptTarget = this;
                return;
            }

            if (PromptTarget == this)
                PromptTarget = null;
        }

        void TryBegin()
        {
            // 다른 스팟이 루팅 중이면 개입하지 않는다
            if (Active != null)
                return;

            if (!playerInRange.InteractHeld)
                return;

            // 홀드 이동(ADR-0007)에서 좌우 이동을 유지한 채 E = 시작 즉시 취소가
            // 되므로 시작 자체를 막는다 - 좌우를 놓고 홀드해야 루팅
            if (Mathf.Abs(playerInRange.LateralInput) > CancelLateralThreshold)
                return;

            // 단차 위 보상은 올라와야 딴다 - 바닥 옆에서 트리거만 겹쳐도 불가
            if (playerInRange.transform.position.y < requiredMinPlayerY)
                return;

            if (!playerInRange.TryBeginLoot())
                return;

            Active = this;
            lootingPlayer = playerInRange;
            holdElapsed = 0f;
        }

        void TickLooting()
        {
            // 사망/선택지 진입/런 종료/스팟 침몰 등 - 진행 즉시 중단 (검증 반영)
            if (!IsLootingStillValid())
            {
                Release();
                return;
            }

            // 좌우 입력 = 취소 (ADR-0001), 홀드 해제 = 취소
            bool cancelRequested = Mathf.Abs(lootingPlayer.LateralInput) > CancelLateralThreshold;

            if (cancelRequested || !lootingPlayer.InteractHeld)
            {
                Release();
                return;
            }

            holdElapsed += Time.deltaTime;

            // 뒤지는 중 표현: 주기적으로 파편이 튄다 (사용자 지시)
            rummageTimer -= Time.deltaTime;

            if (rummageTimer <= 0f)
            {
                rummageTimer = RummageBurstInterval;
                LootBurst.Spawn(
                    transform.position + Vector3.up * 0.35f, RummageChipCount, RummageColor);
            }

            if (holdElapsed < Definition.holdSeconds)
                return;

            Complete();
        }

        bool IsLootingStillValid()
        {
            if (lootingPlayer == null || lootingPlayer.State != PlayerState.Looting)
                return false;

            RunManager run = RunManager.Instance;

            if (run == null || run.StateMachine.Current != RunState.Running)
                return false;

            // 시작 조건은 진행 중에도 유지되어야 한다 (Codex 교차 검토):
            // 단차 침몰로 스팟이 내려가거나 플레이어가 떨어지면 즉시 취소
            if (lootingPlayer.transform.position.y < requiredMinPlayerY)
                return false;

            float maxDistance = interactRadius + RangeRevalidateSlack;
            Vector3 delta = lootingPlayer.transform.position - transform.position;

            if (delta.sqrMagnitude > maxDistance * maxDistance)
                return false;

            return true;
        }

        void Complete()
        {
            // 완료 직전 재검증 - 사망 프레임에 획득이 확정되는 것을 차단
            if (!IsLootingStillValid())
            {
                Release();
                return;
            }

            looted = true;

            // 획득은 여기서 확정되지 않는다 - 조각이 튀어나와 바닥에 떨어지고,
            // 줍는 시점에 인벤토리 반영 (사용자 지시). 방치한 조각 = 두고 간 가치
            LootBurst.Spawn(transform.position + Vector3.up * 0.4f, 9, RummageColor);
            ScatterPickups();

            UnityEngine.Debug.Log($"[Loot] {Definition.displayName} 해체 - 조각 {DropPieces}개 낙하");

            Release();

            // 그레이박스: 뒤진 스팟은 제거 (조각은 부모 스트립에 남는다)
            Destroy(gameObject);
        }

        // 조각을 포물선으로 흩뿌린다. 착지 높이 = 스팟 바닥 (단차 위 스팟은 상판).
        // 난수는 비주얼/산포 전용 스트림 - 배치 스트림(RunManager.Rng) 오염 금지
        void ScatterPickups()
        {
            System.Random rng = new System.Random(GetInstanceID());

            float floorY = transform.position.y;
            Vector3 origin = transform.position + Vector3.up * DropOriginHeight;

            for (int i = 0; i < DropPieces; i++)
            {
                LootDefinition piece = CreatePieceDefinition(i);

                float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                float radius = Mathf.Lerp(
                    scatterRadiusMin, scatterRadiusMax, (float)rng.NextDouble());

                Vector3 landing = transform.position + new Vector3(
                    Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

                // 복도 밖(허공) 착지 방지 - 바닥 레벨 스팟만 클램프
                if (scatterClampHalfWidth > 0f)
                    landing.x = Mathf.Clamp(landing.x, -scatterClampHalfWidth, scatterClampHalfWidth);

                float flightSeconds = Mathf.Lerp(
                    DropFlightSecondsMin, DropFlightSecondsMax, (float)rng.NextDouble());

                Vector3 velocity = ComputeArcVelocity(origin, landing, floorY, flightSeconds);

                // 스팟이 아니라 스팟의 부모(지지 스트립)에 부착 - 스팟 파괴 후에도
                // 바닥과 함께 침몰하는 규칙 유지
                LootPickup.Launch(transform.parent, piece, origin, velocity, floorY, rng);
            }
        }

        // 원본 가치/무게를 조각 수로 분배 (합계 보존, 나머지는 앞 조각에).
        // id가 같아 인벤토리 스택/스태시 저장 키는 그대로 동작한다
        LootDefinition CreatePieceDefinition(int index)
        {
            int baseValue = Definition.value / DropPieces;
            int remainder = Definition.value - baseValue * DropPieces;
            int pieceValue = baseValue + (index < remainder ? 1 : 0);

            return LootDefinition.Create(
                Definition.id, Definition.displayName, pieceValue,
                Definition.tier, Definition.holdSeconds, Definition.weight / DropPieces,
                Definition.shortDescription);
        }

        // 발사 지점에서 landing(x, z) / floorY(y)에 flightSeconds 만에 도달하는 초기 속도
        static Vector3 ComputeArcVelocity(
            Vector3 origin, Vector3 landing, float floorY, float flightSeconds)
        {
            const float Gravity = 9.81f;

            float velocityX = (landing.x - origin.x) / flightSeconds;
            float velocityZ = (landing.z - origin.z) / flightSeconds;

            float deltaY = floorY - origin.y;
            float velocityY = (deltaY + 0.5f * Gravity * flightSeconds * flightSeconds) / flightSeconds;

            return new Vector3(velocityX, velocityY, velocityZ);
        }

        void Release()
        {
            if (Active == this)
                Active = null;

            holdElapsed = 0f;

            // 트리거 이탈 여부와 무관하게 루팅 참여자는 반드시 풀어준다 (소프트락 방지)
            if (lootingPlayer != null)
            {
                lootingPlayer.EndLoot();
                lootingPlayer = null;
            }
        }
    }
}
