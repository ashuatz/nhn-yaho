using UnityEngine;

namespace Scavenger.Field
{
    /// <summary>
    /// 파밍 포인트와 존을 잇는 연결 부위 마커 (파밍 문서 2.2 (2)).
    /// 이 지점이 파밍 포인트의 입구이며, 제거(낙하) 판단의 기준 좌표가 된다.
    /// 아트 프리팹 계약: 빈 GameObject에 이 컴포넌트만 붙이고 위치만 잡는다.
    /// 파밍 포인트 프리팹당 1개 (한 소켓에 여러 개가 붙을 수 없다).
    /// y 좌표는 입구의 보행면 높이에 맞춘다.
    /// </summary>
    public sealed class PointSocket : MonoBehaviour
    {
    }
}
