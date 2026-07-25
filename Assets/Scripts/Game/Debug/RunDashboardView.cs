using Scavenger.Loot;
using Scavenger.Player;
using Scavenger.Run;
using UnityEngine;
using UnityEngine.UIElements;

namespace Scavenger.Diagnostics
{
    /// <summary>
    /// 진단 대시보드 (구 IMGUI RunDebugDashboard 대체, 사용자 지시 2026-07-26).
    /// HUD와 같은 UIDocument에 Dashboard.uxml을 얹어 그린다 - 패널 설정/폰트/스케일을
    /// HUD와 공유하므로 별도 문서를 두지 않는다.
    ///
    /// 표시 규칙: **기본은 접힌 상태**(헤더 한 줄 + 요약)이고, 헤더를 누르면 펼친다.
    /// F1은 패널 자체의 표시/숨김이다. 숨김 정보(타이머 실수치, 제거선 거리)는
    /// 플레이어 HUD가 아니라 여기에만 노출한다.
    ///
    /// 실행 순서 -140: HudView(-150)가 루트에 폰트를 넣은 뒤에 트리를 붙인다
    /// (폰트는 상속이라 순서가 뒤바뀌면 첫 프레임에 글자가 비어 보인다).
    /// </summary>
    [DefaultExecutionOrder(-140)]
    [RequireComponent(typeof(UIDocument))]
    public sealed class RunDashboardView : MonoBehaviour
    {
        [Header("대시보드 레이아웃 (Assets/UI/Dashboard.uxml)")]
        public VisualTreeAsset dashboardTree;

        [Header("시작 시 접어 둔다 (사용자 지시)")]
        public bool startFolded = true;

        [Header("갱신 간격 (초). 매 프레임 문자열을 만들지 않는다")]
        public float refreshInterval = 0.1f;

#if UNITY_EDITOR || DEVELOPMENT_BUILD

        // 붕괴 기준선이 이 거리 안이면 값에 경고 색을 준다
        const float RemoveLineAlertDistance = 12f;

        UIDocument document;
        VisualElement panel;
        VisualElement body;

        Label summaryLabel;
        Label foldLabel;

        Label runState;
        Label runSeed;
        Label runTime;
        Label runStageTime;

        Label fieldStage;
        Label fieldZone;
        Label fieldRemoveZ;
        Label fieldRemoveDistance;
        VisualElement fieldProgressFill;
        Label fieldProgressText;

        Label playerState;
        Label playerPosition;
        Label playerSafe;
        Label playerInteract;

        Label lootKinds;
        Label lootValue;
        Label lootWeight;
        Label lootField;

        PlayerController player;

        bool folded;
        bool visible = true;
        float refreshTimer;

        // 스테이지 체류 시간 (2-3분 라운드 템포 검증용, 구현계획 v0.0.2 2.3)
        int observedDepth = -1;
        float depthEnteredAt;

        void OnEnable()
        {
            document = GetComponent<UIDocument>();
            folded = startFolded;
        }

        void OnDisable()
        {
            if (panel != null)
                panel.RemoveFromHierarchy();

            panel = null;
        }

        void Update()
        {
            if (!EnsureBound())
                return;

            ReadToggleInput();

            if (!visible)
                return;

            refreshTimer -= Time.deltaTime;

            if (refreshTimer > 0f)
                return;

            refreshTimer = Mathf.Max(0.02f, refreshInterval);

            Refresh();
        }

        // -- 바인딩 -----------------------------------------------------------

        /// <summary>
        /// UIDocument는 자기 OnEnable에서 트리를 만들고 이 컴포넌트가 더 먼저 도므로,
        /// 루트가 생길 때까지 매 프레임 다시 시도한다 (HUD 뷰들과 같은 규칙).
        /// </summary>
        bool EnsureBound()
        {
            if (panel != null)
                return true;

            if (document == null || dashboardTree == null)
                return false;

            VisualElement root = document.rootVisualElement;

            if (root == null)
                return false;

            panel = dashboardTree.CloneTree();

            // CloneTree는 래퍼 하나를 씌운다 - 래퍼가 레이아웃을 먹지 않게 통과시킨다
            panel.style.position = Position.Absolute;
            panel.style.left = 0f;
            panel.style.top = 0f;
            panel.style.right = 0f;
            panel.style.bottom = 0f;
            panel.pickingMode = PickingMode.Ignore;

            root.Add(panel);

            QueryElements();
            ApplyFoldState();
            ApplyVisibleState();

            return true;
        }

