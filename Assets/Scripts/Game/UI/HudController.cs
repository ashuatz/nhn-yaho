using Scavenger.Loot;
using Scavenger.Player;
using Scavenger.Run;
using Scavenger.Segment;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Scavenger.UI
{
    /// <summary>
    /// uGUI HUD 컨트롤러 (M5-1, IMGUI HudOverlay 대체). HudCanvas 프리팹 루트에 상주.
    /// 패널 참조는 에디터 템플릿(HudCanvasTemplate)이 배선한다 - 런타임 생성 없음.
    /// 상호작용 프롬프트 박스는 모바일 홀드 버튼을 겸한다 (E 키와 OR 합성).
    /// 주의: 숨김 정보(타이머 실수치, 실거리)는 여기 노출 금지 - 대시보드 전용.
    /// </summary>
    public sealed class HudController : MonoBehaviour
    {
        [Header("가치/무게 (좌하단)")]
        public GameObject valueRoot;
        public Text valueText;

        [Header("상호작용 프롬프트 (우하단) - 터치 홀드 버튼 겸용")]
        public GameObject interactRoot;
        public Text interactText;

        [Header("루팅 게이지")]
        public GameObject gaugeRoot;
        public RectTransform gaugeFill;
        public Text gaugeLabel;

        [Header("선택지 프롬프트")]
        public GameObject choiceRoot;
        public Text choiceText;

        [Header("신호 배너")]
        public GameObject signalRoot;
        public Text signalText;

        [Header("붕괴 근접 경고")]
        public GameObject collapseRoot;

        [Header("런 결과")]
        public GameObject resultRoot;
        public Text resultText;

        const float SignalBannerSeconds = 3.5f;
        const float CollapseWarningDistance = 12f;
        const float GaugeFillPadding = 4f;

        CarryLoad carryLoad;
        PlayerController player;

        void Update()
        {
            RunManager run = RunManager.Instance;

            if (run == null)
            {
                SetAllInactive();
                return;
            }

            bool running = run.StateMachine.Current == RunState.Running;

            UpdateValue(run);
            UpdateInteract(running);
            UpdateGauge();
            UpdateChoice();
            UpdateSignal();
            UpdateCollapse(running);
            UpdateResult(run);
            UpdateInteractHold();
        }

        void SetAllInactive()
        {
            SetActive(valueRoot, false);
            SetActive(interactRoot, false);
            SetActive(gaugeRoot, false);
            SetActive(choiceRoot, false);
            SetActive(signalRoot, false);
            SetActive(collapseRoot, false);
            SetActive(resultRoot, false);
        }

        // 무게 상태 (M2-1): 가치 합계 아래에 무게와 적재 단계를 함께 표시
        void UpdateValue(RunManager run)
        {
            SetActive(valueRoot, true);

            if (valueText == null)
                return;

            valueText.text = $"가치 합계: {run.Inventory.TotalValue}\n{BuildWeightLine(run)}";
        }

        string BuildWeightLine(RunManager run)
        {
            if (carryLoad == null)
                carryLoad = FindFirstObjectByType<CarryLoad>();

            if (carryLoad == null)
                return $"무게: {run.Inventory.TotalWeight:F1}";

            return $"무게: {run.Inventory.TotalWeight:F1} ({CarryLoad.StageLabel(carryLoad.Stage)})";
        }

        // 상호작용 키 프롬프트: 루팅 시작 / 조각 줍기 / 루팅 중 안내.
        // 루팅 중에도 유지 - 이 박스가 모바일 홀드 버튼이라 사라지면 즉시 취소된다
        void UpdateInteract(bool running)
        {
            if (!running)
            {
                SetActive(interactRoot, false);
                return;
            }

            string prompt = BuildInteractPrompt();

            if (prompt == null)
            {
                SetActive(interactRoot, false);
                return;
            }

            SetActive(interactRoot, true);

            if (interactText != null)
                interactText.text = prompt;
        }

        static string BuildInteractPrompt()
        {
            if (LootSpot.Active != null)
                return "루팅 중 - 놓으면 취소";

            LootSpot spot = LootSpot.PromptTarget;

            if (spot != null && spot.Definition != null)
                return $"E 꾹 눌러 루팅\n{spot.Definition.displayName} (+{spot.Definition.value})";

            LootPickup pickup = LootPickup.PromptTarget;

            if (pickup != null && pickup.Definition != null)
                return $"E 눌러 줍기\n{pickup.Definition.displayName} (+{pickup.Definition.value})";

            return null;
        }

        void UpdateGauge()
        {
            LootSpot active = LootSpot.Active;

            if (active == null || active.Definition == null)
            {
                SetActive(gaugeRoot, false);
                return;
            }

            SetActive(gaugeRoot, true);

            if (gaugeLabel != null)
                gaugeLabel.text = $"루팅 중: {active.Definition.displayName} (+{active.Definition.value})";

            if (gaugeFill == null)
                return;

            RectTransform track = gaugeFill.parent as RectTransform;

            if (track == null)
                return;

            float maxWidth = track.rect.width - GaugeFillPadding;
            gaugeFill.sizeDelta = new Vector2(maxWidth * active.Progress01, gaugeFill.sizeDelta.y);
        }

        void UpdateChoice()
        {
            ChoiceNode active = ChoiceNode.Active;

            SetActive(choiceRoot, active != null);

            if (active != null && choiceText != null)
                choiceText.text = "선택하라\nW: 더 깊이 전진 (고가치/고위험)  E: 탈출 (확정)";
        }

        void UpdateSignal()
        {
            bool visible = !string.IsNullOrEmpty(SignalEmitter.LastMessage)
                && Time.time - SignalEmitter.LastMessageAt <= SignalBannerSeconds;

            SetActive(signalRoot, visible);

            if (visible && signalText != null)
                signalText.text = SignalEmitter.LastMessage;
        }

        // 시간 압박 인지 표현 (ADR-0006): 붕괴 전선 근접 경고
        void UpdateCollapse(bool running)
        {
            CollapseFront collapse = CollapseFront.Instance;

            bool visible = running
                && collapse != null
                && collapse.DistanceToPlayer <= CollapseWarningDistance;

            SetActive(collapseRoot, visible);
        }

        void UpdateResult(RunManager run)
        {
            RunState state = run.StateMachine.Current;

            if (state != RunState.Extracted && state != RunState.Dead)
            {
                SetActive(resultRoot, false);
                return;
            }

            SetActive(resultRoot, true);

            if (resultText == null)
                return;

            if (state == RunState.Extracted)
            {
                resultText.text = $"탈출 성공. 확보 가치: {run.Inventory.TotalValue}\n클릭: 계속 전진";
                return;
            }

            resultText.text = "사망. 획득물 전량 손실\n클릭: 계속";
        }

        // 프롬프트 박스 = 모바일 홀드 버튼 (M5-1). EventSystem 없이 포인터 직접 판독 -
        // 조이스틱과 동일 방식. 누르는 동안 매 프레임 공급한다
        void UpdateInteractHold()
        {
            if (!TryResolvePlayer())
                return;

            bool held = interactRoot != null
                && interactRoot.activeSelf
                && ReadPointerPressed(out Vector2 pointer)
                && RectTransformUtility.RectangleContainsScreenPoint(
                    (RectTransform)interactRoot.transform, pointer, null);

            player.SetExternalInteractHeld(held);
        }

        bool TryResolvePlayer()
        {
            if (player != null)
                return true;

            player = FindFirstObjectByType<PlayerController>();
            return player != null;
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

        static void SetActive(GameObject target, bool active)
        {
            if (target == null)
                return;

            if (target.activeSelf != active)
                target.SetActive(active);
        }
    }
}
