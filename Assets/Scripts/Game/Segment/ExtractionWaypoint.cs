using System;
using Scavenger.Player;
using Scavenger.Run;
using UnityEngine;

namespace Scavenger.Segment
{
    /// <summary>
    /// 구간 끝 웨이포인트 (웹 프로토타입 이식, ADR-0008 - ChoiceNode 선택 대체).
    /// 두 개가 나란히 놓인다: 탈출 지점(Extract) / 다음 스테이지 포탈(Advance).
    /// 웹처럼 "밟으면 자동" - 선택 UI 없이 플레이어가 밟은 웨이포인트의 동작을 실행.
    /// 시각(청록 빛기둥 랜드마크)은 스포너의 BuildWaypointVisual이 만든다.
    /// </summary>
    public sealed class ExtractionWaypoint : MonoBehaviour
    {
        public enum Kind
        {
            Extract,   // 탈출 지점 - 밟으면 자동 탈출(정산)
            Advance,   // 다음 스테이지 포탈 - 밟으면 depth+1 진행
        }

        Kind kind;
        Action<ExtractionWaypoint> onTrigger;
        bool consumed;

        public Kind WaypointKind
        {
            get { return kind; }
        }

        public void Initialize(Kind kind, Action<ExtractionWaypoint> onTrigger)
        {
            this.kind = kind;
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
