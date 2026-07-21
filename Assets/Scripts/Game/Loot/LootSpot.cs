using Scavenger.Player;
using Scavenger.Run;
using UnityEngine;

namespace Scavenger.Loot
{
    /// <summary>
    /// 씬 배치 파밍 요소. 범위 내에서 E 홀드로 루팅한다.
    /// 규칙 (ADR-0001): 루팅 시작 = 플레이어 정지, 좌우 입력 또는 홀드 해제 = 취소.
    /// 취소 시 진행도는 리셋된다 (부분 진행 보존 없음 - 고가치일수록 정지 리스크 유지).
    /// </summary>
    public sealed class LootSpot : MonoBehaviour
    {
        /// <summary>HUD 게이지가 참조하는 현재 루팅 중인 스팟.</summary>
        public static LootSpot Active { get; private set; }

        public LootDefinition Definition { get; private set; }

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
            if (looted || playerInRange == null)
                return;

            if (Active == this)
            {
                TickLooting();
                return;
            }

            TryBegin();
        }

        void OnDestroy()
        {
            if (Active == this)
                Active = null;
        }

        void TryBegin()
        {
            // 다른 스팟이 루팅 중이면 개입하지 않는다
            if (Active != null)
                return;

            if (!playerInRange.InteractHeld)
                return;

            if (!playerInRange.TryBeginLoot())
                return;

            Active = this;
            holdElapsed = 0f;
        }

        void TickLooting()
        {
            // 좌우 입력 = 취소 (ADR-0001), 홀드 해제 = 취소
            bool cancelRequested = Mathf.Abs(playerInRange.LateralInput) > CancelLateralThreshold;

            if (cancelRequested || !playerInRange.InteractHeld)
            {
                Release();
                return;
            }

            holdElapsed += Time.deltaTime;

            if (holdElapsed < Definition.holdSeconds)
                return;

            Complete();
        }

        void Complete()
        {
            looted = true;

            RunManager run = RunManager.Instance;

            if (run != null)
            {
                run.Inventory.Add(Definition);
                UnityEngine.Debug.Log($"[Loot] {Definition.displayName} +{Definition.value} (total {run.Inventory.TotalValue})");
            }

            Release();

            // 그레이박스: 획득한 스팟은 제거
            Destroy(gameObject);
        }

        void Release()
        {
            if (Active == this)
                Active = null;

            holdElapsed = 0f;
            playerInRange.EndLoot();
        }
    }
}
