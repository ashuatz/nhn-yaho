using System;
using Scavenger.Player;
using Scavenger.Run;
using UnityEngine;

namespace Scavenger.Segment
{
    /// <summary>
    /// 탈출 웨이포인트 (웹 프로토타입 이식, ADR-0008 - ChoiceNode 선택 대체).
    /// 존과 존 사이 구간에 하나 놓인다 - 밟으면 자동 탈출(정산).
    /// 선택 UI는 없다.
    ///
    /// 진행(Advance)은 웨이포인트가 아니다 (사용자 지시): 밟지 않고 걸어서
    /// 구간을 통과하는 것이 곧 다음 스테이지이며, 통과 감지와 깊이 증가는
    /// Field/FieldSpawner가 담당한다.
    /// 배치와 시각(청록 빛기둥 랜드마크)은 Field/FieldSpawner.Drops.cs가 소유.
    /// </summary>
    public sealed class ExtractionWaypoint : MonoBehaviour
    {
        Action<ExtractionWaypoint> onTrigger;
        bool consumed;

        public void Initialize(Action<ExtractionWaypoint> onTrigger)
        {
            this.onTrigger = onTrigger;
        }

        void OnTriggerEnter(Collider other)
        {
            if (consumed)
                return;

            PlayerController player = other.GetComponent<PlayerController>();

            if (player == null)
                return;

            if (!IsRunActive())
                return;

            // 밟는 순간 1회 발동 - 선택 UI 없이 자동 (웹 이식)
            consumed = true;
            onTrigger?.Invoke(this);
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
