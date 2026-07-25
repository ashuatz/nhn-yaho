using System.Collections.Generic;
using Scavenger.Player;
using Scavenger.Run;
using UnityEngine;

namespace Scavenger.Field
{
    /// <summary>존에 붙는 위치 (파밍 문서 2.4). 기능은 같고 배치와 단차 방향만 다르다.</summary>
    public enum FarmingPointType
    {
        /// <summary>상단 - 화면 위쪽(카메라 반대편), 본선보다 높은 층.</summary>
        Top,

        /// <summary>하단 - 화면 아래쪽(카메라 쪽), 본선보다 낮은 층.</summary>
        Bottom,
    }

    /// <summary>등급 위상 (파밍 문서 2.3). 전설은 영웅 위상에 함께 배치된다.</summary>
    public enum FarmingPointGrade
    {
        Normal,
        Rare,
        Hero,
    }

    /// <summary>
    /// 파밍 포인트 (파밍 문서 2장). 존 옆에 붙는 구역이며 세 역할을 겸한다.
    /// - 파밍 구역: 오브젝트 스팟에 아이템 오브젝트가 생성된다 (3단계 작업)
    /// - 안전지대: 구역 안에서는 낙사하지 않는다. 가방 정리도 여기서 진행 (4단계 작업)
    /// - 제거 대상: 소켓 좌표가 제거 기준선에 닿으면 통째로 낙하한다 (2.7)
    ///
    /// 3층 구조 (3층 구조 계획): 상단은 본선보다 높고 하단은 낮으며, 계단으로
    /// 오르내린다. 입구는 z- 쪽, 출구는 z+ 쪽이라 들어간 곳으로 되돌아 나오지 않는다.
    /// 구역 위에 있는 동안 이동 경계를 구역 범위로 바꿔(PlayerMotor.SetBoundsOverride)
    /// 바깥으로 떨어지지 않게 한다.
    /// </summary>
    public sealed class FarmingPoint : MonoBehaviour
    {
        [Header("프리팹 자체 깊이 (블록, x. 계단 포함. 0이면 ZoneDefinition 값)")]
        public float authoredDepthBlocks;

        [Header("프리팹 자체 길이 (블록, z. 0이면 ZoneDefinition 값)")]
        public float authoredLengthBlocks;

        [Header("프리팹이 뻗는 방향 (+1 = +x / -1 = -x). 반대편에 붙일 때 회전 기준")]
        [Tooltip("구버전(ver01) 프리팹은 전부 +x 규격이므로 기본값이 +1이다")]
        public float authoredSideSign = 1f;

        /// <summary>플레이어가 어느 파밍 포인트 안에 있는가 (없으면 null).</summary>
        public static FarmingPoint PlayerInside { get; private set; }

        /// <summary>안전지대 안인가. 위협 발동 보류/가방 정리 허용 판정에 사용.</summary>
        public static bool IsPlayerInSafeZone
        {
            get { return PlayerInside != null; }
        }

        public FarmingPointType PointType { get; private set; }
        public FarmingPointGrade Grade { get; private set; }

        /// <summary>입구 소켓 월드 좌표 (z- 쪽. 흔들림 판정 기준 - 파밍 문서 2.7 (1)).</summary>
        public Vector3 EntrySocketPosition { get; private set; }

        /// <summary>출구 소켓 월드 좌표 (z+ 쪽. 낙하 판정 기준).</summary>
        public Vector3 ExitSocketPosition { get; private set; }

        /// <summary>플랫폼 높이 (m. 상단은 +, 하단은 -. 본선 = 0).</summary>
        public float PlatformY { get; private set; }

        /// <summary>구역 안 오브젝트 스팟 (아이템 오브젝트 생성 자리).</summary>
        public IReadOnlyList<ObjectSpot> Spots
        {
            get { return spots; }
        }

        /// <summary>구역 규격 주입 묶음. 값이 많아 구조체로 받는다.</summary>
        public struct Layout
        {
            public FarmingPointType PointType;
            public FarmingPointGrade Grade;

            /// <summary>입구 소켓 (존 가장자리, z- 쪽).</summary>
            public Vector3 EntrySocket;

            /// <summary>출구 소켓 (존 가장자리, z+ 쪽).</summary>
            public Vector3 ExitSocket;

