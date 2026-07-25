using Scavenger.Run;
using Scavenger.Segment;
using UnityEngine;

namespace Scavenger.Player
{
    /// <summary>
    /// 카메라 쉐이크 (피격/위협 피드백 - 사용자 지시).
    /// 채널 2개:
    /// - 임펄스(트라우마): 폭발 피격 등 순간 충격. AddImpulse로 누적, 시간 감쇠
    /// - 근접 트레머: 붕괴 전선이 가까울수록 지속 진동. 땅 꺼짐 예고 등
    ///   외부 위협은 RequestTremor(강도)를 매 프레임 호출해 합류
    /// FollowCamera가 포즈를 확정한 뒤(LateUpdate, 실행 순서 +100) 오프셋만 더한다.
    /// 수치는 카메라 프리팹 튜닝 지점.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class CameraShake : MonoBehaviour
    {
        public static CameraShake Instance { get; private set; }

        [Header("흔들림 크기 (진폭 1 기준)")]
        public float maxPositionOffset = 0.35f;
        public float maxRollDegrees = 1.4f;

        [Header("노이즈 주파수 (Hz 느낌)")]
        public float frequency = 13f;

        [Header("임펄스 감쇠 (트라우마/초)")]
        public float traumaDecayPerSecond = 1.4f;

        [Header("붕괴 전선 근접 트레머")]
        public float collapseTremorDistance = 12f;
        [Range(0f, 1f)] public float collapseTremorMax = 0.5f;

        float trauma;
        float externalTremor;
        float noiseSeed;

        void OnEnable()
        {
            Instance = this;
            noiseSeed = GetInstanceID() * 0.137f;
        }

        void OnDisable()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>순간 충격 (폭발 피격 등). strength 0..1.</summary>
        public void AddImpulse(float strength)
        {
            trauma = Mathf.Clamp01(trauma + Mathf.Max(0f, strength));
        }

        /// <summary>
        /// 지속 위협 진동 요청 (땅 꺼짐 예고 등). 매 프레임 호출 전제 -
        /// 이번 프레임 요청 중 최대값만 반영되고 다음 프레임에 리셋된다.
        /// </summary>
        public void RequestTremor(float strength01)
        {
            externalTremor = Mathf.Max(externalTremor, Mathf.Clamp01(strength01));
        }

        void LateUpdate()
        {
            if (!Application.isPlaying)
                return;

            trauma = Mathf.Max(0f, trauma - traumaDecayPerSecond * Time.deltaTime);

            float amplitude = ComputeAmplitude();

            // 이번 프레임 외부 요청 소비 (다음 프레임에 다시 쌓인다)
            externalTremor = 0f;

            if (amplitude <= 0.0001f)
                return;

            ApplyShake(amplitude);
        }

        float ComputeAmplitude()
        {
            // 임펄스는 제곱 커브 - 작은 충격은 은은하게, 큰 충격은 확실하게
            float impulse = trauma * trauma;

            float continuous = Mathf.Max(externalTremor, ComputeRemovalTremor());

            return Mathf.Clamp01(Mathf.Max(impulse, continuous));
        }

        // 바닥 제거 기준선이 등 뒤로 다가올수록 지속 트레머 (필드 규칙 3.2 압박 피드백)
        float ComputeRemovalTremor()
        {
            RunManager run = RunManager.Instance;

            if (run == null || run.StateMachine.Current != RunState.Running)
                return 0f;

            Field.FieldSpawner field = Field.FieldSpawner.Instance;

            if (field == null)
                return 0f;

            float distance = field.RemoveLineDistanceToPlayer;

            if (distance >= collapseTremorDistance)
                return 0f;

            float proximity01 = 1f - Mathf.Clamp01(distance / collapseTremorDistance);
            return proximity01 * collapseTremorMax;
        }

        // FollowCamera가 매 프레임 포즈를 재계산하므로 누적 없이 더하기만 하면 된다
        void ApplyShake(float amplitude)
        {
            float time = Time.time * frequency;

            float offsetX = (Mathf.PerlinNoise(noiseSeed, time) * 2f - 1f);
            float offsetY = (Mathf.PerlinNoise(noiseSeed + 17.3f, time) * 2f - 1f);
            float roll = (Mathf.PerlinNoise(noiseSeed + 39.9f, time) * 2f - 1f);

            Vector3 localOffset = new Vector3(offsetX, offsetY, 0f) * (maxPositionOffset * amplitude);

            transform.position += transform.rotation * localOffset;
            transform.rotation *= Quaternion.Euler(0f, 0f, roll * maxRollDegrees * amplitude);
        }
    }
}
