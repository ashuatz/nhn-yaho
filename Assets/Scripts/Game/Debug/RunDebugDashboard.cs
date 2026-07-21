using Scavenger.Run;
using Scavenger.Segment;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Scavenger.Diagnostics
{
    /// <summary>
    /// 온스크린 런 상태 대시보드. 숨김 타이머의 실수치 등 플레이어에게 감춰지는
    /// 값을 개발 중에만 노출한다. F1로 토글. 에디터/개발 빌드 전용.
    /// 그레이박스 단계라 IMGUI 사용 (uGUI 폴리시는 트랙 C).
    /// </summary>
    public sealed class RunDebugDashboard : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        bool visible = true;

        void Update()
        {
            Keyboard keyboard = Keyboard.current;

            if (keyboard == null)
                return;

            if (keyboard.f1Key.wasPressedThisFrame)
                visible = !visible;
        }

        void OnGUI()
        {
            if (!visible)
                return;

            RunManager run = RunManager.Instance;

            if (run == null)
            {
                GUILayout.Label("[Dashboard] RunManager not found");
                return;
            }

            GUILayout.BeginArea(new Rect(10f, 10f, 320f, 400f), GUI.skin.box);

            GUILayout.Label("== Run Dashboard (F1) ==");
            GUILayout.Label($"State: {run.StateMachine.Current}");
            GUILayout.Label($"Seed: {run.Seed}");
            GUILayout.Label($"Depth: {run.Depth}");

            GUILayout.Space(4f);

            // 아래 시간 값들은 숨김 정보 - 플레이어 HUD에는 절대 노출 금지
            GUILayout.Label($"Elapsed: {run.Timer.Elapsed:F1}s");
            GUILayout.Label($"Limit (hidden): {run.Timer.LimitSeconds:F1}s");
            GUILayout.Label($"Remaining (hidden): {run.Timer.Remaining:F1}s");
            GUILayout.Label($"Extraction locked: {run.Timer.IsExpired}");

            DrawExtraSections(run);

            GUILayout.EndArea();
        }

        void DrawExtraSections(RunManager run)
        {
            GUILayout.Space(4f);

            GUILayout.Label($"Inventory value: {run.Inventory.TotalValue}");
            GUILayout.Label($"Inventory kinds: {run.Inventory.Entries.Count}");

            GUILayout.Space(4f);

            // 신호의 실거리 - 플레이어 HUD에는 절대 노출 금지
            if (!string.IsNullOrEmpty(SignalEmitter.LastMessage))
            {
                GUILayout.Label($"Signal: {SignalEmitter.LastMessage}");
                GUILayout.Label($"Signal real distance: {SignalEmitter.LastRealDistance:F1}m");
            }
        }
#endif
    }
}
