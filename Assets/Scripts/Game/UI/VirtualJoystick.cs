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

        // 드래그 소스 추적 (멀티터치 - Codex 교차 검토): 왼손이 조이스틱을 잡은 동안
        // 다른 손가락 입력이 조이스틱을 뺏지 않도록 시작한 touchId를 고정한다.
        // -1 = 마우스
        int dragTouchId = -1;

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

            // 오버레이 캔버스에서 RectTransform.position = 스크린 픽셀 좌표
            Vector2 center = baseRect.position;
            float radiusPixels = baseRect.rect.width * 0.5f * baseRect.lossyScale.x;

            Vector2 pointer;

            // 드래그 시작은 베이스 원 안에서만 - 화면 아무데나 누르면 반응하지 않게.
            // 시작한 포인터(touchId/마우스)를 고정 추적한다 (멀티터치 대응)
            if (!dragging)
            {
                if (!TryBeginDrag(center, radiusPixels, out pointer))
                    return;

                dragging = true;
            }
            else if (!TryReadDragPointer(out pointer))
            {
                ReleaseDrag();
                return;
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
            dragTouchId = -1;
            Value = Vector2.zero;

            if (knobRect != null)
                knobRect.anchoredPosition = Vector2.zero;

            if (player != null)
                player.SetExternalMoveInput(Vector2.zero);
        }

        // 베이스 원 안에서 눌린 포인터를 찾아 드래그 소스로 고정한다
        bool TryBeginDrag(Vector2 center, float radiusPixels, out Vector2 position)
        {
            float radiusSqr = radiusPixels * radiusPixels;

            Touchscreen touch = Touchscreen.current;

            if (touch != null)
            {
                foreach (UnityEngine.InputSystem.Controls.TouchControl control in touch.touches)
                {
                    if (!control.press.isPressed)
                        continue;

                    Vector2 touchPosition = control.position.ReadValue();

                    if ((touchPosition - center).sqrMagnitude > radiusSqr)
                        continue;

                    dragTouchId = control.touchId.ReadValue();
                    position = touchPosition;
                    return true;
                }
            }

            Mouse mouse = Mouse.current;

            if (mouse != null && mouse.leftButton.isPressed)
            {
                Vector2 mousePosition = mouse.position.ReadValue();

                if ((mousePosition - center).sqrMagnitude <= radiusSqr)
                {
                    dragTouchId = -1;
                    position = mousePosition;
                    return true;
                }
            }

            position = Vector2.zero;
            return false;
        }

        // 드래그를 시작한 포인터의 현재 위치. 끝났으면 false
        bool TryReadDragPointer(out Vector2 position)
        {
            if (dragTouchId >= 0)
            {
                Touchscreen touch = Touchscreen.current;

                if (touch != null)
                {
                    foreach (UnityEngine.InputSystem.Controls.TouchControl control in touch.touches)
                    {
                        if (!control.press.isPressed)
                            continue;

                        if (control.touchId.ReadValue() != dragTouchId)
                            continue;

                        position = control.position.ReadValue();
                        return true;
                    }
                }

                position = Vector2.zero;
                return false;
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

        void ApplySprite(RectTransform target)
        {
            Image image = target.GetComponent<Image>();

            if (image == null)
                return;

            image.sprite = circleSprite;
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
