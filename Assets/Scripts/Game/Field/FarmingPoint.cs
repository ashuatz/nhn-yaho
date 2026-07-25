using System.Collections.Generic;
using Scavenger.Player;
using Scavenger.Run;
using UnityEngine;

namespace Scavenger.Field
{
    /// <summary>존에 붙는 위치 (파밍 문서 2.4). 기능은 같고 배치와 단차만 다르다.</summary>
    public enum FarmingPointType
    {
        Top,
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
    /// 파밍 포인트 (파밍 문서 2장). 존 양옆에 붙는 구역이며 세 역할을 겸한다.
    /// - 파밍 구역: 오브젝트 스팟에 아이템 오브젝트가 생성된다 (3단계 작업)
    /// - 안전지대: 구역 안에서는 낙사하지 않는다. 가방 정리도 여기서 진행 (4단계 작업)
    /// - 제거 대상: 포인트 소켓 좌표가 제거 기준선에 닿으면 통째로 낙하한다 (2.7)
    ///
    /// 진입은 소켓과 같은 선상으로만 가능하다. 구역 위에 있는 동안 이동 경계를
    /// 구역 범위로 바꿔(PlayerMotor.SetBoundsOverride) 바깥으로 떨어지지 않게 한다.
    /// </summary>
    public sealed class FarmingPoint : MonoBehaviour
    {
        [Header("프리팹 자체 규격 (블록, 4~7). 0이면 ZoneDefinition 값을 쓴다")]
        public float authoredSizeBlocks;

        /// <summary>플레이어가 어느 파밍 포인트 안에 있는가 (없으면 null).</summary>
        public static FarmingPoint PlayerInside { get; private set; }

        /// <summary>안전지대 안인가. 위협 발동 보류/가방 정리 허용 판정에 사용.</summary>
        public static bool IsPlayerInSafeZone
        {
            get { return PlayerInside != null; }
        }

        public FarmingPointType PointType { get; private set; }
        public FarmingPointGrade Grade { get; private set; }

        /// <summary>포인트 소켓 월드 좌표 (제거 판정 기준 - 파밍 문서 2.7 (1)).</summary>
        public Vector3 SocketPosition { get; private set; }

        /// <summary>구역 안 오브젝트 스팟 (아이템 오브젝트 생성 자리).</summary>
        public IReadOnlyList<ObjectSpot> Spots
        {
            get { return spots; }
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
        /// 구역 규격 주입. socketPosition은 존 가장자리의 입구 좌표,
        /// depth는 존 밖으로 뻗는 길이, length는 z 방향 길이.
        /// </summary>
        public void Initialize(
            FarmingPointType pointType, FarmingPointGrade grade,
            Vector3 socketPosition, float sideSign, float zoneHalfWidth,
            float depth, float length, float shakeStartDistance)
        {
            PointType = pointType;
            Grade = grade;
            SocketPosition = socketPosition;

            this.sideSign = sideSign;
            this.zoneHalfWidth = zoneHalfWidth;
            this.shakeStartDistance = shakeStartDistance;

            zoneEdgeX = sideSign * zoneHalfWidth;

            float outerX = sideSign * (zoneHalfWidth + depth);
            minX = Mathf.Min(zoneEdgeX, outerX);
            maxX = Mathf.Max(zoneEdgeX, outerX);

            minZ = socketPosition.z - length * 0.5f;
            maxZ = socketPosition.z + length * 0.5f;

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

        // 제거 기준선이 소켓에 가까워지면 흔들리고, 지나가면 통째로 낙하한다 (2.7)
        void UpdateRemovalState()
        {
            FieldSpawner field = FieldSpawner.Instance;

            if (field == null)
                return;

            float removeLineZ = field.RemoveLineZ;

            if (removeLineZ >= SocketPosition.z)
            {
                BeginFall();
                return;
            }

            shaking = SocketPosition.z - removeLineZ <= shakeStartDistance;

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
                // 구역 안: x/z 모두 구역으로 클램프 - 바깥으로 떨어지지 않는다 (2.6 (3))
                player.Motor.SetBoundsOverride(minX, maxX, minZ, maxZ);
                return;
            }

            // 입구 선상: 복도와 구역을 합친 x 범위 (z는 제한하지 않는다)
            float unionMin = sideSign > 0f ? -zoneHalfWidth : minX;
            float unionMax = sideSign > 0f ? maxX : zoneHalfWidth;

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
