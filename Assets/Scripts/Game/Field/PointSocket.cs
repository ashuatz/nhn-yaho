using UnityEngine;

namespace Scavenger.Field
{
    /// <summary>
    /// 소켓 역할. 파밍 포인트는 입구와 출구가 나뉘어 있고, 출구는 z+ 쪽에 있다
    /// (사용자 지시) - 들어간 방향으로 되돌아 나오지 않고 앞으로 빠져나온다.
    /// </summary>
    public enum PointSocketRole
    {
        /// <summary>입구 (z- 쪽). 흔들림 경고 판정 기준.</summary>
        Entry,

        /// <summary>출구 (z+ 쪽). 낙하 판정 기준 - 밟고 있는 중에 무너지지 않게.</summary>
        Exit,
    }

    /// <summary>
    /// 파밍 포인트와 존을 잇는 연결 부위 마커 (파밍 문서 2.2 (2)).
    /// 이 지점이 파밍 포인트의 드나드는 자리이며, 제거(낙하) 판단의 기준 좌표가 된다.
    /// 아트 프리팹 계약: 빈 GameObject에 이 컴포넌트만 붙이고 위치/역할만 지정한다.
    /// 파밍 포인트 프리팹당 입구 1개 + 출구 1개 (역할별 1개).
    /// y 좌표는 드나드는 자리의 보행면 높이(존 본선과 같은 높이)에 맞춘다.
    /// </summary>
    public sealed class PointSocket : MonoBehaviour
    {
        [Header("소켓 역할 (입구 = z- / 출구 = z+)")]
        public PointSocketRole role = PointSocketRole.Entry;
    }
}
