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

        /// <summary>
        /// 붕괴 전선이 이 z를 넘으면 가라앉는다. 긴 요소(단차)도 전선이 2m
        /// 파고들면 무너지게 해 붕괴 면역 구간을 없앤다 (검증 반영).
        /// </summary>
        public float SinkThresholdZ
        {
            get
            {
                float startZ = transform.position.z - depthMeters * 0.5f;
                return startZ + Mathf.Min(depthMeters, 2f);
            }
        }

        public bool IsSinking { get; private set; }

        /// <summary>
        /// 사전 배치(손 편집) 스트립용: 침몰 후 파괴하지 않고 비활성 보존한다.
        /// 파괴하면 다음 런에서 커버 범위 판정은 통과하는데 바닥이 없어
        /// 재시작 즉시 낙사 루프가 된다 (Codex 교차 검토 P1).
        /// </summary>
        public bool preserveOnSink;

        const float SinkSpeed = 4.5f;
        const float DestroyDepth = -8f;

        Vector3 initialLocalPosition;
        bool initialCaptured;

        void Awake()
        {
            CaptureInitial();
        }

        public void Sink()
        {
            if (IsSinking)
                return;

            IsSinking = true;

            // 단일 스트립뿐 아니라 단차(자식 콜라이더 포함) 같은 복합 요소도 지원
            Collider[] colliders = GetComponentsInChildren<Collider>();

            foreach (Collider featureCollider in colliders)
                featureCollider.enabled = false;
        }

        /// <summary>런 재시작 시 보존된 스트립을 원위치로 복구한다 (사전 배치 전용).</summary>
        public void Restore()
        {
            CaptureInitial();

            IsSinking = false;
            transform.localPosition = initialLocalPosition;

            Collider[] colliders = GetComponentsInChildren<Collider>(true);

            foreach (Collider featureCollider in colliders)
                featureCollider.enabled = true;

            gameObject.SetActive(true);
        }

        void Update()
        {
            if (!IsSinking)
                return;

            Vector3 position = transform.position;
            position.y -= SinkSpeed * Time.deltaTime;
            transform.position = position;

            if (position.y >= DestroyDepth)
                return;

            if (preserveOnSink)
            {
                gameObject.SetActive(false);
                return;
            }

            Destroy(gameObject);
        }

        void CaptureInitial()
        {
            if (initialCaptured)
                return;

            initialLocalPosition = transform.localPosition;
            initialCaptured = true;
        }
    }
}