        void QueryElements()
        {
            body = panel.Q<VisualElement>("dash-body");
            summaryLabel = panel.Q<Label>("dash-summary");
            foldLabel = panel.Q<Label>("dash-fold");

            Button header = panel.Q<Button>("dash-header");

            if (header != null)
                header.clicked += ToggleFold;

            runState = panel.Q<Label>("run-state");
            runSeed = panel.Q<Label>("run-seed");
            runTime = panel.Q<Label>("run-time");
            runStageTime = panel.Q<Label>("run-stage-time");

            fieldStage = panel.Q<Label>("field-stage");
            fieldZone = panel.Q<Label>("field-zone");
            fieldRemoveZ = panel.Q<Label>("field-remove-z");
            fieldRemoveDistance = panel.Q<Label>("field-remove-distance");
            fieldProgressFill = panel.Q<VisualElement>("field-progress-fill");
            fieldProgressText = panel.Q<Label>("field-progress-text");

            playerState = panel.Q<Label>("player-state");
            playerPosition = panel.Q<Label>("player-position");
            playerSafe = panel.Q<Label>("player-safe");
            playerInteract = panel.Q<Label>("player-interact");

            lootKinds = panel.Q<Label>("loot-kinds");
            lootValue = panel.Q<Label>("loot-value");
            lootWeight = panel.Q<Label>("loot-weight");
            lootField = panel.Q<Label>("loot-field");
        }

        // -- 토글 -------------------------------------------------------------

        void ReadToggleInput()
        {
            UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;

            if (keyboard == null)
                return;

            if (!keyboard.f1Key.wasPressedThisFrame)
                return;

            visible = !visible;
            ApplyVisibleState();
        }

        void ToggleFold()
        {
            folded = !folded;
            ApplyFoldState();
        }

        void ApplyFoldState()
        {
            if (body != null)
                SetVisible(body, !folded);

            if (foldLabel == null)
                return;

            // 접힘 표시는 텍스트로 둔다 (프로젝트 규약: 이모지/특수문자 금지)
            if (folded)
            {
                foldLabel.text = "[+]";
                return;
            }

            foldLabel.text = "[-]";
        }

        void ApplyVisibleState()
        {
            VisualElement root = panel.Q<VisualElement>("dash-root");

            if (root == null)
                return;

            SetVisible(root, visible);
        }

        // -- 갱신 -------------------------------------------------------------

        void Refresh()
        {
            RunManager run = RunManager.Instance;

            if (run == null)
            {
                SetText(summaryLabel, "RunManager 없음");
                return;
            }

            ResolvePlayer();

            RefreshRun(run);
            RefreshField();
            RefreshPlayer();
            RefreshLoot(run);
            RefreshSummary(run);
        }

        void RefreshRun(RunManager run)
        {
            SetText(runState, run.StateMachine.Current.ToString());
            SetText(runSeed, $"{run.Seed} / {run.Depth}");

            if (run.Timer != null)
                SetText(runTime, $"{run.Timer.Elapsed:F1}s / {Mathf.Max(0f, run.Timer.Remaining):F1}s");

            if (observedDepth != run.Depth)
            {
                observedDepth = run.Depth;
                depthEnteredAt = Time.time;
            }

            SetText(runStageTime, $"{Time.time - depthEnteredAt:F1}s (목표 150s)");
        }