            /// <summary>구역이 붙은 방향 (+1 = +x, -1 = -x).</summary>
            public float SideSign;

            public float ZoneHalfWidth;

            /// <summary>존 밖으로 뻗는 전체 깊이 (계단 + 플랫폼).</summary>
            public float TotalDepth;

            /// <summary>z 방향 전체 길이.</summary>
            public float Length;

            /// <summary>플랫폼 높이 (상단 +, 하단 -).</summary>
            public float PlatformY;

            public float ShakeStartDistance;
        }

        // 흔들림 진폭/주기 - 바닥 행(FloorRow)과 같은 문법으로 통일
        const float ShakeAmplitude = 0.07f;
        const float ShakeFrequency = 24f;

        const float FallGravity = 14f;
        const float FallSpeedInitial = 1.5f;
        const float DestroyDepth = -16f;

        // 진입 판정 여유 (m). 소켓 선상에서 살짝 벗어나도 드나들 수 있게
        const float EntrySlack = 0.4f;

        readonly List<ObjectSpot> spots = new List<ObjectSpot>();

        // 구역 경계 (월드). x는 존 가장자리에서 바깥으로, z는 구역 앞뒤
        float minX, maxX, minZ, maxZ;

        // 존 쪽 경계 (여기서 구역으로 드나든다)
        float zoneEdgeX;
        float sideSign;
        float zoneHalfWidth;

        float shakeStartDistance;
        bool shaking;
        bool falling;
        float fallSpeed;
        Vector3 restPosition;

        PlayerController player;

        /// <summary>
        /// 구역 규격 주입. 소켓은 존 가장자리의 드나드는 좌표이고,
        /// z 범위는 두 소켓의 중점에서 length만큼 잡는다 (소켓이 하나면 그 좌표 기준).
        /// </summary>
        public void Initialize(Layout layout)
        {
            PointType = layout.PointType;
            Grade = layout.Grade;
            EntrySocketPosition = layout.EntrySocket;
            ExitSocketPosition = layout.ExitSocket;
            PlatformY = layout.PlatformY;

            sideSign = layout.SideSign;
            zoneHalfWidth = layout.ZoneHalfWidth;
            shakeStartDistance = layout.ShakeStartDistance;

            zoneEdgeX = sideSign * zoneHalfWidth;

            float outerX = sideSign * (zoneHalfWidth + layout.TotalDepth);
            minX = Mathf.Min(zoneEdgeX, outerX);
            maxX = Mathf.Max(zoneEdgeX, outerX);

            float centerZ = (layout.EntrySocket.z + layout.ExitSocket.z) * 0.5f;
            minZ = centerZ - layout.Length * 0.5f;
            maxZ = centerZ + layout.Length * 0.5f;

            restPosition = transform.position;

            spots.Clear();
            spots.AddRange(GetComponentsInChildren<ObjectSpot>(true));
        }

        void OnDisable()
        {
            ReleasePlayer();
        }

        void Update()
        {
            if (falling)
            {
                TickFalling();
                return;
            }

            UpdateRemovalState();

            if (falling)
                return;

            UpdatePlayerBounds();
        }

        /// <summary>
        /// 제거 기준선이 입구 소켓에 가까워지면 흔들리고, 출구 소켓을 지나면
        /// 통째로 낙하한다 (2.7). 낙하 기준을 출구(z+)로 두는 이유는 구역이
        /// 길어졌기 때문 - 입구 기준으로 두면 플레이어가 출구 쪽 플랫폼에 서 있는
        /// 동안 발밑이 무너진다.
        /// </summary>
        void UpdateRemovalState()
        {
            FieldSpawner field = FieldSpawner.Instance;

            if (field == null)
                return;

            float removeLineZ = field.RemoveLineZ;

            if (removeLineZ >= ExitSocketPosition.z)
            {
                BeginFall();
                return;
            }

            shaking = EntrySocketPosition.z - removeLineZ <= shakeStartDistance;

            if (!shaking)
                return;

            float offsetX = Mathf.Sin(Time.time * ShakeFrequency) * ShakeAmplitude;
            float offsetY = Mathf.Sin(Time.time * ShakeFrequency * 1.6f) * ShakeAmplitude * 0.5f;
            transform.position = restPosition + new Vector3(offsetX, offsetY, 0f);
        }

