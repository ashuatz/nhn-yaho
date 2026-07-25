using Scavenger.Loot;
using Scavenger.Player;
using Scavenger.Run;
using UnityEngine;
using UnityEngine.UIElements;

namespace Scavenger.UI
{
    /// <summary>
    /// UI Toolkit HUD (uGUI HudController 대체, 사용자 지시 2026-07-25).
    /// 레이아웃/색은 Assets/UI/Hud.uxml + Hud.uss가 소유하고, 이 클래스는 수치만 넣는다.
    /// 실행 순서 -150: 입력 공급이 PlayerController(-100)보다 먼저.
    ///
    /// 트리 바인딩은 지연 처리한다 - UIDocument는 자기 OnEnable에서 트리를 만들고
    /// 이 컴포넌트는 실행 순서가 더 빨라, OnEnable 시점에는 루트가 아직 없다.
    ///
    /// 조이스틱은 HudJoystickView, 월드 아이템 라벨은 HudLootLabelView가 담당한다
    /// (같은 UIDocument를 각자 조회 - 서로 의존하지 않는다).
    /// </summary>
    [DefaultExecutionOrder(-150)]
    [RequireComponent(typeof(UIDocument))]
    public sealed class HudView : MonoBehaviour
    {
        [Header("붕괴 경고를 띄우는 거리 (m)")]
        public float collapseWarningDistance = 12f;

        // 바가 값을 따라가는 속도 (지수 보간) - 숫자가 튀지 않게
        const float BarLerpSpeed = 10f;

        const float DamageFlashSeconds = 0.35f;
        const float DamageFlashAlpha = 0.35f;
        const float AnnounceSeconds = 2.2f;

        // 진행 강조 메시지 (웹 이식): 통과 시 1회 표시
        static readonly float[] Milestones = { 0.25f, 0.5f, 0.75f, 0.9f };

        // 가방 칸이 없을 때(무제한 구성) 보여줄 칸 수
        const int UnlimitedSlotDisplayCount = 8;

        UIDocument document;
        VisualElement root;

        VisualElement damageFlash;
        VisualElement healthFill;
        VisualElement loadFill;
        VisualElement bagSlotRow;
        VisualElement resultPanel;
        Label timerText;
        Label healthText;
        Label loadText;
        Label objectiveText;
        Label announceText;
        Label warningText;
        Label bagWeightText;
        Label resultTitle;
        Label resultBody;
        Button interactButton;

        CarryLoad carryLoad;
        PlayerController player;
        PlayerHealth health;

        float healthShown = 1f;
        float loadShown;
        float flashTimer;
        float announceTimer;
        readonly bool[] milestoneShown = new bool[Milestones.Length];
        int builtSlotCount = -1;

        void OnEnable()
        {
            document = GetComponent<UIDocument>();
        }

        void OnDisable()
        {
            if (health != null)
                health.Damaged -= OnPlayerDamaged;

            root = null;
        }

        void Update()
        {
            if (!EnsureBound())
                return;

            RunManager run = RunManager.Instance;

            if (run == null)
                return;

            bool running = run.StateMachine.Current == RunState.Running;

            ResolvePlayerRefs();

            UpdateTimer(run, running);
            UpdateHealth(running);
            UpdateLoad(running);
            UpdateObjective(run, running);
            UpdateBag(run, running);
            UpdateInteract(running);
            UpdateCollapseWarning(running);
            UpdateAnnounce(running);
            UpdateResult(run);
            UpdateDamageFlash();
        }

        // -- 바인딩 -----------------------------------------------------------

        bool EnsureBound()
        {
            if (root != null)
                return true;

            if (document == null)
                return false;

            root = document.rootVisualElement;

            if (root == null)
                return false;

            ApplyFont();

            damageFlash = root.Q<VisualElement>("damage-flash");
            healthFill = root.Q<VisualElement>("health-fill");
            loadFill = root.Q<VisualElement>("load-fill");
            bagSlotRow = root.Q<VisualElement>("bag-slots");
            resultPanel = root.Q<VisualElement>("result-panel");

            timerText = root.Q<Label>("timer-text");
            healthText = root.Q<Label>("health-text");
            loadText = root.Q<Label>("load-text");
            objectiveText = root.Q<Label>("objective-text");
            announceText = root.Q<Label>("announce-text");
            warningText = root.Q<Label>("warning-text");
            bagWeightText = root.Q<Label>("bag-weight");
            resultTitle = root.Q<Label>("result-title");
            resultBody = root.Q<Label>("result-body");

            interactButton = root.Q<Button>("interact-button");

            if (interactButton != null)
            {
                interactButton.RegisterCallback<PointerDownEvent>(OnInteractPointerDown);
                interactButton.RegisterCallback<PointerUpEvent>(OnInteractPointerUp);
                interactButton.RegisterCallback<PointerLeaveEvent>(OnInteractPointerUp);
            }

            return true;
        }

