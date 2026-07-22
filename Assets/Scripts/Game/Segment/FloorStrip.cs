using UnityEngine;

namespace Scavenger.Segment
{
    /// <summary>
    /// 붕괴 단위 바닥 스트립 (ADR-0006). CollapseFront가 Sink를 호출하면
    /// 콜라이더를 끄고 가라앉는다 - 위에 있던 것은 낙하(낙사).
    /// </summary>
    public sealed class FloorStrip : MonoBehaviour
    {
        public float depthMeters = 2f;

        public float EndZ
        {
            get { return transform.position.z + depthMeters * 0.5f; }
        }

        public bool IsSinking { get; private set; }

        const float SinkSpeed = 4.5f;
        const float DestroyDepth = -8f;

        public void Sink()
        {
            if (IsSinking)
                return;

            IsSinking = true;

            Collider stripCollider = GetComponent<Collider>();

            if (stripCollider != null)
                stripCollider.enabled = false;
        }

        void Update()
        {
            if (!IsSinking)
                return;

            Vector3 position = transform.position;
            position.y -= SinkSpeed * Time.deltaTime;
            transform.position = position;

            if (position.y < DestroyDepth)
                Destroy(gameObject);
        }
    }
}