        void BeginFall()
        {
            if (falling)
                return;

            falling = true;
            fallSpeed = FallSpeedInitial;
            transform.position = restPosition;

            // 구역 위에 있으면 클램프를 유지한 채 함께 떨어지게 둔다 (2.7 (3) - 사망 가능).
            // 여기서 해제하면 복도 클램프가 플레이어를 옆으로 순간이동시킨다.
            // 복도(입구 선상)에 있었다면 즉시 해제 - 사라진 구역 범위로 걸어 들어가면 안 된다
            if (!IsPlayerOverPlatform())
                ReleasePlayer();

            Collider[] colliders = GetComponentsInChildren<Collider>();

            foreach (Collider pointCollider in colliders)
                pointCollider.enabled = false;
        }

        void TickFalling()
        {
            float deltaTime = Time.deltaTime;

            fallSpeed += FallGravity * deltaTime;
            transform.position += Vector3.down * (fallSpeed * deltaTime);

            if (transform.position.y > DestroyDepth)
                return;

            Destroy(gameObject);
        }

        // 구역 위에 있으면 이동 경계를 구역으로, 입구 선상이면 복도와 구역을 잇는다
        void UpdatePlayerBounds()
        {
            if (!IsRunActive() || !TryResolvePlayer())
            {
                ReleasePlayer();
                return;
            }

            Vector3 position = player.transform.position;

            bool inZRange = position.z >= minZ - EntrySlack && position.z <= maxZ + EntrySlack;

            if (!inZRange)
            {
                ReleasePlayer();
                return;
            }

            bool overPoint = sideSign > 0f
                ? position.x > zoneEdgeX
                : position.x < zoneEdgeX;

            // 소유권은 조건을 만족한 쪽이 즉시 가져간다 (양보 금지).
            // 양보하면 앞 포인트가 해제하고 뒤 포인트가 잡기 전 1프레임 공백이 생겨,
            // 그 프레임에 복도 클램프가 플레이어를 옆으로 끌어당긴다
            PlayerInside = this;

            if (overPoint)
            {
                // 구역 안: x/z 모두 구역으로 클램프 - 바깥으로 떨어지지 않는다 (2.6 (3)).
                // 계단 구간도 이 범위에 포함된다 (계단이 존 가장자리에서 시작하므로)
                player.Motor.SetBoundsOverride(minX, maxX, minZ, maxZ);
                return;
            }

            // 입구/출구 선상: 복도와 구역을 합친 x 범위 (z는 제한하지 않는다).
            // 게이트가 아닌 z 구간은 차단 매스가 물리적으로 막는다.
            // 복도 쪽 한계는 모터의 클램프 폭을 쓴다 (캐릭터 반경만큼 안쪽) -
            // 존 반폭을 그대로 쓰면 구역 z 범위에 있는 동안만 반대쪽 바닥 밖으로
            // 캡슐 절반이 걸친다 (평소 클램프와 어긋난다)
            float corridorLimit = Mathf.Min(zoneHalfWidth, player.Motor.corridorHalfWidth);

            float unionMin = sideSign > 0f ? -corridorLimit : minX;
            float unionMax = sideSign > 0f ? maxX : corridorLimit;

            player.Motor.SetBoundsOverride(
                unionMin, unionMax, float.NegativeInfinity, float.PositiveInfinity);
        }

        // 구역 위(복도 밖)에 서 있는가. 낙하 시 클램프 유지 여부 판정에 사용
        bool IsPlayerOverPlatform()
        {
            if (player == null)
                return false;

            Vector3 position = player.transform.position;

            if (position.z < minZ - EntrySlack || position.z > maxZ + EntrySlack)
                return false;

            return sideSign > 0f
                ? position.x > zoneEdgeX
                : position.x < zoneEdgeX;
        }

        void ReleasePlayer()
        {
            if (PlayerInside != this)
                return;

            PlayerInside = null;

            if (player != null)
                player.Motor.ClearBoundsOverride();
        }

        static bool IsRunActive()
        {
            RunManager run = RunManager.Instance;

            if (run == null)
                return false;

            return run.StateMachine.Current == RunState.Running;
        }

        bool TryResolvePlayer()
        {
            if (player != null)
                return true;

            player = FindFirstObjectByType<PlayerController>();
            return player != null;
        }
    }
}
