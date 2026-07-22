using Scavenger.Loot;
using Scavenger.Player;
using Scavenger.Run;
using Scavenger.Segment;
using UnityEngine;

namespace Scavenger.UI
{
    /// <summary>
    /// 플레이어용 HUD. 그레이박스 단계라 IMGUI (uGUI 폴리시는 트랙 C).
    /// 주의: 숨김 정보(타이머 실수치, 실거리)는 여기 노출 금지 - 대시보드 전용.
    /// </summary>
    public sealed class HudOverlay : MonoBehaviour
    {
        CarryLoad carryLoad;

        void OnGUI()
        {
            RunManager run = RunManager.Instance;

            if (run == null)
                return;

            DrawInventoryValue(run);
            DrawLootGauge();
            DrawChoicePrompt();
            DrawSignalBanner();
            DrawCollapseWarning(run);
            DrawRunResult(run);
        }

        // 무게 상태 (M2-1): 가치 합계 아래에 무게와 적재 단계를 함께 표시.
        // x 204 = 가상 D-패드(좌하단, 폭 약 192px) 오른쪽 - 겹침 방지 (배치 정리는 M5-1)
        void DrawInventoryValue(RunManager run)
        {
            Rect area = new Rect(204f, Screen.height - 64f, 300f, 54f);
            GUI.Box(area, $"가치 합계: {run.Inventory.TotalValue}\n{BuildWeightLine(run)}");
        }

        string BuildWeightLine(RunManager run)
        {
            if (carryLoad == null)
                carryLoad = FindFirstObjectByType<CarryLoad>();

            if (carryLoad == null)
                return $"무게: {run.Inventory.TotalWeight:F1}";

            return $"무게: {run.Inventory.TotalWeight:F1} ({CarryLoad.StageLabel(carryLoad.Stage)})";
        }

        static void DrawLootGauge()
        {
            LootSpot active = LootSpot.Active;

            if (active == null)
                return;

            float width = 260f;
            Rect back = new Rect((Screen.width - width) * 0.5f, Screen.height * 0.62f, width, 24f);
            GUI.Box(back, string.Empty);

            Rect fill = new Rect(back.x + 2f, back.y + 2f, (back.width - 4f) * active.Progress01, back.height - 4f);
            GUI.Box(fill, string.Empty);

            Rect label = new Rect(back.x, back.y - 22f, width, 20f);
            GUI.Label(label, $"루팅 중: {active.Definition.displayName} (+{active.Definition.value})");
        }

        static void DrawChoicePrompt()
        {
            ChoiceNode active = ChoiceNode.Active;

            if (active == null)
                return;

            float width = 340f;
            Rect area = new Rect((Screen.width - width) * 0.5f, Screen.height * 0.3f, width, 64f);

            GUI.Box(area, "선택하라\nW: 더 깊이 전진 (고가치/고위험)  E: 탈출 (확정)");
        }

        const float SignalBannerSeconds = 3.5f;

        static void DrawSignalBanner()
        {
            if (string.IsNullOrEmpty(SignalEmitter.LastMessage))
                return;

            if (Time.time - SignalEmitter.LastMessageAt > SignalBannerSeconds)
                return;

            float width = 360f;
            Rect area = new Rect((Screen.width - width) * 0.5f, 30f, width, 28f);
            GUI.Box(area, SignalEmitter.LastMessage);
        }

        const float CollapseWarningDistance = 12f;

        // 시간 압박 인지 표현 (ADR-0006): 붕괴 전선 근접 경고
        static void DrawCollapseWarning(RunManager run)
        {
            if (run.StateMachine.Current != RunState.Running)
                return;

            CollapseFront collapse = CollapseFront.Instance;

            if (collapse == null || collapse.DistanceToPlayer > CollapseWarningDistance)
                return;

            float width = 340f;
            Rect area = new Rect((Screen.width - width) * 0.5f, Screen.height * 0.18f, width, 28f);
            GUI.Box(area, "뒤에서 바닥이 무너지고 있다!");
        }

        void DrawRunResult(RunManager run)
        {
            RunState state = run.StateMachine.Current;

            if (state != RunState.Extracted && state != RunState.Dead)
                return;

            float width = 340f;
            Rect area = new Rect((Screen.width - width) * 0.5f, Screen.height * 0.4f, width, 60f);

            if (state == RunState.Extracted)
            {
                GUI.Box(area, $"탈출 성공. 확보 가치: {run.Inventory.TotalValue}\n클릭: 계속 전진");
                return;
            }

            GUI.Box(area, "사망. 획득물 전량 손실\n클릭: 계속");
        }
    }
}
