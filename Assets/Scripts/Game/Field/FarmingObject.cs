using System.Collections.Generic;
using Scavenger.Loot;
using Scavenger.Player;
using Scavenger.Run;
using UnityEngine;

namespace Scavenger.Field
{
    /// <summary>아이템 오브젝트 종류 (파밍 문서 3.2). 종류마다 상호작용 모션이 다르다.</summary>
    public enum FarmingObjectKind
    {
        /// <summary>상자 - 뚜껑을 여는 형태.</summary>
        Box,

        /// <summary>도자기 항아리 - 깨는 형태.</summary>
        Jar,
    }

    /// <summary>
    /// 아이템 오브젝트 (파밍 문서 3장). 파밍 포인트의 오브젝트 스팟에 1대1로 생성되며,
    /// 일정 시간 상호작용하면 열려서 파밍 아이템 1개를 낸다.
    ///
    /// 상호작용 (문서 3.3): 상호작용 키(E) 홀드로 진행하고, 이동 입력이나 홀드 해제,
    /// 범위 이탈이면 즉시 중단된다 - 중단해도 오브젝트는 남아 다시 열 수 있다.
    /// 진행 중에는 PlayerController를 Looting 상태로 두어 이동을 멈춘다 (ADR-0001).
    ///
    /// 획득 (문서 3.4 / 3.5): 열리면 아이템이 오브젝트 위로 튀어나오고, 가방에 여유가
    /// 있으면 접근한 순간 흡수된다. 여유가 없으면 그 자리에 남는다 - 이 규칙은
    /// LootSpot(자동 수집 + 무게/슬롯 초과 시 잔류)이 이미 갖고 있어 그대로 쓴다.
    ///
    /// 프리팹 계약: 이 컴포넌트를 루트에 붙이고 종류/등급/상호작용 시간을 선언한다.
    /// lid(열림 연출 부위)와 popOrigin(아이템이 나오는 자리)은 선택 - 없으면 루트 기준.
    /// </summary>
    public sealed class FarmingObject : MonoBehaviour
    {
        /// <summary>씬의 모든 아이템 오브젝트. 대시보드/라벨이 순회한다.</summary>
        public static readonly List<FarmingObject> All = new List<FarmingObject>();

        /// <summary>상호작용 범위 안에 있는 미개봉 오브젝트 (HUD 버튼 참조).</summary>
        public static FarmingObject PromptTarget { get; private set; }

        /// <summary>지금 열리는 중인 오브젝트 (HUD 게이지 참조). 동시 진행 불가.</summary>
        public static FarmingObject Active { get; private set; }

        [Header("프리팹 계약 (아트가 선언한다)")]
        public FarmingObjectKind kind = FarmingObjectKind.Box;

        [Header("오브젝트 등급 - 어떤 파밍 아이템이 나오는지의 기준 (문서 3.2)")]
        public FarmingPointGrade grade = FarmingPointGrade.Normal;

        [Header("상호작용 시간 (초). 등급이 높을수록 길다 (문서 3.3 (2))")]
        public float interactSeconds = 1.2f;

        [Header("상호작용 시작 거리 (m). 평면 거리로 판정한다")]
        public float interactRadius = 1.8f;

        [Header("열림 연출로 젖혀지는 부위 (없으면 생략)")]
        public Transform lid;

        [Header("아이템이 나오는 자리 (없으면 루트 위쪽)")]
        public Transform popOrigin;

        /// <summary>상호작용 진행도 0..1 (HUD 게이지).</summary>
        public float Progress01 { get; private set; }

        /// <summary>이미 열렸는가. 열린 오브젝트는 다시 상호작용하지 않는다.</summary>
        public bool IsOpened { get; private set; }

        // 이동 입력으로 보는 최소 크기 - 조이스틱 미세 흔들림으로 취소되지 않게
        const float CancelInputThreshold = 0.2f;

        // 플랫폼 아래에서 오브젝트를 열지 못하게 하는 높이 여유 (m)
        const float HeightTolerance = 1f;

