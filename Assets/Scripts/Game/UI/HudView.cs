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

        // 버린 아이템이 놓이는 거리 (m). 발밑 조금 앞
        const float DropForwardDistance = 0.9f;

        // 드래그 잔상 크기의 절반 (px) - 포인터를 잔상 중앙에 맞춘다
        const float GhostHalfSize = 22f;

        UIDocument document;
        VisualElement root;

        VisualElement damageFlash;
        VisualElement healthFill;
        VisualElement loadFill;
        VisualElement bagSlotRow;
        VisualElement resultPanel;
        VisualElement gaugeBox;
        VisualElement gaugeFill;
        Label gaugeText;
        VisualElement bagPanel;
        VisualElement dragGhost;
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
            UpdateInteractGauge(running);
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

            // 하단 중앙 게이지 - 아이템 오브젝트 상호작용 진행도 (파밍 문서 3.3)
            gaugeBox = root.Q<VisualElement>("bottom-center");
            gaugeFill = root.Q<VisualElement>("loot-gauge-fill");
            gaugeText = root.Q<Label>("loot-gauge-text");

            // 가방 버리기 (드래그앤드롭) - 슬롯을 가방 밖으로 끌어내면 떨어뜨린다
            bagPanel = root.Q<VisualElement>("bag-panel");
            dragGhost = root.Q<VisualElement>("drag-ghost");

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

            // 노출 조건 (드랍은 자동 수집이라 제외): 조각 줍기 대상이 있거나,
            // 파밍 포인트의 아이템 오브젝트가 사거리에 있거나, 여는 중일 때
            bool visible = running && HasInteractTarget();

            SetVisible(interactButton, visible);

            if (visible)
            {
                interactButton.text = ResolveInteractLabel();
                return;
            }

            if (player != null)
                player.SetExternalInteractHeld(false);
        }

        static bool HasInteractTarget()
        {
            if (LootPickup.PromptTarget != null)
                return true;

            if (Field.FarmingObject.Active != null)
                return true;

            return Field.FarmingObject.PromptTarget != null;
        }

        static string ResolveInteractLabel()
        {
            if (Field.FarmingObject.Active != null)
                return "여는 중";

            if (Field.FarmingObject.PromptTarget != null)
                return "열기 (홀드)";

            return "줍기";
        }

        /// <summary>
        /// 아이템 오브젝트 상호작용 게이지 (파밍 문서 3.3). 진행 중에만 노출한다 -
        /// 이동/홀드 해제로 중단되면 게이지도 함께 사라져 중단이 바로 읽힌다.
        /// </summary>
        void UpdateInteractGauge(bool running)
        {
            if (gaugeBox == null)
                return;

            Field.FarmingObject active = Field.FarmingObject.Active;
            bool visible = running && active != null;

            SetVisible(gaugeBox, visible);

            if (!visible)
                return;

            if (gaugeFill != null)
                SetFillRatio(gaugeFill, active.Progress01);

            if (gaugeText != null)
                gaugeText.text = $"{Mathf.RoundToInt(active.Progress01 * 100f)}%";
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

                // 버리기 드래그를 받으려면 픽킹이 켜져 있어야 한다.
                // 가방은 우하단 작은 영역이라 게임 클릭을 크게 가리지 않는다
                slot.pickingMode = PickingMode.Position;
                RegisterSlotDrag(slot, i);

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

        // -- 가방 버리기 (드래그앤드롭, 사용자 지시 2026-07-26) -------------------

        // 끌고 있는 슬롯 번호와 포인터. -1이면 드래그 중이 아니다
        int draggingSlot = -1;
        int draggingPointer = -1;

        // 끌기 시작한 아이템의 정체. 드래그 도중에도 자동 수집/합성으로 슬롯 순서가
        // 바뀔 수 있어, 번호만 믿으면 엉뚱한 아이템을 버리게 된다
        string draggingId;
        int draggingGrade;

        /// <summary>
        /// 슬롯을 가방 밖으로 끌어내면 발밑에 떨어뜨린다. 떨어진 아이템은 다시 주울 수
        /// 있고 등급/몫을 유지한다 (가방 문서 6장의 "버리기"를 드래그로 구현).
        ///
        /// 포인터 캡처를 쓰는 이유는 조이스틱과 같다 - 캡처가 멀티터치 추적을 대신한다.
        /// </summary>
        void RegisterSlotDrag(VisualElement slot, int slotIndex)
        {
            slot.RegisterCallback<PointerDownEvent>(evt => BeginSlotDrag(evt, slot, slotIndex));
            slot.RegisterCallback<PointerMoveEvent>(OnSlotDragMove);
            slot.RegisterCallback<PointerUpEvent>(evt => EndSlotDrag(evt, slot));

            // 캡처가 풀리면(창 밖 등) 드래그도 끝난다 - 잔상이 남지 않게
            slot.RegisterCallback<PointerCaptureOutEvent>(_ => ClearSlotDrag());
        }

        void BeginSlotDrag(PointerDownEvent evt, VisualElement slot, int slotIndex)
        {
            RunManager run = RunManager.Instance;

            if (run == null || run.StateMachine.Current != RunState.Running)
                return;

            if (slotIndex >= run.Inventory.Entries.Count)
                return;

            RunInventory.Entry entry = run.Inventory.Entries[slotIndex];

            draggingSlot = slotIndex;
            draggingPointer = evt.pointerId;
            draggingId = entry.Definition.id;
            draggingGrade = entry.Grade;

            slot.CapturePointer(evt.pointerId);

            ShowDragGhost(entry, evt.position);

            evt.StopPropagation();
        }

        void OnSlotDragMove(PointerMoveEvent evt)
        {
            if (draggingSlot < 0 || evt.pointerId != draggingPointer)
                return;

            MoveDragGhost(evt.position);

            evt.StopPropagation();
        }

        void EndSlotDrag(PointerUpEvent evt, VisualElement slot)
        {
            if (draggingSlot < 0 || evt.pointerId != draggingPointer)
                return;

            int slotIndex = ResolveDraggingSlot();
            bool outsideBag = IsOutsideBag(evt.position);

            slot.ReleasePointer(evt.pointerId);
            ClearSlotDrag();

            evt.StopPropagation();

            // 가방 안에서 놓으면 취소 (자리 이동은 아직 없다)
            if (!outsideBag)
                return;

            DropSlot(slotIndex);
        }

        /// <summary>
        /// 끌기 시작한 아이템이 지금 몇 번 슬롯인지 다시 찾는다. 드래그 도중의
        /// 자동 수집/합성으로 순서가 바뀌었으면 번호가 어긋난다 - 못 찾으면 -1.
        /// </summary>
        int ResolveDraggingSlot()
        {
            RunManager run = RunManager.Instance;

            if (run == null || draggingId == null)
                return -1;

            var entries = run.Inventory.Entries;

            if (draggingSlot >= 0 && draggingSlot < entries.Count)
            {
                RunInventory.Entry atSlot = entries[draggingSlot];

                if (atSlot.Definition.id == draggingId && atSlot.Grade == draggingGrade)
                    return draggingSlot;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Definition.id == draggingId && entries[i].Grade == draggingGrade)
                    return i;
            }

            return -1;
        }

        /// <summary>
        /// 슬롯 하나를 월드로 내보낸다. 월드에 놓지 못하면 가방으로 되돌린다 -
        /// 인벤토리에서 뺀 뒤 놓기에 실패하면 아이템이 증발한다.
        /// </summary>
        void DropSlot(int slotIndex)
        {
            if (slotIndex < 0)
                return;

            RunManager run = RunManager.Instance;

            if (run == null || run.StateMachine.Current != RunState.Running)
                return;

            if (!run.Inventory.TryDropOne(slotIndex, out RunInventory.DroppedItem item))
                return;

            Field.FieldSpawner field = Field.FieldSpawner.Instance;

            if (field == null || player == null)
            {
                run.Inventory.Restore(item);
                return;
            }

            if (field.DropBagItem(item, ResolveDropPosition()) == null)
                run.Inventory.Restore(item);
        }

        // 발밑 조금 앞. 자동 수집이 되빨아들이지 않는 것은 LootSpot의 재무장 규칙이 맡는다
        Vector3 ResolveDropPosition()
        {
            Vector3 position = player.transform.position;

            return new Vector3(position.x, position.y, position.z + DropForwardDistance);
        }

        void ShowDragGhost(RunInventory.Entry entry, Vector2 pointerPosition)
        {
            if (dragGhost == null)
                return;

            dragGhost.style.backgroundColor =
                new StyleColor(LootDefinition.GradeColor(entry.Grade));

            SetVisible(dragGhost, true);
            MoveDragGhost(pointerPosition);
        }

        void MoveDragGhost(Vector2 pointerPosition)
        {
            if (dragGhost == null)
                return;

            dragGhost.style.left = pointerPosition.x - GhostHalfSize;
            dragGhost.style.top = pointerPosition.y - GhostHalfSize;

            if (IsOutsideBag(pointerPosition))
                dragGhost.AddToClassList("drag-ghost--drop");
            else
                dragGhost.RemoveFromClassList("drag-ghost--drop");
        }

        void ClearSlotDrag()
        {
            draggingSlot = -1;
            draggingPointer = -1;
            draggingId = null;
            draggingGrade = 0;

            if (dragGhost == null)
                return;

            dragGhost.RemoveFromClassList("drag-ghost--drop");
            SetVisible(dragGhost, false);
        }

        bool IsOutsideBag(Vector2 pointerPosition)
        {
            if (bagPanel == null)
                return true;

            return !bagPanel.worldBound.Contains(pointerPosition);
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
