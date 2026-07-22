using System.Collections.Generic;
using Scavenger.Loot;
using UnityEngine;

namespace Scavenger.Run
{
    /// <summary>
    /// 런 종료 정산. 탈출 시 인벤토리를 스태시에 확정, 사망 시 아무것도 남기지 않는다.
    /// 중복 정산은 상태 머신이 차단한다 (Running -> Extracted 전이는 런당 1회).
    /// RunManager와 같은 오브젝트에 씬 배치. 구독은 모든 Awake 이후(Start)에 수행.
    /// </summary>
    [RequireComponent(typeof(RunManager))]
    public sealed class RunSettlement : MonoBehaviour
    {
        RunManager runManager;
        bool subscribed;

        void Start()
        {
            runManager = GetComponent<RunManager>();
            runManager.StateMachine.StateChanged += OnStateChanged;
            subscribed = true;
        }

        void OnDestroy()
        {
            if (!subscribed || runManager == null || runManager.StateMachine == null)
                return;

            runManager.StateMachine.StateChanged -= OnStateChanged;
        }

        void OnStateChanged(RunState previous, RunState next)
        {
            if (next == RunState.Extracted)
            {
                SettleExtraction();
                return;
            }

            if (next == RunState.Dead)
            {
                // 전량 손실: 스태시에 아무것도 반영하지 않는다.
                // 인벤토리 자체는 결과 화면 표시를 위해 남기고 다음 런 시작 시 비운다
                UnityEngine.Debug.Log($"[Settle] Death. Lost value {runManager.Inventory.TotalValue}");
            }
        }

        void SettleExtraction()
        {
            // 저장 실패가 런 상태 전이를 오염시키지 않게 격리 (Codex 검토 반영).
            // 실패 시 이번 런 획득물은 유실되지만 기존 스태시 파일은 보존된다
            try
            {
                PlayerStash stash = PlayerStash.LoadFrom(PlayerStash.DefaultPath);
                BankInventory(runManager.Inventory, stash);
                stash.SaveTo(PlayerStash.DefaultPath);

                UnityEngine.Debug.Log(
                    $"[Settle] Extracted. banked value {runManager.Inventory.TotalValue}, stash kinds {stash.Entries.Count}");
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError($"[Settle] Stash save failed - run loot lost: {e.Message}");
            }
        }

        /// <summary>
        /// 인벤토리를 스태시에 반영하는 순수 로직. EditMode 테스트 대상.
        /// 등급 합성(A안)으로 런 중 Entries는 압축되지만, 스태시는 등급을 모른다 -
        /// 합성 전 원본 개수(BankedCounts)로 저장해 창고의 id 스택을 유지한다.
        /// </summary>
        public static void BankInventory(RunInventory inventory, PlayerStash stash)
        {
            if (inventory == null || stash == null)
                return;

            foreach (KeyValuePair<string, int> banked in inventory.BankedCounts)
                stash.AddItem(banked.Key, banked.Value);
        }
    }
}