        /// <summary>
        /// 루트에 내장 동적 폰트를 넣는다. 자식은 상속받으므로 조이스틱/라벨 뷰까지 함께 적용된다.
        ///
        /// USS의 -unity-font-definition: resource(...)는 런타임 패널에서 해석되지 않아
        /// 폰트가 null이 되고(글자가 아예 그려지지 않는다), 기본 런타임 테마 폰트에는
        /// 한글 글리프가 없다. 내장 LegacyRuntime.ttf는 동적 폰트라 OS 폰트로 폴백한다
        /// (uGUI HUD가 쓰던 것과 같은 폰트 - 실제로 글자가 안 나와 확인한 경로).
        /// </summary>
        void ApplyFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            if (font == null)
            {
                UnityEngine.Debug.LogWarning("[HUD] 내장 폰트를 찾지 못했다. 글자가 보이지 않는다.");
                return;
            }

            root.style.unityFontDefinition = new StyleFontDefinition(font);
        }

        void ResolvePlayerRefs()
        {
            if (player == null)
                player = FindFirstObjectByType<PlayerController>();

            if (carryLoad == null)
                carryLoad = FindFirstObjectByType<CarryLoad>();

            if (health != null)
                return;

            health = FindFirstObjectByType<PlayerHealth>();

            if (health != null)
                health.Damaged += OnPlayerDamaged;
        }

        // -- 패널 갱신 --------------------------------------------------------

        void UpdateTimer(RunManager run, bool running)
        {
            if (timerText == null)
                return;

            if (!running || run.Timer == null || run.Timer.LimitSeconds <= 0f)
            {
                timerText.text = "--:--";
                return;
            }

            float remaining = Mathf.Max(0f, run.Timer.Remaining);
            int minutes = Mathf.FloorToInt(remaining / 60f);
            int seconds = Mathf.FloorToInt(remaining - minutes * 60f);

            timerText.text = $"{minutes:00}:{seconds:00}";
        }

        void UpdateHealth(bool running)
        {
            if (healthFill == null || health == null)
                return;

            if (healthText != null)
            {
                healthText.text =
                    $"HP {Mathf.CeilToInt(health.Current)}/{Mathf.CeilToInt(health.maxHealth)}";
            }

            float target = running ? health.Normalized : 0f;
            healthShown = Mathf.Lerp(healthShown, target, BarLerpSpeed * Time.deltaTime);

            SetFillRatio(healthFill, healthShown);
        }

        void UpdateLoad(bool running)
        {
            if (loadFill == null || carryLoad == null)
                return;

            if (loadText != null)
            {
                string stage = CarryLoad.StageLabel(carryLoad.Stage);
                loadText.text =
                    $"{carryLoad.TotalWeight:F1} / {carryLoad.maxCarryWeight:F0}kg ({stage})";
            }

            float target = running ? carryLoad.LoadRatio : 0f;
            loadShown = Mathf.Lerp(loadShown, target, BarLerpSpeed * Time.deltaTime);

            SetFillRatio(loadFill, loadShown);
        }

        void UpdateObjective(RunManager run, bool running)
        {
            if (objectiveText == null)
                return;

            if (!running)
            {
                objectiveText.text = "-";
                return;
            }

            objectiveText.text =
                $"종류 {run.Inventory.Entries.Count} · 가치 {run.Inventory.TotalValue}";
        }

        void UpdateInteract(bool running)
        {
            if (interactButton == null)
                return;

            // 줍기 대상이 있을 때만 노출 (드랍은 자동 수집이므로 조각 줍기 전용)
            bool visible = running && LootPickup.PromptTarget != null;

            SetVisible(interactButton, visible);

            if (visible)
                return;

            if (player != null)
                player.SetExternalInteractHeld(false);
        }

