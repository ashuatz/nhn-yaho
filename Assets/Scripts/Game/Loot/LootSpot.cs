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
        /// <summary>HUD 게이지가 참조하는 현재 루팅 중인 스팟.</summary>
        public static LootSpot Active { get; private set; }

        public LootDefinition Definition { get; private set; }

        /// <summary>단차 위 스팟용: 이 높이 아래의 플레이어는 루팅 시작 불가 (옆에서 도둑질 방지).</summary>
        public float requiredMinPlayerY = -999f;

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

        float holdElapsed;
        bool looted;
        PlayerController playerInRange;
        PlayerController lootingPlayer;

        public void Initialize(LootDefinition definition, float interactRadius)
        {
            Definition = definition;

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

            if (playerInRange == null)
                return;

            TryBegin();
        }

        // Destroy는 프레임 끝까지 지연되므로 비활성화 시점에 즉시 정리한다
        // (런 재시작/침몰 시 이전 상호작용 잔존 방지 - Codex 검토 반영)
        void OnDisable()
        {
            Release();
        }

        void TryBegin()
        {
            // 다른 스팟이 루팅 중이면 개입하지 않는다
            if (Active != null)
                return;

            if (!playerInRange.InteractHeld)
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

            RunManager run = RunManager.Instance;
            run.Inventory.Add(Definition);
            UnityEngine.Debug.Log($"[Loot] {Definition.displayName} +{Definition.value} (total {run.Inventory.TotalValue})");

            Release();

            // 그레이박스: 획득한 스팟은 제거
            Destroy(gameObject);
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
