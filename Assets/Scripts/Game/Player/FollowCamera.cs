using UnityEngine;

namespace Scavenger.Player
{
    /// <summary>
    /// 아이소메트릭 추적 카메라 (ADR-0008, 웹 프로토타입 이식 - 기존 원근 로우앵글
    /// ADR-0003을 대체). 고정 회전(pitch 30 / yaw 45 = 2:1 픽셀 아이소).
    /// 투영은 두 모드다 (사용자 지시):
    ///   - 원근 초망원 (기본): fov 15~20 + 카메라를 멀리 후퇴. 오쏘와 거의 같은 룩에
    ///     아주 약한 원근 단서만 남는다. 그림자/포그/블러가 원근을 전제하는 URP
    ///     기능들과도 어긋나지 않는다
    ///   - 직교: orthographic = true. 원근 왜곡이 완전히 0인 순수 아이소
    /// 리그 구성:
    ///   1. 회전 = Euler(pitchDegrees, yawDegrees, 0) 고정. 좌우 회피에도 각도 불변
    ///   2. lookAtOffset - 앵커(복도 중앙 x=0, smoothedZ) 기준 바라보는 지점
    ///   3. 카메라 거리 = EffectiveCameraDistance(). 원근에서는 오쏘와 같은 화면 크기가
    ///      되는 거리로 자동 대체된다 (cameraDistance는 오쏘 전용)
    ///   4. positionOffsetLocal - 시선 로컬축 평행이동 (화면 구도 시프트, 시선 불변 -
    ///      플레이어를 화면 비중심에 두는 용도)
    /// z는 SmoothDamp로 약간 늦게, 전진 전용 래칫(후퇴해도 카메라는 물러나지 않음).
    /// 포즈/투영/클립 평면은 매 프레임 재계산 - 인스펙터 튜닝 즉시 반영. 프리팹 카메라
    /// 값이 어긋나 있어도 리그 기준으로 보정된다.
    /// 벨트스크롤 후방 한계(BackLimitZ)는 ViewportPointToRay 기반 - 두 투영 모두 유효.
    /// 입력축(조작)은 카메라 forward를 지면 투영해 화면 기준으로 자동 정렬 - 조작
    /// 로직(PlayerController)은 불변, 각도만 새 카메라를 따른다 (사용자 지시: 조작 유지).
    /// 주의: 원근 후퇴량만큼 카메라 기준 후처리 값(LUT 포그 distanceRange, DOF
    /// focusDistance)도 함께 밀어야 한다 - 눈 깊이가 통째로 커진다.
    /// </summary>
    [ExecuteAlways]
    public sealed class FollowCamera : MonoBehaviour
    {
        // FOV가 0에 붕괴하면 tan(fov/2)가 0이 되어 거리가 무한이 된다
        const float MinFieldOfView = 1f;
        const float MaxFieldOfView = 170f;

        // 유니티 카메라가 허용하는 최소 근접 클립
        const float MinNearClip = 0.01f;

        public Transform target;

        [Header("직교 투영. false = 초망원 원근 (기본, 사용자 지시 - 오쏘와 거의 같은 룩)")]
        public bool orthographic = false;

        [Header("원근 수직 FOV (도). 작을수록 오쏘에 가깝고 카메라가 더 멀어진다 (15~20 권장)")]
        [Range(MinFieldOfView, 60f)] public float fieldOfView = 18f;

        [Header("카메라 회전 (고정). pitch = 내려보는 각, yaw = 수평 회전")]
        public float pitchDegrees = 30f;

        // yaw -45(=315): 전진(+z) = 화면 오른쪽 위 (웹 진행 방향 - 왼쪽하단->오른쪽상단).
        // +45로 두면 좌우가 반전돼 왼쪽 위로 간다 (사용자 지시로 -45 확정)
        public float yawDegrees = -45f;

        [Header("프레이밍 크기 = 화면 절반 높이 (줌). 클수록 넓게 보임 = 웹 zoom 반비례. "
                + "원근에서도 이 크기를 유지하도록 카메라 거리가 자동 계산된다")]
        public float orthographicSize = 12f;

        [Header("룩앳 지점: 앵커(복도 중앙, smoothedZ) 기준 월드축. 바라보는 지점")]
        public Vector3 lookAtOffset = new Vector3(0f, 1.2f, 4f);

        [Header("[직교 전용] 카메라 거리: 룩앳에서 시선 반대로 물러나는 거리 (구도 무관, 컬링용). "
                + "원근에서는 프레이밍 크기와 FOV로 계산한 거리가 대신 쓰인다")]
        public float cameraDistance = 24f;

        [Header("[직교 기준] 클립 평면. 원근에서는 후퇴한 거리만큼 함께 밀어 "
                + "직교가 보던 월드 깊이 구간을 그대로 유지한다")]
        public float orthoNearClip = 0.1f;

        public float orthoFarClip = 90f;

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
        /// 이번 프레임에 실제로 쓰는 카메라 거리. 원근에서는 프레이밍 크기를 그대로
        /// 재현하는 거리로 대체되므로 cameraDistance보다 훨씬 크다 (초망원 = 멀리서 좁게).
        /// </summary>
        public float EffectiveCameraDistance()
        {
            if (orthographic)
                return cameraDistance;

            return MatchedPerspectiveDistance(orthographicSize, fieldOfView);
        }

