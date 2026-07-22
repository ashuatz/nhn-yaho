using Scavenger.Player;
using Scavenger.Run;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Scavenger.UI
{
    /// <summary>
    /// 좌하단 가상 조이스틱 (ADR-0007, 토글 D-패드 대체).
    /// 베이스 원 안에서 드래그 = 2D 벡터 입력. 화면 x = 좌우, 화면 y = 전후.
    /// 마우스/터치 공용. 그레이박스 단계라 IMGUI 렌더 (uGUI 전환은 M5-1).
    /// </summary>
    public sealed class VirtualJoystick : MonoBehaviour
    {
        [Header("레이아웃 (좌하단 기준, 픽셀)")]
        public float baseRadius = 80f;
        public float knobRadius = 30f;
        public float marginLeft = 36f;
        public float marginBottom = 36f;

        [Header("입력 (반경 비율 데드존)")]
        [Range(0f, 0.5f)] public float deadZone = 0.12f;

        /// <summary>현재 입력 벡터 (-1..1). HUD/디버그 참조용.</summary>
        public Vector2 Value { get; private set; }

        PlayerController player;
        bool dragging;

        Texture2D baseTexture;
        Texture2D knobTexture;

        void Awake()
        {
            // 런타임 전용 텍스처 - 에셋 저장 없음 (마젠타 이슈는 에디터 저장 경로에만 해당)
            baseTexture = CreateCircleTexture(64, new Color(1f, 1f, 1f, 0.16f));
            knobTexture = CreateCircleTexture(64, new Color(1f, 1f, 1f, 0.55f));
        }

        void OnDestroy()
        {
            Destroy(baseTexture);
            Destroy(knobTexture);
        }

        void Update()
        {
            if (!IsRunning() || !TryResolvePlayer())
            {
                ReleaseDrag();
                return;
            }

            Vector2 pointer;

            if (!ReadPointerPressed(out pointer))
            {
                ReleaseDrag();
                return;
            }

            Vector2 center = CenterInputSpace();

            // 드래그 시작은 베이스 원 안에서만 - 화면 아무데나 누르면 반응하지 않게
            if (!dragging)
            {
                if ((pointer - center).sqrMagnitude > baseRadius * baseRadius)
                    return;

                dragging = true;
            }

            Vector2 raw = Vector2.ClampMagnitude((pointer - center) / baseRadius, 1f);

            if (raw.magnitude < deadZone)
                raw = Vector2.zero;

            Value = raw;
            player.SetExternalMoveInput(raw);
        }

        void OnGUI()
        {
            if (!IsRunning())
                return;

            // GUI 좌표는 y가 아래로 - 입력 좌표(y 위)와 반전
            float centerX = marginLeft + baseRadius;
            float centerY = Screen.height - marginBottom - baseRadius;

            Rect baseRect = new Rect(
                centerX - baseRadius, centerY - baseRadius, baseRadius * 2f, baseRadius * 2f);
            GUI.DrawTexture(baseRect, baseTexture);

            float travel = baseRadius - knobRadius;
            float knobX = centerX + Value.x * travel;
            float knobY = centerY - Value.y * travel;

            Rect knobRect = new Rect(
                knobX - knobRadius, knobY - knobRadius, knobRadius * 2f, knobRadius * 2f);
            GUI.DrawTexture(knobRect, knobTexture);
        }

        static bool IsRunning()
        {
            RunManager run = RunManager.Instance;

            if (run == null)
                return false;

            return run.StateMachine.Current == RunState.Running;
        }

        bool TryResolvePlayer()
        {
            if (player != null)
                return true;

            player = FindFirstObjectByType<PlayerController>();
            return player != null;
        }

        void ReleaseDrag()
        {
            if (!dragging && Value == Vector2.zero)
                return;

            dragging = false;
            Value = Vector2.zero;

            if (player != null)
                player.SetExternalMoveInput(Vector2.zero);
        }

        Vector2 CenterInputSpace()
        {
            // Input System 포인터 좌표는 좌하단 원점 (y 위)
            return new Vector2(marginLeft + baseRadius, marginBottom + baseRadius);
        }

        static bool ReadPointerPressed(out Vector2 position)
        {
            Touchscreen touch = Touchscreen.current;

            if (touch != null && touch.primaryTouch.press.isPressed)
            {
                position = touch.primaryTouch.position.ReadValue();
                return true;
            }

            Mouse mouse = Mouse.current;

            if (mouse != null && mouse.leftButton.isPressed)
            {
                position = mouse.position.ReadValue();
                return true;
            }

            position = Vector2.zero;
            return false;
        }

        static Texture2D CreateCircleTexture(int size, Color color)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float radius = size * 0.5f;
            Vector2 center = new Vector2(radius, radius);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);

                    if (distance <= radius)
                        texture.SetPixel(x, y, color);
                    else
                        texture.SetPixel(x, y, Color.clear);
                }
            }

            texture.Apply();
            return texture;
        }
    }
}
