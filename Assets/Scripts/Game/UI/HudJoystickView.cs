using Scavenger.Player;
using Scavenger.Run;
using UnityEngine;
using UnityEngine.UIElements;

namespace Scavenger.UI
{
    /// <summary>
    /// UI Toolkit 가상 조이스틱 (ADR-0007. uGUI VirtualJoystick 대체).
    /// 베이스 원 안에서 드래그 = 2D 벡터 입력 -> PlayerController.SetExternalMoveInput.
    /// 포인터를 직접 판독하지 않고 UI Toolkit 포인터 이벤트를 쓴다 -
    /// 포인터 캡처가 멀티터치 추적(다른 손가락이 드래그를 뺏는 문제)을 대신 처리한다.
    /// 실행 순서 -200: PlayerController(-100)가 읽기 전에 벡터를 갱신.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    [RequireComponent(typeof(UIDocument))]
    public sealed class HudJoystickView : MonoBehaviour
    {
        [Header("입력 (반경 비율 데드존)")]
        [Range(0f, 0.5f)] public float deadZone = 0.12f;

        /// <summary>현재 입력 벡터 (-1..1). 디버그 참조용.</summary>
        public Vector2 Value { get; private set; }

        UIDocument document;
        VisualElement baseElement;
        VisualElement knobElement;
        PlayerController player;

        int activePointerId = -1;
        Vector2 knobRestPosition;

        void OnEnable()
        {
            document = GetComponent<UIDocument>();
        }

        void OnDisable()
        {
            Release();
            baseElement = null;
        }

        void Update()
        {
            if (!EnsureBound())
                return;

            bool running = IsRunActive();

            // 런이 끝나면 조이스틱을 숨기고 입력을 놓는다 (결과 화면에서 계속 걷지 않게)
            baseElement.style.display = running ? DisplayStyle.Flex : DisplayStyle.None;

            if (!running && activePointerId != -1)
                Release();
        }

        bool EnsureBound()
        {
            if (baseElement != null)
                return true;

            if (document == null)
                return false;

            VisualElement root = document.rootVisualElement;

            if (root == null)
                return false;

            baseElement = root.Q<VisualElement>("joystick-base");
            knobElement = root.Q<VisualElement>("joystick-knob");

            if (baseElement == null)
                return false;

            baseElement.RegisterCallback<PointerDownEvent>(OnPointerDown);
            baseElement.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            baseElement.RegisterCallback<PointerUpEvent>(OnPointerUp);
            baseElement.RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);

            return true;
        }

        void OnPointerDown(PointerDownEvent evt)
        {
            if (activePointerId != -1)
                return;

            if (!IsRunActive())
                return;

            activePointerId = evt.pointerId;
            baseElement.CapturePointer(evt.pointerId);

            ApplyPointer(evt.localPosition);
            evt.StopPropagation();
        }

        void OnPointerMove(PointerMoveEvent evt)
        {
            if (evt.pointerId != activePointerId)
                return;

            ApplyPointer(evt.localPosition);
            evt.StopPropagation();
        }

        void OnPointerUp(PointerUpEvent evt)
        {
            if (evt.pointerId != activePointerId)
                return;

            baseElement.ReleasePointer(evt.pointerId);
            Release();
            evt.StopPropagation();
        }

        // 캡처가 외부 사정으로 풀릴 때도 입력을 놓는다 (놓지 않으면 계속 걷는다)
        void OnPointerCaptureOut(PointerCaptureOutEvent evt)
        {
            if (evt.pointerId != activePointerId)
                return;

            Release();
        }

        void ApplyPointer(Vector3 localPosition)
        {
            float radius = baseElement.resolvedStyle.width * 0.5f;

            if (radius <= 0f)
                return;

            Vector2 center = new Vector2(radius, radius);

            // 패널 좌표는 y가 아래로 증가한다 - 화면 기준(위가 +)으로 맞춘다
            Vector2 offset = new Vector2(
                localPosition.x - center.x,
                center.y - localPosition.y);

            Vector2 raw = Vector2.ClampMagnitude(offset / radius, 1f);

            if (raw.magnitude < deadZone)
                raw = Vector2.zero;

            Value = raw;
            PushInput(raw);
            MoveKnob(raw, radius);
        }

        void Release()
        {
            activePointerId = -1;
            Value = Vector2.zero;

            PushInput(Vector2.zero);

            if (knobElement == null)
                return;

            knobElement.style.translate = new StyleTranslate(new Translate(0f, 0f));
        }

        void MoveKnob(Vector2 raw, float radius)
        {
            if (knobElement == null)
                return;

            // 노브는 반경의 절반까지만 나간다 (원 안에 머무는 느낌)
            float travel = radius * 0.5f;

            knobElement.style.translate = new StyleTranslate(
                new Translate(raw.x * travel, -raw.y * travel));
        }

        void PushInput(Vector2 raw)
        {
            if (player == null)
                player = FindFirstObjectByType<PlayerController>();

            if (player == null)
                return;

            player.SetExternalMoveInput(raw);
        }

        static bool IsRunActive()
        {
            RunManager run = RunManager.Instance;

            if (run == null)
                return false;

            return run.StateMachine.Current == RunState.Running;
        }
    }
}
