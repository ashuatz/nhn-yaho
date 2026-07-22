using Scavenger.Player;
using Scavenger.Run;
using UnityEngine;

namespace Scavenger.UI
{
    /// <summary>
    /// 좌하단 가상 D-패드 (기획: 왼쪽 아래 조작계). 조이스틱 대용 온스크린 입력.
    /// 버튼 클릭 = 키보드와 동일한 4방향 토글 (같은 방향 = 정지, 다른 방향 = 전환).
    /// 현재 토글된 방향은 하이라이트. 그레이박스 단계라 IMGUI.
    /// </summary>
    public sealed class VirtualDPad : MonoBehaviour
    {
        [Header("레이아웃 (좌하단 기준)")]
        public float buttonSize = 56f;
        public float marginLeft = 24f;
        public float marginBottom = 24f;

        PlayerController player;

        void OnGUI()
        {
            RunManager run = RunManager.Instance;

            if (run == null || run.StateMachine.Current != RunState.Running)
                return;

            if (player == null)
            {
                player = FindFirstObjectByType<PlayerController>();

                if (player == null)
                    return;
            }

            DrawPad();
        }

        void DrawPad()
        {
            float size = buttonSize;
            float originX = marginLeft;
            float originY = Screen.height - marginBottom - size * 3f;

            Vector2 direction = player.Motor.Direction;

            // 십자 배치: 상 / 좌 우 / 하
            DrawButton(new Rect(originX + size, originY, size, size), "^", Vector2.up, direction);
            DrawButton(new Rect(originX, originY + size, size, size), "<", Vector2.left, direction);
            DrawButton(new Rect(originX + size * 2f, originY + size, size, size), ">", Vector2.right, direction);
            DrawButton(new Rect(originX + size, originY + size * 2f, size, size), "v", Vector2.down, direction);
        }

        void DrawButton(Rect rect, string label, Vector2 cardinal, Vector2 activeDirection)
        {
            Color previous = GUI.backgroundColor;

            if (activeDirection == cardinal)
                GUI.backgroundColor = new Color(1f, 0.85f, 0.3f);

            if (GUI.Button(rect, label))
                player.RequestToggle(cardinal);

            GUI.backgroundColor = previous;
        }
    }
}
