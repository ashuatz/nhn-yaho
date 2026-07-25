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

            GUILayout.Label($"Elapsed: {run.Timer.Elapsed:F1}s");

            // 바닥 제거 기준선 실수치 - 플레이어 HUD에는 근접 경고만 노출
            Field.FieldSpawner field = Field.FieldSpawner.Instance;

            if (field != null)
            {
                GUILayout.Label($"Remove line z: {field.RemoveLineZ:F1}");
                GUILayout.Label($"Remove line distance: {field.RemoveLineDistanceToPlayer:F1}m");
                GUILayout.Label($"Stage: {field.StageIndex + 1} (zones {field.StageZoneCount})");

                // 구간(존 사이)에 있으면 존 인덱스가 -1이다
                GUILayout.Label(field.CurrentZoneIndex >= 0
                    ? $"Zone: {field.CurrentZoneIndex + 1} / {field.StageZoneCount}"
                    : "Zone: junction (waypoint)");

                Field.FarmingPoint point = Field.FarmingPoint.PlayerInside;

                GUILayout.Label(point != null
                    ? $"Safe zone: {point.PointType} / {point.Grade}"
                    : "Safe zone: -");
            }

            DrawExtraSections(run);

            GUILayout.EndArea();
        }

        int observedDepth = -1;
        float depthEnteredAt;

        void DrawExtraSections(RunManager run)
        {
            GUILayout.Space(4f);

            GUILayout.Label($"Inventory value: {run.Inventory.TotalValue}");
            GUILayout.Label($"Inventory kinds: {run.Inventory.Entries.Count}");

            GUILayout.Space(4f);

            // 구간 소요 시간 계측 - 2-3분 라운드 템포 목표 검증용 (구현계획 v0.0.2 섹션 2.3)
            if (observedDepth != run.Depth)
            {
                observedDepth = run.Depth;
                depthEnteredAt = Time.time;
            }

            GUILayout.Label($"Stage elapsed: {Time.time - depthEnteredAt:F1}s (target 150s/stage)");
        }
#endif
    }
}
