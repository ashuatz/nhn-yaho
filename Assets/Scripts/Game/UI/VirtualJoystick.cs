using Scavenger.Player;
using Scavenger.Run;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Scavenger.UI
{
    /// <summary>
    /// 좌하단 가상 조이스틱 (ADR-0007). M5-1에서 uGUI로 전환 - HudCanvas 프리팹의
    /// RectTransform 리그(base/knob)를 에디터 템플릿이 배선한다.
    /// 베이스 원 안에서 드래그 = 2D 벡터 입력. 마우스/터치 공용.
    /// EventSystem 없이 포인터 직접 판독 (기존 방식 유지).
    /// 실행 순서 -200: PlayerController(-100)가 읽기 전에 벡터를 갱신해
    /// 1프레임 입력 지연을 없앤다 (Codex 검토 반영).
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class VirtualJoystick : MonoBehaviour
    {
        [Header("uGUI 리그 (HudCanvas 프리팹이 배선)")]
        public RectTransform baseRect;
        public RectTransform knobRect;

        [Header("입력 (반경 비율 데드존)")]
        [Range(0f, 0.5f)] public float deadZone = 0.12f;

        /// <summary>현재 입력 벡터 (-1..1). HUD/디버그 참조용.</summary>
        public Vector2 Value { get; private set; }

        PlayerController player;
        bool dragging;

        Texture2D circleTexture;
        Sprite circleSprite;

        void Awake()
        {
            if (baseRect == null || knobRect == null)
            {
                UnityEngine.Debug.LogWarning(
                    "[Joystick] uGUI 리그 미배선. HudCanvas 프리팹을 재생성하거나 배선할 것.");
                enabled = false;
                return;
            }

            // 원형 스프라이트는 런타임 생성 - 에셋 저장 없음
            // (에디터 저장 인메모리 리소스 = 마젠타/깨짐 이슈 회피)
            circleTexture = CreateCircleTexture(64);
            circleSprite = Sprite.Create(
                circleTexture, new Rect(0f, 0f, 64f, 64f), new Vector2(0.5f, 0.5f));

            ApplySprite(baseRect);
            ApplySprite(knobRect);
        }

        void OnDestroy()
        {
            if (circleSprite != null)
                Destroy(circleSprite);

            if (circleTexture != null)
                Destroy(circleTexture);
        }

        void Update()
        {
            bool running = IsRunning();

            if (baseRect != null && baseRect.gameObject.activeSelf != running)
                baseRect.gameObject.SetActive(running);

            if (!running || !TryResolvePlayer())
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

            // 오버레이 캔버스에서 RectTransform.position = 스크린 픽셀 좌표
            Vector2 center = baseRect.position;
            float radiusPixels = baseRect.rect.width * 0.5f * baseRect.lossyScale.x;

            // 드래그 시작은 베이스 원 안에서만 - 화면 아무데나 누르면 반응하지 않게
            if (!dragging)
            {
                if ((pointer - center).sqrMagnitude > radiusPixels * radiusPixels)
                    return;

                dragging = true;
            }

            Vector2 raw = Vector2.ClampMagnitude((pointer - center) / radiusPixels, 1f);

            if (raw.magnitude < deadZone)
                raw = Vector2.zero;

            Value = raw;
            player.SetExternalMoveInput(raw);

            float travel = (baseRect.rect.width - knobRect.rect.width) * 0.5f;
            knobRect.anchoredPosition = raw * travel;
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

            if (knobRect != null)
                knobRect.anchoredPosition = Vector2.zero;

            if (player != null)
                player.SetExternalMoveInput(Vector2.zero);
        }

        void ApplySprite(RectTransform target)
        {
            Image image = target.GetComponent<Image>();

            if (image == null)
                return;

            image.sprite = circleSprite;
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

        static Texture2D CreateCircleTexture(int size)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float radius = size * 0.5f;
            Vector2 center = new Vector2(radius, radius);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    texture.SetPixel(x, y, distance <= radius ? Color.white : Color.clear);
                }
            }

            texture.Apply();
            return texture;
        }
    }
}
