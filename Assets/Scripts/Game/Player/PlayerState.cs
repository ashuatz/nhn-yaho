namespace Scavenger.Player
{
    /// <summary>
    /// 플레이어 조작 상태 (ADR-0001, ADR-0002).
    /// 기본 정지 + 클릭 스텝 전진. 루팅 = 정지 상태의 홀드 상호작용.
    /// </summary>
    public enum PlayerState
    {
        // 통상 상태: 정지 대기 + 클릭 스텝 전진 + 좌우 이동
        Advancing,

        // 루팅 홀드 중. 클릭(전진) 무시, 좌우 입력 시 루팅 취소
        Looting,

        // 선택지 노드에서 탈출/전진 선택 중. 이동 정지
        AtChoice,

        // 사망. 모든 입력 무시
        Dead,
    }
}