        /// <summary>
        /// 직교 카메라의 화면 절반 높이(orthoSize)를 원근 카메라가 그대로 재현하는 거리.
        /// half = d * tan(fov/2) 이므로 d = orthoSize / tan(fov/2).
        /// FOV는 수직 기준이라 종횡비와 무관하게 가로도 함께 일치한다.
        /// (정적 순수 함수 - EditMode 테스트 대상)
        /// </summary>
        public static float MatchedPerspectiveDistance(float orthoSize, float fov)
        {
            float clampedFov = Mathf.Clamp(fov, MinFieldOfView, MaxFieldOfView);
            float tangent = Mathf.Tan(clampedFov * 0.5f * Mathf.Deg2Rad);

            return orthoSize / tangent;
        }

        /// <summary>
        /// 카메라가 룩앳 지점(복도 중앙 x=0) 대비 갖는 (x, y) 오프셋. 시야 클리어런스
        /// (SegmentSpawner)가 배경 가림 판정에 쓴다. 아이소 회전/거리에서 유도.
        /// </summary>
        public Vector2 CameraOffsetXY()
        {
            Quaternion rotation = Quaternion.Euler(pitchDegrees, yawDegrees, 0f);
            Vector3 forward = rotation * Vector3.forward;

            // 실제 카메라 위치에서 유도해야 가림 판정이 화면과 일치한다. 원근에서는
            // 카메라가 훨씬 멀어져 시선 밴드가 pitch 각도에 더 가까워진다
            float distance = EffectiveCameraDistance();

            // 카메라 위치 = lookAt - forward*distance. lookAt.x = 0(복도 중앙)이므로
            // 오프셋 = lookAtOffset.xy - forward.xy * distance
            float x = lookAtOffset.x - forward.x * distance;
            float y = lookAtOffset.y - forward.y * distance;

            return new Vector2(x, y);
        }

        // 아이소메트릭 포즈 (웹 이식): 회전은 pitch/yaw 고정, 위치는 룩앳 지점에서
        // 시선 반대 방향으로 후퇴. 직교에서는 거리가 구도에 영향을 주지 않지만
        // (투영 무한원) 컬링/근접 클립 여유를 위해 물러나고, 원근에서는 거리 자체가
        // 줌이라 프레이밍 크기를 재현하는 지점까지 멀리 물러난다.
        void ApplyPose()
        {
            EnsureCamera();

            // 고정 회전 = 아이소 각도. 좌우 회피에도 각도가 흔들리지 않는다
            Quaternion rotation = Quaternion.Euler(pitchDegrees, yawDegrees, 0f);
            Vector3 forward = rotation * Vector3.forward;

            // x는 복도 중앙 고정, z만 추적 - 좌우 회피 시 화면이 흔들리지 않게
            Vector3 anchor = new Vector3(0f, 0f, smoothedZ);
            Vector3 lookAt = anchor + lookAtOffset;

            Vector3 basePosition = lookAt - forward * EffectiveCameraDistance();

            transform.rotation = rotation;

            // 구도 시프트(시선 로컬축): 플레이어를 화면 정중앙이 아닌 곳에 두는 용도.
            // 시선 방향은 불변 - 위치만 로컬축으로 평행이동
            transform.position = basePosition + rotation * positionOffsetLocal;
        }

        // 투영/줌/클립 평면을 매 포즈마다 강제 - 인스펙터 튜닝 즉시 반영, 프리팹 카메라
        // 값이 어긋나 있어도 리그 기준으로 보정된다
        void EnsureCamera()
        {
            if (viewCamera == null)
                viewCamera = GetComponent<Camera>();

            if (viewCamera == null)
                return;

            viewCamera.orthographic = orthographic;

            if (orthographic)
            {
                viewCamera.orthographicSize = orthographicSize;
                ApplyClipPlanes(0f);
                return;
            }

            viewCamera.fieldOfView = Mathf.Clamp(fieldOfView, MinFieldOfView, MaxFieldOfView);

            // 뒤로 뺀 만큼 클립 평면도 함께 밀어, 직교 카메라가 보던 월드 깊이 구간을
            // 그대로 유지한다 (사용자 지시 - 오쏘 기준 카메라와 위치 일치)
            ApplyClipPlanes(EffectiveCameraDistance() - cameraDistance);
        }

        // 직교 기준 클립 평면을 pullback만큼 밀어 적용.
        // 근접을 최소값으로 먼저 내려두는 이유: 이전 프레임의 근접이 새 원거리보다
        // 뒤에 있으면(모드 전환 직후) 대입 순서 때문에 구간이 뒤집힌다
        void ApplyClipPlanes(float pullback)
        {
            float nearPlane = Mathf.Max(MinNearClip, orthoNearClip + pullback);
            float farPlane = Mathf.Max(nearPlane + 1f, orthoFarClip + pullback);

            viewCamera.nearClipPlane = MinNearClip;
            viewCamera.farClipPlane = farPlane;
            viewCamera.nearClipPlane = nearPlane;
        }
    }
}
