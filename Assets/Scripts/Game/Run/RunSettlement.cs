using Scavenger.Loot;
using UnityEngine;

namespace Scavenger.Run
{
    /// <summary>
    /// 런 종료 정산. 탈출 시 인벤토리를 스태시에 확정, 사망 시 아무것도 남기지 않는다.
    /// 중복 정산은 상태 머신이 차단한다 (Running -> Extracted 전이는 런당 1회).
    /// </summary>
    public sealed class RunSettlement : MonoBehaviour
    {
        RunManager runManager;

        public void Attach(RunManager manager)
        {
            runManager = manager;
            runManager.StateMachine.StateChanged += OnStateChanged;
        }

        void OnDestroy()
        {
            if (runManager != null)
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
            PlayerStash stash = PlayerStash.LoadFrom(PlayerStash.DefaultPath);

            foreach (RunInventory.Entry entry in runManager.Inventory.Entries)
                stash.AddItem(entry.Definition.id, entry.Count);

            stash.SaveTo(PlayerStash.DefaultPath);

            UnityEngine.Debug.Log(
                $"[Settle] Extracted. banked value {runManager.Inventory.TotalValue}, stash kinds {stash.Entries.Count}");
        }
    }
}
