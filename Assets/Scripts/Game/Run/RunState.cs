namespace Scavenger.Run
{
    /// <summary>
    /// 런 전체의 수명 주기 상태.
    /// </summary>
    public enum RunState
    {
        // 런 시작 전 대기
        Ready,

        // 런 진행 중 (파밍, 전진, 선택지 포함)
        Running,

        // 탈출 성공, 정산 완료 상태
        Extracted,

        // 사망, 전량 손실 상태
        Dead,
    }
}
