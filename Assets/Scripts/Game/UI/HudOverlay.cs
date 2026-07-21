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
        public PlayerController Player { get; set; }

        void OnGUI()
        {
            RunManager run = RunManager.Instance;

            if (run == null)
                return;

            DrawInventoryValue(run);
            DrawLootGauge();
            DrawChoicePrompt();
            DrawSignalBanner();
            DrawRunResult(run);
        }

        static void DrawInventoryValue(RunManager run)
        {
            Rect area = new Rect(10f, Screen.height - 40f, 300f, 30f);
            GUI.Box(area, $"가치 합계: {run.Inventory.TotalValue}");
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

            if (active.IsExtractionLocked())
            {
                GUI.Box(area, "탈출 신호 없음 - 이미 늦었다\nW: 더 깊이 전진");
                return;
            }

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

        void DrawRunResult(RunManager run)
        {
            RunState state = run.StateMachine.Current;

            if (state != RunState.Extracted && state != RunState.Dead)
                return;

            float width = 340f;
            Rect area = new Rect((Screen.width - width) * 0.5f, Screen.height * 0.4f, width, 60f);

            if (state == RunState.Extracted)
            {
                GUI.Box(area, $"탈출 성공. 확보 가치: {run.Inventory.TotalValue}\nR: 다음 런");
                return;
            }

            GUI.Box(area, "사망. 획득물 전량 손실\nR: 다시 시도");
        }
    }
}
