using UnityEngine;

namespace Scavenger.Field
{
    /// <summary>
    /// 아이템 오브젝트가 생성되는 자리 마커 (파밍 문서 2.2 (3)).
    /// 한 스팟에 아이템 오브젝트 1개가 1대1로 생성된다.
    /// 아트 프리팹 계약: 빈 GameObject에 이 컴포넌트만 붙이고 위치만 잡는다.
    /// 파밍 포인트 안에 필요한 만큼 여러 개 배치할 수 있다.
    /// y 좌표는 그 자리의 보행면 높이에 맞춘다 (어긋나면 오브젝트가 뜨거나 묻힌다).
    /// </summary>
    public sealed class ObjectSpot : MonoBehaviour
    {
        /// <summary>이 스팟에 아이템 오브젝트가 이미 생성되었는가 (중복 생성 방지).</summary>
        public bool IsOccupied { get; private set; }

        public void MarkOccupied()
        {
            IsOccupied = true;
        }
    }
}