        void UpdateCollapseWarning(bool running)
        {
            if (warningText == null)
                return;

            Field.FieldSpawner field = Field.FieldSpawner.Instance;
            bool near = running && field != null
                && field.RemoveLineDistanceToPlayer <= collapseWarningDistance;

            SetVisible(warningText, near);

            if (near)
                warningText.text = "뒤에서 바닥이 무너지고 있다!";
        }

        void UpdateAnnounce(bool running)
        {
            if (announceText == null)
                return;

            if (!running)
            {
                ResetMilestones();
                SetVisible(announceText, false);
                return;
            }

            CheckMilestones();

            if (announceTimer <= 0f)
            {
                SetVisible(announceText, false);
                return;
            }

            announceTimer -= Time.deltaTime;

            // 남은 시간에 비례해 사라진다 (별도 애니메이션 없이 알파만)
            announceText.style.opacity = Mathf.Clamp01(announceTimer / AnnounceSeconds);
        }

        void CheckMilestones()
        {
            Field.FieldSpawner field = Field.FieldSpawner.Instance;

            if (field == null)
                return;

            float progress = field.StageProgress01;

            for (int i = 0; i < Milestones.Length; i++)
            {
                if (milestoneShown[i] || progress < Milestones[i])
                    continue;

                milestoneShown[i] = true;
                ShowAnnounce($"{Mathf.RoundToInt(Milestones[i] * 100f)}% 지점 통과");
            }
        }

        void ResetMilestones()
        {
            for (int i = 0; i < milestoneShown.Length; i++)
                milestoneShown[i] = false;
        }

        void ShowAnnounce(string message)
        {
            if (announceText == null)
                return;

            announceText.text = message;
            announceTimer = AnnounceSeconds;
            SetVisible(announceText, true);
        }

        void UpdateResult(RunManager run)
        {
            if (resultPanel == null)
                return;

            RunState state = run.StateMachine.Current;
            bool ended = state == RunState.Extracted || state == RunState.Dead;

            SetVisible(resultPanel, ended);

            if (!ended)
                return;

            if (resultTitle != null)
            {
                resultTitle.text = state == RunState.Extracted ? "탈출 성공" : "사망";
                resultTitle.style.color = state == RunState.Extracted
                    ? new StyleColor(new Color(0.42f, 0.85f, 0.6f))
                    : new StyleColor(new Color(0.9f, 0.35f, 0.32f));
            }

            if (resultBody == null)
                return;

            resultBody.text = state == RunState.Extracted
                ? $"확보 가치 {run.Inventory.TotalValue} · 깊이 {run.Depth}"
                : "획득물을 전부 잃었다";
        }

        void UpdateDamageFlash()
        {
            if (damageFlash == null)
                return;

            if (flashTimer <= 0f)
            {
                damageFlash.style.opacity = 0f;
                return;
            }

            flashTimer -= Time.deltaTime;
            damageFlash.style.opacity =
                Mathf.Clamp01(flashTimer / DamageFlashSeconds) * DamageFlashAlpha;
        }

        void OnPlayerDamaged(float amount)
        {
            flashTimer = DamageFlashSeconds;
        }

        // -- 가방 ------------------------------------------------------------

        void UpdateBag(RunManager run, bool running)
        {
            if (bagSlotRow == null)
                return;

            if (bagWeightText != null)
                bagWeightText.text = $"{run.Inventory.TotalWeight:F1}kg";

            int capacity = run.Inventory.SlotCapacity;
            int slotCount = capacity == RunInventory.UnlimitedSlots
                ? UnlimitedSlotDisplayCount
                : capacity;

            EnsureSlots(slotCount);

            var entries = run.Inventory.Entries;

            for (int i = 0; i < bagSlotRow.childCount; i++)
            {
                VisualElement slot = bagSlotRow[i];
                bool filled = running && i < entries.Count;

                if (filled)
                    FillSlot(slot, entries[i]);
                else
                    ClearSlot(slot);
            }
        }

