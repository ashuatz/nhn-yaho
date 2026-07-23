using UnityEngine;

namespace Scavenger.Player
{
    /// <summary>
    /// 아이소메트릭 추적 카메라 (ADR-0008, 웹 프로토타입 이식 - 기존 원근 로우앵글
    /// ADR-0003을 대체). 직교(Orthographic) 투영 + 고정 회전(pitch 30 / yaw 45 =
    /// 2:1 픽셀 아이소). 원근 왜곡이 없어 웹의 평평한 아이소 룩과 일치한다.
    /// 리그 구성:
    ///   1. 회전 = Euler(pitchDegrees, yawDegrees, 0) 고정. 좌우 회피에도 각도 불변
    ///   2. lookAtOffset - 앵커(복도 중앙 x=0, smoothedZ) 기준 바라보는 지점
    ///   3. cameraDistance - 룩앳에서 시선 반대로 물러나는 거리 (직교라 구도 무관, 컬링용)
    ///   4. positionOffsetLocal - 시선 로컬축 평행이동 (화면 구도 시프트, 시선 불변 -
    ///      플레이어를 화면 비중심에 두는 용도)
    /// z는 SmoothDamp로 약간 늦게, 전진 전용 래칫(후퇴해도 카메라는 물러나지 않음).
    /// 포즈/투영은 매 프레임 재계산 - 인스펙터 튜닝 즉시 반영. orthographic을 강제하므로
    /// 프리팹 카메라가 원근으로 남아 있어도 아이소로 보정된다.
    /// 벨트스크롤 후방 한계(BackLimitZ)는 ViewportPointToRay 기반 - 직교에서도 유효.
    /// 입력축(조작)은 카메라 forward를 지면 투영해 화면 기준으로 자동 정렬 - 조작
    /// 로직(PlayerController)은 불변, 각도만 새 카메라를 따른다 (사용자 지시: 조작 유지).
    /// </summary>
    [ExecuteAlways]
    public sealed class FollowCamera : MonoBehaviour
    {
        public Transform target;

        [Header("아이소메트릭 (웹 프로토타입 이식, ADR-0008). 2:1 픽셀 아이소 = pitch 30 / yaw 45")]
        public bool orthographic = true;

        [Header("카메라 회전 (고정). pitch = 내려보는 각, yaw = 수평 회전")]
        public float pitchDegrees = 30f;

        // yaw -45(=315): 전진(+z) = 화면 오른쪽 위 (웹 진행 방향 - 왼쪽하단->오른쪽상단).
        // +45로 두면 좌우가 반전돼 왼쪽 위로 간다 (사용자 지시로 -45 확정)
        public float yawDegrees = -45f;

        [Header("직교 크기 (줌). 클수록 넓게(멀리) 보임 = 웹 zoom 반비례")]
        public float orthographicSize = 12f;

        [Header("룩앳 지점: 앵커(복도 중앙, smoothedZ) 기준 월드축. 바라보는 지점")]
        public Vector3 lookAtOffset = new Vector3(0f, 1.2f, 4f);

        [Header("카메라 거리: 룩앳에서 시선 반대로 물러나는 거리 (직교는 구도 무관, 컬링용)")]
        public float cameraDistance = 24f;

        [Header("카메라 포지션 보정: 시선 로컬축 (구도 시프트 - 플레이어를 화면 비중심에)")]
        public Vector3 positionOffsetLocal = new Vector3(0f, 0f, 0f);

        [Header("따라가기 지연 (초). 0 = 즉시 추적")]
        public float followSmoothTime = 0.15f;

        [Header("벨트스크롤: 후방 한계 = 화면 하단 에지의 지면 교점 (사용자 지시 - 아슬아슬)")]
        [Range(0f, 0.3f)] public float backEdgeViewportY = 0.03f;

        [Header("하단 에지 교점 기준 추가 여유 (+ = 더 뒤까지 허용)")]
        public float backLimitSlack = 0.5f;

        [Header("카메라 컴포넌트 부재 시 폴백 여유 (앵커 기준 뒤 허용 거리)")]
        public float backLimitMargin = 4f;

        /// <summary>플레이어 후퇴 한계 z (벨트스크롤 - 카메라 영역 밖 이탈 금지).</summary>
        public float BackLimitZ
        {
            get { return backLimitZ; }
        }

        float backLimitZ = float.NegativeInfinity;
        float smoothedZ;
        float zVelocity;
        Camera viewCamera;

        // 벨트스크롤 (사용자 지시): 카메라 앵커는 최대 도달 z만 따른다 - 후퇴 없음
        float ratchetZ;

        PlayerMotor targetMotor;

        void LateUpdate()
        {
            if (target == null)
                return;

            UpdateSmoothedZ();
            ApplyPose();
            PushBackLimit();
        }

        /// <summary>런 시작/월드 재생성 시 지연 없이 즉시 위치 맞춤.</summary>
        public void SnapAndLook()
        {
            if (target == null)
                return;

            smoothedZ = target.position.z;
            ratchetZ = smoothedZ;
            zVelocity = 0f;

            ApplyPose();
            PushBackLimit();
        }

