using System.Collections.Generic;
using Scavenger.Player;
using Scavenger.Run;
using UnityEngine;

namespace Scavenger.Loot
{
    /// <summary>
    /// 길바닥 파밍 아이템 (웹 프로토타입 이식, ADR-0008 - 기존 E 홀드 루팅 대체).
    /// 자동 수집: 플레이어가 수집 반경에 들어오면 무게 여유가 있는 한 즉시 획득해
    /// 인벤토리에 반영하고 사라진다. E 홀드/조각 낙하 없음 - 밟으면 먹는 손맛(웹).
    /// 무게 초과면 획득하지 않고 그대로 남는다 (과적 압박 = 두고 갈지 선택).
    /// 획득 연출(가방으로 날아가는 파티클)은 HUD(LootFlyLayer)가 이벤트로 처리.
    /// </summary>
    public sealed class LootSpot : MonoBehaviour
    {
        /// <summary>씬의 모든 스팟. HUD 아이템 라벨이 순회한다.</summary>
        public static readonly List<LootSpot> All = new List<LootSpot>();

        /// <summary>
        /// 방금 획득된 아이템 알림 (정의, 월드 위치). HUD가 구독해 획득 파티클/토스트를
        /// 띄운다. 자동 수집이라 별도 프롬프트/게이지는 없다.
        /// </summary>
        public static event System.Action<LootDefinition, Vector3> Collected;

        public LootDefinition Definition { get; private set; }

        /// <summary>단차 위 아이템용: 이 높이 미만의 플레이어는 수집 불가 (바닥에서 도둑질 방지).</summary>
        public float requiredMinPlayerY = -999f;

        // 자동 수집 반경 (웹 0.85 제곱거리에 대응). 스포너가 주입
        float collectRadius;

        bool collected;
        PlayerController player;
        CarryLoad carryLoad;

        // 가방에서 버린 아이템 (등급/몫 보존). 일반 배치물이면 사용하지 않는다
        RunInventory.DroppedItem dropped;
        bool isDropped;

        // 주울 수 있는 상태인가. 버린 직후에는 false이고 반경을 벗어나면 열린다
        bool armed = true;

        public void Initialize(LootDefinition definition, float collectRadius)
        {
            Definition = definition;
            this.collectRadius = collectRadius;
        }

        /// <summary>
        /// 가방에서 버린 아이템으로 초기화한다 (사용자 지시 2026-07-26: 드래그앤드롭 버리기).
        /// 등급과 몫을 그대로 들고 있다가 다시 주우면 되돌린다 - 합성해 둔 결과가
        /// 버렸다 줍는 것만으로 사라지면 안 된다.
        ///
        /// 버린 직후에는 주울 수 없다. 발밑에 놓자마자 자동 수집이 되빨아들이면
        /// 버리기가 성립하지 않으므로, 플레이어가 수집 반경을 한 번 벗어나야 열린다.
        /// </summary>
        public void InitializeDropped(RunInventory.DroppedItem item, float collectRadius)
        {
            Definition = item.Definition;
            this.collectRadius = collectRadius;

            dropped = item;
            isDropped = true;
            armed = false;
        }

        void OnEnable()
        {
            All.Add(this);
        }

        void OnDisable()
        {
            All.Remove(this);
        }

        void Update()
        {
            if (collected)
                return;

            if (!IsRunActive())
                return;

            if (!TryResolvePlayer())
                return;

            if (player.State == PlayerState.Dead)
                return;

            if (!IsPlayerInRange())
            {
                // 버린 아이템은 여기서 열린다 (반경을 한 번 벗어나야 다시 주울 수 있다)
                armed = true;
                return;
            }

            if (!armed)
                return;

            TryCollect();
        }

        bool IsPlayerInRange()
        {
            // 단차 위 아이템은 올라와야 딴다 - 바닥에서 수평으로만 겹치는 것 차단
            if (player.transform.position.y < requiredMinPlayerY)
                return false;

            Vector3 delta = player.transform.position - transform.position;

            // 수평 거리만 - 단차 아래위 y 차이는 무시(밟는 느낌). 웹은 x/z 평면 판정
            float sqrPlanar = delta.x * delta.x + delta.z * delta.z;

            return sqrPlanar <= collectRadius * collectRadius;
        }

        // 획득 제한 (가방 문서 5장 / 드랍 문서 6.3): 무게 초과 또는 슬롯 초과면
        // 남겨두고(두고 갈 선택) 아무 것도 안 한다
        void TryCollect()
        {
            RunManager run = RunManager.Instance;

            if (run == null || Definition == null)
                return;

            // 버린 아이템은 나갈 때의 몫으로 되돌아온다 (등급 유지)
            float weight = isDropped ? dropped.Weight : Definition.weight;
            float projectedWeight = run.Inventory.TotalWeight + weight;
            float maxWeight = ResolveMaxWeight();

            if (projectedWeight > maxWeight)
                return;

            // 슬롯 초과: 같은 (id, 등급) 스택이 없고 빈 슬롯도 없으면 담을 자리가 없다
            int grade = isDropped ? dropped.Grade : Definition.tier;

            if (!run.Inventory.HasSlotFor(Definition, grade))
                return;

            collected = true;

            if (isDropped)
                run.Inventory.Restore(dropped);
            else
                run.Inventory.Add(Definition);

            // HUD 획득 연출 (파티클/토스트) - 위치를 넘겨 가방으로 날아가는 시작점으로
            Collected?.Invoke(Definition, transform.position);

            Destroy(gameObject);
        }

        // 최대 적재량은 CarryLoad가 소유 (무게 5단계의 기준값). 없으면 넉넉한 폴백
        float ResolveMaxWeight()
        {
            if (carryLoad == null)
                carryLoad = FindFirstObjectByType<CarryLoad>();

            if (carryLoad == null)
                return 45f;

            return carryLoad.maxCarryWeight;
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
    }
}