        // 칸은 한 번만 만들고 재사용한다 (매 프레임 재생성 금지)
        void EnsureSlots(int slotCount)
        {
            if (builtSlotCount == slotCount)
                return;

            bagSlotRow.Clear();

            for (int i = 0; i < slotCount; i++)
            {
                VisualElement slot = new VisualElement();
                slot.AddToClassList("bag-slot");
                slot.pickingMode = PickingMode.Ignore;

                VisualElement icon = new VisualElement();
                icon.AddToClassList("bag-slot-icon");
                icon.name = "icon";
                slot.Add(icon);

                VisualElement mark = new VisualElement();
                mark.AddToClassList("bag-slot-mark");
                mark.name = "mark";
                slot.Add(mark);

                Label count = new Label();
                count.AddToClassList("bag-slot-count");
                count.name = "count";
                slot.Add(count);

                bagSlotRow.Add(slot);
            }

            builtSlotCount = slotCount;
        }

        void FillSlot(VisualElement slot, RunInventory.Entry entry)
        {
            slot.RemoveFromClassList("bag-slot--locked");

            // 외곽선 = 현재 등급 (합성으로 오른 값), 아이콘 = 아이템의 시작 등급
            SetBorderColor(slot, ResolveOutlineColor(entry.Grade));

            VisualElement icon = slot.Q<VisualElement>("icon");

            if (icon != null)
            {
                icon.style.backgroundColor =
                    new StyleColor(LootDefinition.GradeColor(entry.Definition.tier));
            }

            VisualElement mark = slot.Q<VisualElement>("mark");

            if (mark != null)
            {
                // 무게추 자리 - 압축 단계가 들어오면 색으로 표시한다 (가방 문서 6.4).
                // 압축 기능 전까지는 압축 가능 여부만 표시
                bool compressible = entry.Definition.MaxCompressStep > 0;
                mark.style.backgroundColor = new StyleColor(compressible
                    ? new Color(0.85f, 0.3f, 0.28f, 0.9f)
                    : new Color(0.4f, 0.44f, 0.5f, 0.5f));
            }

            Label count = slot.Q<Label>("count");

            if (count == null)
                return;

            count.text = entry.Count > 1 ? $"x{entry.Count}" : "";
        }

        void ClearSlot(VisualElement slot)
        {
            slot.AddToClassList("bag-slot--locked");
            SetBorderColor(slot, new Color(0.35f, 0.37f, 0.41f, 0.25f));

            VisualElement icon = slot.Q<VisualElement>("icon");

            if (icon != null)
                icon.style.backgroundColor = new StyleColor(Color.clear);

            VisualElement mark = slot.Q<VisualElement>("mark");

            if (mark != null)
                mark.style.backgroundColor = new StyleColor(Color.clear);

            Label count = slot.Q<Label>("count");

            if (count != null)
                count.text = "";
        }

        // 일반 등급은 외곽선을 눌러 표시한다 (기본 상태라 강조할 이유가 없다)
        static Color ResolveOutlineColor(int grade)
        {
            Color color = LootDefinition.GradeColor(grade);

            if (grade <= 1)
                color.a = 0.5f;

            return color;
        }

        // -- 입력 (상호작용 홀드) ---------------------------------------------

        void OnInteractPointerDown(PointerDownEvent evt)
        {
            if (player != null)
                player.SetExternalInteractHeld(true);

            interactButton.CapturePointer(evt.pointerId);
        }

        void OnInteractPointerUp(EventBase evt)
        {
            if (player != null)
                player.SetExternalInteractHeld(false);

            if (evt is PointerUpEvent up)
                interactButton.ReleasePointer(up.pointerId);
        }

        // -- 스타일 헬퍼 ------------------------------------------------------

        static void SetFillRatio(VisualElement fill, float ratio01)
        {
            fill.style.width = new StyleLength(
                new Length(Mathf.Clamp01(ratio01) * 100f, LengthUnit.Percent));
        }

        static void SetVisible(VisualElement element, bool visible)
        {
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        static void SetBorderColor(VisualElement element, Color color)
        {
            StyleColor styleColor = new StyleColor(color);

            element.style.borderLeftColor = styleColor;
            element.style.borderRightColor = styleColor;
            element.style.borderTopColor = styleColor;
            element.style.borderBottomColor = styleColor;
        }
    }
}