        void RefreshField()
        {
            Field.FieldSpawner field = Field.FieldSpawner.Instance;

            if (field == null)
            {
                SetText(fieldStage, "-");
                return;
            }

            SetText(fieldStage, $"{field.StageIndex + 1} (존 {field.StageZoneCount}개)");

            // 구간(존 사이)에 있으면 존 인덱스가 -1이다
            if (field.CurrentZoneIndex >= 0)
                SetText(fieldZone, $"{field.CurrentZoneIndex + 1} / {field.StageZoneCount}");
            else
                SetText(fieldZone, "구간 (웨이포인트)");

            float progress = Mathf.Clamp01(field.StageProgress01);

            if (fieldProgressFill != null)
            {
                fieldProgressFill.style.width =
                    new StyleLength(new Length(progress * 100f, LengthUnit.Percent));
            }

            SetText(fieldProgressText, $"{Mathf.RoundToInt(progress * 100f)}%");
            SetText(fieldRemoveZ, $"{field.RemoveLineZ:F1}");

            float distance = field.RemoveLineDistanceToPlayer;

            if (float.IsPositiveInfinity(distance))
            {
                SetText(fieldRemoveDistance, "-");
                SetAlert(fieldRemoveDistance, false);
                return;
            }

            SetText(fieldRemoveDistance, $"{distance:F1}m");
            SetAlert(fieldRemoveDistance, distance <= RemoveLineAlertDistance);
        }

        void RefreshPlayer()
        {
            if (player == null)
            {
                SetText(playerState, "-");
                return;
            }

            SetText(playerState, player.State.ToString());

            Vector3 position = player.transform.position;
            SetText(playerPosition, $"x {position.x:F1} / y {position.y:F1} / z {position.z:F1}");

            Field.FarmingPoint point = Field.FarmingPoint.PlayerInside;

            if (point != null)
                SetText(playerSafe, $"{point.PointType} / {point.Grade}");
            else
                SetText(playerSafe, "-");

            RefreshInteract();
        }

        // 파밍 오브젝트 상호작용 상태 - 진행률은 플레이어 HUD에도 있지만
        // 대상/등급까지는 여기서만 본다
        void RefreshInteract()
        {
            Field.FarmingObject active = Field.FarmingObject.Active;

            if (active != null)
            {
                SetText(playerInteract,
                    $"{active.kind} {active.grade} {Mathf.RoundToInt(active.Progress01 * 100f)}%");

                return;
            }

            Field.FarmingObject prompt = Field.FarmingObject.PromptTarget;

            if (prompt != null)
            {
                SetText(playerInteract, $"{prompt.kind} {prompt.grade} 대기");
                return;
            }

            SetText(playerInteract, "-");
        }

        void RefreshLoot(RunManager run)
        {
            int slotCapacity = run.Inventory.SlotCapacity;

            if (slotCapacity == RunInventory.UnlimitedSlots)
                SetText(lootKinds, $"{run.Inventory.Entries.Count} / 무제한");
            else
                SetText(lootKinds, $"{run.Inventory.UsedSlots} / {slotCapacity}");

            SetText(lootValue, run.Inventory.TotalValue.ToString());
            SetText(lootWeight, $"{run.Inventory.TotalWeight:F1}kg");
            SetText(lootField, $"드랍 {LootSpot.All.Count} / 조각 {LootPickup.All.Count}");
        }

        // 접힌 상태에서 헤더에 붙는 한 줄 요약 - 펼치지 않아도 최소 정보는 보인다
        void RefreshSummary(RunManager run)
        {
            float distance = Field.FieldSpawner.Instance != null
                ? Field.FieldSpawner.Instance.RemoveLineDistanceToPlayer
                : float.PositiveInfinity;

            if (float.IsPositiveInfinity(distance))
            {
                SetText(summaryLabel, $"D{run.Depth} / {run.StateMachine.Current}");
                return;
            }

            SetText(summaryLabel, $"D{run.Depth} / {run.StateMachine.Current} / 붕괴 {distance:F0}m");
        }

        void ResolvePlayer()
        {
            if (player != null)
                return;

            player = FindFirstObjectByType<PlayerController>();
        }

        // -- 헬퍼 -------------------------------------------------------------

        static void SetText(Label label, string text)
        {
            if (label == null)
                return;

            label.text = text;
        }

        static void SetAlert(Label label, bool alert)
        {
            if (label == null)
                return;

            if (alert)
            {
                label.AddToClassList("dash-value--alert");
                return;
            }

            label.RemoveFromClassList("dash-value--alert");
        }

        static void SetVisible(VisualElement element, bool value)
        {
            element.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
        }
#endif
    }
}