        void UpdateSmoothedZ()
        {
            float targetZ = target.position.z;

            // 에디트 모드나 지연 0에서는 즉시 추적 (에디트 모드는 래칫도 무시)
            if (!Application.isPlaying || followSmoothTime <= 0f)
            {
                if (Application.isPlaying)
                    targetZ = ratchetZ = Mathf.Max(ratchetZ, targetZ);

                smoothedZ = targetZ;
                zVelocity = 0f;
                return;
            }

            // 전진 전용 래칫: 플레이어가 후퇴해도 카메라는 물러나지 않는다
            ratchetZ = Mathf.Max(ratchetZ, targetZ);

            smoothedZ = Mathf.SmoothDamp(smoothedZ, ratchetZ, ref zVelocity, followSmoothTime);
        }

        // 후퇴 한계를 모터에 공급 - 카메라 가시 영역 밖 이탈 금지 (벨트스크롤)
        void PushBackLimit()
        {
            if (!Application.isPlaying)
                return;

            backLimitZ = ComputeBackLimitZ();

            if (targetMotor == null)
            {
                targetMotor = target.GetComponent<PlayerMotor>();

                if (targetMotor == null)
                    return;
            }

            targetMotor.CameraMinZ = backLimitZ;
        }

        // 화면 하단 에지 뷰포트 지점의 시선이 지면(y=0)과 만나는 z가 후퇴 한계.
        // 발이 그 지점에 있으면 상체는 여전히 화면에 걸린다 (사용자 지시 - 아슬아슬).
        // 대각 카메라에서는 에지 교점 z가 화면 x에 따라 달라지므로 플레이어의
        // 현재 뷰포트 x에서 샘플한다 (Codex 교차 검토).
        // 포즈 확정(ApplyPose) 후에 호출할 것 - 이번 프레임 카메라 위치 기준
        float ComputeBackLimitZ()
        {
            if (viewCamera == null)
                viewCamera = GetComponent<Camera>();

            if (viewCamera == null)
                return smoothedZ - backLimitMargin;

            float sampleX = 0.5f;
            Vector3 targetViewport = viewCamera.WorldToViewportPoint(target.position);

            if (targetViewport.z > 0f)
                sampleX = Mathf.Clamp(targetViewport.x, 0.05f, 0.95f);

            Ray edgeRay = viewCamera.ViewportPointToRay(
                new Vector3(sampleX, backEdgeViewportY, 0f));

            // 시선이 지면을 향하지 않으면 (수평 이상) 폴백
            if (edgeRay.direction.y >= -0.0001f)
                return smoothedZ - backLimitMargin;

            float t = -edgeRay.origin.y / edgeRay.direction.y;
            float groundZ = edgeRay.origin.z + edgeRay.direction.z * t;

            return groundZ - backLimitSlack;
        }

        /// <summary>
        /// 카메라가 룩앳 지점(복도 중앙 x=0) 대비 갖는 (x, y) 오프셋. 시야 클리어런스
        /// (SegmentSpawner)가 배경 가림 판정에 쓴다. 아이소 회전/거리에서 유도.
        /// </summary>
        public Vector2 CameraOffsetXY()
        {
            Quaternion rotation = Quaternion.Euler(pitchDegrees, yawDegrees, 0f);
            Vector3 forward = rotation * Vector3.forward;

            // 카메라 위치 = lookAt - forward*distance. lookAt.x = 0(복도 중앙)이므로
            // 오프셋 = lookAtOffset.xy - forward.xy * distance
            float x = lookAtOffset.x - forward.x * cameraDistance;
            float y = lookAtOffset.y - forward.y * cameraDistance;

            return new Vector2(x, y);
        }

        // 아이소메트릭 포즈 (웹 이식): 회전은 pitch/yaw 고정, 위치는 룩앳 지점에서
        // 시선 반대 방향으로 cameraDistance만큼 후퇴. 직교 카메라라 거리는 구도에
        // 영향을 주지 않지만(투영 무한원) 컬링/근접 클립 여유를 위해 물러난다.
        void ApplyPose()
        {
            EnsureCamera();

            // 고정 회전 = 아이소 각도. 좌우 회피에도 각도가 흔들리지 않는다
            Quaternion rotation = Quaternion.Euler(pitchDegrees, yawDegrees, 0f);
            Vector3 forward = rotation * Vector3.forward;

            // x는 복도 중앙 고정, z만 추적 - 좌우 회피 시 화면이 흔들리지 않게
            Vector3 anchor = new Vector3(0f, 0f, smoothedZ);
            Vector3 lookAt = anchor + lookAtOffset;

            Vector3 basePosition = lookAt - forward * cameraDistance;

            transform.rotation = rotation;

            // 구도 시프트(시선 로컬축): 플레이어를 화면 정중앙이 아닌 곳에 두는 용도.
            // 시선 방향은 불변 - 위치만 로컬축으로 평행이동
            transform.position = basePosition + rotation * positionOffsetLocal;
        }

        // 직교/원근 및 줌을 매 포즈마다 강제 - 인스펙터 튜닝 즉시 반영, 프리팹이
        // 원근으로 남아 있어도 아이소로 보정 (웹 이식 - 원근 왜곡 제거)
        void EnsureCamera()
        {
            if (viewCamera == null)
                viewCamera = GetComponent<Camera>();

            if (viewCamera == null)
                return;

            viewCamera.orthographic = orthographic;

            if (orthographic)
                viewCamera.orthographicSize = orthographicSize;
        }
    }
}