        // 열림 연출: 뚜껑이 젖혀지는 각도와 시간
        const float LidOpenDegrees = 105f;
        const float LidOpenSeconds = 0.25f;

        // 아이템이 나오는 높이 (m). 오브젝트 위로 살짝 띄운다
        const float PopHeight = 0.55f;

        List<LootDefinition> itemCatalog;
        System.Random rng;
        float collectRadius = 1f;

        PlayerController player;
        FarmingPoint owner;
        Quaternion lidClosedRotation;
        float lidTimer;

        /// <summary>
        /// 배치 직후 스포너가 주입한다. 시드는 런 시드에서 배정받는다 -
        /// 어떤 아이템이 나오는지는 게임 결과라 재현성 대상이다 (필드 규칙 난수 규약).
        /// </summary>
        /// <summary>
        /// 등급별 기본 상호작용 시간 (파밍 문서 3.3 (2): 등급이 높을수록 오래 걸린다).
        /// 프리팹 템플릿과 코드 폴백이 같은 값을 쓰게 하는 단일 소스다.
        /// </summary>
        public static float DefaultInteractSeconds(FarmingPointGrade objectGrade)
        {
            if (objectGrade == FarmingPointGrade.Hero)
                return 3f;

            if (objectGrade == FarmingPointGrade.Rare)
                return 2f;

            return 1.2f;
        }

        public void Initialize(List<LootDefinition> catalog, int seed, float itemCollectRadius)
        {
            itemCatalog = catalog;
            rng = new System.Random(seed);
            collectRadius = Mathf.Max(0.1f, itemCollectRadius);
        }

        void OnEnable()
        {
            All.Add(this);

            if (lid != null)
                lidClosedRotation = lid.localRotation;
        }

        void OnDisable()
        {
            All.Remove(this);

            if (PromptTarget == this)
                PromptTarget = null;

            // 진행 중에 구역이 낙하하면 여기로 온다 - 플레이어를 이동 가능 상태로 되돌린다
            if (Active == this)
                CancelInteract();
        }

        void Update()
        {
            TickLid();

            if (IsOpened)
                return;

            if (!IsRunActive())
            {
                CancelIfActive();
                return;
            }

            // 구역이 무너지기 시작하면 즉시 중단한다 (파밍 문서 3.3 (4) 낙하).
            // 낙하 중에도 이 오브젝트는 살아 있으므로 OnDisable만 믿으면
            // 파괴될 때까지 플레이어가 Looting에 묶인다 (Codex 교차 검토 지적)
            if (IsOwnerFalling())
            {
                CancelIfActive();
                return;
            }

            if (!TryResolvePlayer())
                return;

            if (player.State == PlayerState.Dead)
            {
                CancelIfActive();
                return;
            }

            if (Active == this)
            {
                TickInteract();
                return;
            }

            TickPrompt();
        }

        // -- 상호작용 ---------------------------------------------------------

        void TickPrompt()
        {
            if (!IsPlayerInRange())
            {
                if (PromptTarget == this)
                    PromptTarget = null;

                return;
            }

            if (PromptTarget == null)
                PromptTarget = this;

            // 다른 오브젝트가 열리는 중이면 새로 시작하지 않는다 (동시 진행 불가)
            if (Active != null)
                return;

            if (!player.InteractHeld)
                return;

            // 이동 중에는 시작하지 않는다 - 지나가다 눌린 상태로 잡히지 않게
            if (Mathf.Abs(player.LateralInput) > CancelInputThreshold)
                return;

            if (!player.TryBeginLoot())
                return;

            Active = this;
            Progress01 = 0f;
        }

        void TickInteract()
        {
            // 중단 조건 (문서 3.3 (4)): 이동 입력 / 홀드 해제 / 범위 이탈
            if (Mathf.Abs(player.LateralInput) > CancelInputThreshold)
            {
                CancelInteract();
                return;
            }

            if (!player.InteractHeld)
            {
                CancelInteract();
                return;
            }

            if (!IsPlayerInRange())
            {
                CancelInteract();
                return;
            }

            float duration = Mathf.Max(0.1f, interactSeconds);
            Progress01 += Time.deltaTime / duration;

            if (Progress01 < 1f)
                return;

            CompleteInteract();
        }

