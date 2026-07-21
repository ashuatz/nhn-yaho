namespace Scavenger.Player
{
    /// <summary>
    /// 플레이어 조작 상태 (ADR-0001).
    /// 루팅 중 전진 비허용, 루팅 = 정지 상태의 홀드 상호작용.
    /// </summary>
    public enum PlayerState
    {
        // 자동 전진 + 좌우 입력
        Advancing,

        // 수동 정지 (브레이크 홀드). 폭탄 타이밍 대기 등에 사용
        Stopped,

        // 루팅 홀드 중. 이동 불가, 좌우 입력 시 루팅 취소
        Looting,

        // 선택지 노드에서 탈출/전진 선택 중. 이동 정지
        AtChoice,

        // 사망. 모든 입력 무시
        Dead,
    }
}
