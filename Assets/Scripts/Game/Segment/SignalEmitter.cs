using Scavenger.Player;
using Scavenger.Run;
using UnityEngine;

namespace Scavenger.Segment
{
    /// <summary>
    /// 거리 기반 비정형 신호 (ADR-0001). 통과 지점에서 선택지 노드까지의
    /// 남은 거리를 모호한 문구로만 알린다. 실거리 수치는 대시보드 전용.
    /// 시간 값은 절대 노출하지 않는다 - 시간 초과 시 신호 두절 문구만.
    /// </summary>
    public sealed class SignalEmitter : MonoBehaviour
    {
        /// <summary>HUD 배너가 읽는 마지막 신호. 런 재시작 시 부트스트랩이 Clear.</summary>
        public static string LastMessage { get; private set; }
        public static float LastMessageAt { get; private set; }

        /// <summary>대시보드 전용 실거리.</summary>
        public static float LastRealDistance { get; private set; }

        float distanceToNode;
        bool fired;

        public void Initialize(float distanceToNode, float corridorWidth)
        {
            this.distanceToNode = distanceToNode;

            BoxCollider trigger = gameObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(corridorWidth, 3f, 0.8f);
            trigger.center = new Vector3(0f, 1.5f, 0f);
        }

        public static void Clear()
        {
            LastMessage = null;
            LastMessageAt = 0f;
            LastRealDistance = 0f;
        }

        void OnTriggerEnter(Collider other)
        {
            if (fired)
                return;

            PlayerController player = other.GetComponent<PlayerController>();

            if (player == null)
                return;

            fired = true;
            Emit();
        }

        void Emit()
        {
            LastMessage = BuildMessage();
            LastMessageAt = Time.time;
            LastRealDistance = distanceToNode;
        }

        string BuildMessage()
        {
            if (distanceToNode > 45f)
                return "탈출 신호가 아주 희미하게 잡힌다";

            if (distanceToNode > 20f)
                return "신호가 점점 선명해진다";

            return "신호가 강하다. 선택 지점이 가깝다";
        }
    }
}