        void CancelIfActive()
        {
            if (Active != this)
                return;

            CancelInteract();
        }

        // 중단해도 오브젝트는 그대로 남는다 - 진행도만 버린다 (문서 3.3 (4))
        void CancelInteract()
        {
            Progress01 = 0f;
            Active = null;

            if (player != null)
                player.EndLoot();
        }

        void CompleteInteract()
        {
            Progress01 = 1f;
            IsOpened = true;
            Active = null;

            if (PromptTarget == this)
                PromptTarget = null;

            if (player != null)
                player.EndLoot();

            lidTimer = LidOpenSeconds;

            SpawnItem();
        }

        // -- 획득 -------------------------------------------------------------

        /// <summary>
        /// 열린 자리에 파밍 아이템을 하나 놓는다 (문서 3.4). 흡수/잔류 판정은
        /// LootSpot이 갖고 있다 - 가방이 가득 차 있으면 그 자리에 남는다 (문서 3.5).
        /// </summary>
        void SpawnItem()
        {
            LootDefinition definition = FarmingItemCatalog.Draw(itemCatalog, (int)grade, rng);

            if (definition == null)
                return;

            Vector3 origin = ResolvePopOrigin();

            GameObject itemObject = new GameObject($"FarmItem_{definition.id}");

            // 부모는 오브젝트의 부모(오브젝트 스팟) - 구역이 낙하하면 함께 떨어진다.
            // 오브젝트 자신에 붙이면 열림 연출 회전을 아이템이 같이 받는다
            itemObject.transform.SetParent(transform.parent, true);
            itemObject.transform.position = origin;

            LootSpot spot = itemObject.AddComponent<LootSpot>();
            spot.Initialize(definition, collectRadius);

            // 단차 위 아이템 - 아래에서 수평으로만 겹쳐 가져가지 못하게
            spot.requiredMinPlayerY = transform.position.y - HeightTolerance;

            FieldSpawner.BuildDropVisual(itemObject.transform, definition.tier);

            LootBurst.Spawn(origin, 4, LootDefinition.GradeColor(definition.tier));
        }

        Vector3 ResolvePopOrigin()
        {
            if (popOrigin != null)
                return popOrigin.position;

            return transform.position + Vector3.up * PopHeight;
        }

        // -- 판정 -------------------------------------------------------------

        bool IsPlayerInRange()
        {
            // 플랫폼 위에 올라와야 한다 - 아래층에서 겹쳐 열지 못하게
            if (player.transform.position.y < transform.position.y - HeightTolerance)
                return false;

            Vector3 delta = player.transform.position - transform.position;

            // 평면 거리 - 단차 위아래 y 차이는 무시 (LootSpot과 같은 규칙)
            float planarSqr = delta.x * delta.x + delta.z * delta.z;

            return planarSqr <= interactRadius * interactRadius;
        }

        // 소속 구역. 배치 시 스팟의 자식으로 들어가므로 부모 사슬에 반드시 있다
        bool IsOwnerFalling()
        {
            if (owner == null)
                owner = GetComponentInParent<FarmingPoint>();

            if (owner == null)
                return false;

            return owner.IsFalling;
        }

        bool TryResolvePlayer()
        {
            if (player != null)
                return true;

            player = FindFirstObjectByType<PlayerController>();

            return player != null;
        }

        static bool IsRunActive()
        {
            RunManager run = RunManager.Instance;

            if (run == null)
                return false;

            return run.StateMachine.Current == RunState.Running;
        }

        // -- 연출 -------------------------------------------------------------

        // 뚜껑을 젖히는 짧은 연출. 부위가 없으면 아무 것도 하지 않는다
        void TickLid()
        {
            if (lid == null || lidTimer <= 0f)
                return;

            lidTimer -= Time.deltaTime;

            float openness = Mathf.Clamp01(1f - lidTimer / LidOpenSeconds);

            lid.localRotation =
                lidClosedRotation * Quaternion.Euler(-LidOpenDegrees * openness, 0f, 0f);
        }
    }
}
