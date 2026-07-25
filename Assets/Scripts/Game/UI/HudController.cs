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
    /// 선택지 패널의 전진/탈출 영역은 터치 버튼 - 키보드 없는 환경 소프트락 방지.
    /// 실행 순서 -150: PlayerController(-100)보다 먼저 홀드 상태를 공급해
    /// 1프레임 입력 지연을 없앤다 (Codex 교차 검토).
    /// 주의: 숨김 정보(타이머 실수치, 실거리)는 여기 노출 금지 - 대시보드 전용.
    /// </summary>
    [DefaultExecutionOrder(-150)]
    public sealed class HudController : MonoBehaviour
    {
        [Header("체력 바 (좌상단)")]
        public GameObject healthRoot;
        public RectTransform healthFill;
        public Text healthText;

        [Header("피격 붉은 화면 플래시 (전체 화면 오버레이)")]
        public Image damageFlash;

        [Header("가치/무게 (좌하단)")]
        public GameObject valueRoot;
        public Text valueText;

        [Header("제한 시간 카운트다운 (상단 중앙, 웹 이식). 30초 미만 빨강")]
        public GameObject timerRoot;
        public Text timerText;

        [Header("무게 바 (좌상단, 웹 이식). Fill 폭 = 적재 비율, 색 = 단계")]
        public GameObject weightRoot;
        public RectTransform weightFill;
        public Text weightText;

        [Header("탐색 목표 (우상단, 웹 이식)")]
        public GameObject objectiveRoot;
        public Text objectiveText;

        [Header("가방 그리드 (우하단 5열, 웹 이식). 슬롯은 런타임 채움")]
        public GameObject bagRoot;
        public RectTransform bagGrid;
        public Text bagWeightText;
        public GameObject bagSlotTemplate;

        [Header("획득 토스트 (좌측 스택, 웹 이식)")]
        public RectTransform toastRoot;
        public Text toastTemplate;

        [Header("진행 강조 메시지 (중앙, 웹 이식)")]
        public GameObject announceRoot;
        public Text announceText;

        [Header("상호작용 프롬프트 (우하단) - 터치 홀드 버튼 겸용")]
        public GameObject interactRoot;
        public Text interactText;

        [Header("루팅 게이지")]
        public GameObject gaugeRoot;
        public RectTransform gaugeFill;
        public Text gaugeLabel;

        [Header("선택지 프롬프트 (전진/탈출 = 터치 버튼)")]
        public GameObject choiceRoot;
        public Text choiceText;
        public RectTransform choiceAdvanceButton;
        public RectTransform choiceExtractButton;

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

        // 피격 플래시: 이 알파에서 시작해 매초 FlashFadePerSecond로 사라진다
        const float FlashPeakAlpha = 0.5f;
        const float FlashFadePerSecond = 2.2f;

        // 획득 토스트 (웹 이식): 표시 시간과 최대 스택 수
        const float ToastSeconds = 2.2f;
        const int MaxToasts = 5;

        // 진행 강조 메시지 (웹 이식): 25/50/75/90% 통과 시 1회 표시
        const float AnnounceSeconds = 2.2f;
        static readonly float[] Milestones = { 0.25f, 0.5f, 0.75f, 0.9f };

        // 등급 외곽선 알파 (색은 LootDefinition.GradeColor가 정본 - 드랍 문서 2.2).
        // 일반 등급은 외곽선을 눌러 표시하고, 희귀 이상은 또렷하게 보여준다
        const float NormalGradeOutlineAlpha = 0.5f;

        CarryLoad carryLoad;
        PlayerController player;

        // 체력 - 이벤트 구독으로 피격 플래시 트리거 (구독 대상이 바뀌면 재배선)
        PlayerHealth health;
        float flashAlpha;

        // 획득 토스트 풀 (좌측 스택). Text + 남은 시간 + 등장 진행도
        sealed class Toast
        {
            public Text Label;
            public float Remaining;
            public float Age;
        }

        readonly System.Collections.Generic.List<Toast> toasts = new System.Collections.Generic.List<Toast>();

        // 가방 슬롯 풀 (재사용). 인벤토리 종류 수만큼 활성화
        readonly System.Collections.Generic.List<GameObject> bagSlots = new System.Collections.Generic.List<GameObject>();

        // 획득 비행 연출 (웹 flyToBag 이식): 메인 아이콘 1개 + 꼬리 파티클 4개가
        // 아이템 위치에서 가방으로 포물선 비행. 파티clip은 순차 딜레이로 흩날린다.
        sealed class Flyer
        {
            public Image Icon;
            public Vector2 Start;
            public Vector2 Control;
            public Vector2 End;
            public float Age;      // 음수면 아직 대기 (딜레이)
            public float Duration;
            public bool IsMain;    // 메인 아이콘(크고 도착 시 pop) vs 꼬리 파티클(작음)
        }

        readonly System.Collections.Generic.List<Flyer> flyers = new System.Collections.Generic.List<Flyer>();

        // 웹 수치: 메인 0.55s / 파티클 0.5~0.65s, 파티클 4개, 딜레이 0.03+i*0.045
        const float FlyerMainDuration = 0.55f;
        const int FlyerParticleCount = 4;
        const float FlyerMainStartSize = 40f;
        const float FlyerMainEndSize = 24f;
        const float FlyerDotSize = 10f;

        // 비행 시드용(제어점 랜덤). 결정적 필요 없어 프레임 카운터로 변주
        int flyerSeed;

        // 진행 마일스톤 통과 여부 (런당 리셋)
        readonly bool[] milestonesPassed = new bool[4];
        float announceRemaining;

        // 직전 프레임의 running 여부 - false->true 전이에서 마일스톤 리셋 (새 런)
        bool wasRunning;

        // Collected 이벤트 구독 여부 (중복 구독 방지)
        bool subscribedCollected;

        // 바 부드러운 보간 (실시간 모션): 표시값이 목표값을 lerp로 따라간다
        float healthShown;
        float weightShown;

        // 가방 획득 pop 애니메이션 (웹 bagpop 이식): 남은 시간
        float bagPopTimer;

        // 바 보간 속도(초당 비율 수렴 계수)와 pop 지속/세기
        const float BarLerpSpeed = 8f;
        const float BagPopSeconds = 0.32f;
        const float BagPopScale = 0.06f;

        void Update()
        {
            RunManager run = RunManager.Instance;

            if (run == null)
            {
                SetAllInactive();
                return;
            }

            bool running = run.StateMachine.Current == RunState.Running;

            UpdateHealth(running);
            UpdateDamageFlash();
            UpdateTimer(run, running);
            UpdateWeight(running);
            UpdateBag(run, running);
            UpdateObjective(run, running);
            UpdateToasts();
            UpdateFlyers();
            UpdateAnnounce(run, running);
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
            SetActive(healthRoot, false);
            SetActive(timerRoot, false);
            SetActive(valueRoot, false);
            SetActive(weightRoot, false);
            SetActive(bagRoot, false);
            SetActive(objectiveRoot, false);
            SetActive(announceRoot, false);
            SetActive(interactRoot, false);
            SetActive(gaugeRoot, false);
            SetActive(choiceRoot, false);
            SetActive(signalRoot, false);
            SetActive(collapseRoot, false);
            SetActive(resultRoot, false);
        }

        // 체력 바 (HP 시스템): 현재/최대 비율로 Fill 폭 조정 + 수치 표시.
        // PlayerHealth 참조가 바뀌면 Damaged 이벤트를 재구독한다 (피격 플래시).
        void UpdateHealth(bool running)
        {
            ResolveHealth();

            if (health == null || !running)
            {
                SetActive(healthRoot, false);
                return;
            }

            SetActive(healthRoot, true);

            if (healthText != null)
                healthText.text = $"HP {Mathf.CeilToInt(health.Current)}/{Mathf.CeilToInt(health.maxHealth)}";

            // 표시값이 목표를 부드럽게 따라간다 (실시간 모션 - 피격 시 바가 스르륵 줌)
            healthShown = Mathf.Lerp(healthShown, health.Normalized, BarLerpSpeed * Time.deltaTime);
            FillBar(healthFill, healthShown);
        }

        // 피격 붉은 화면 플래시: Damaged 이벤트가 알파를 채우고 매 프레임 감쇠
        void UpdateDamageFlash()
        {
            if (damageFlash == null)
                return;

            if (flashAlpha > 0f)
                flashAlpha = Mathf.Max(0f, flashAlpha - FlashFadePerSecond * Time.deltaTime);

            Color color = damageFlash.color;
            color.a = flashAlpha;
            damageFlash.color = color;

            SetActive(damageFlash.gameObject, flashAlpha > 0.001f);
        }

        void ResolveHealth()
        {
            if (!TryResolvePlayer())
                return;

            PlayerHealth current = player.Health;

            if (current == health)
                return;

            // 참조 교체 (런 재시작으로 플레이어가 바뀐 경우 등) - 재구독
            if (health != null)
                health.Damaged -= OnPlayerDamaged;

            health = current;

            if (health != null)
                health.Damaged += OnPlayerDamaged;
        }

        void OnPlayerDamaged(float amount)
        {
            flashAlpha = FlashPeakAlpha;
        }

        void OnEnable()
        {
            if (!subscribedCollected)
            {
                LootSpot.Collected += OnLootCollected;
                subscribedCollected = true;
            }
        }

        void OnDisable()
        {
            if (health != null)
                health.Damaged -= OnPlayerDamaged;

            if (subscribedCollected)
            {
                LootSpot.Collected -= OnLootCollected;
                subscribedCollected = false;
            }
        }

        // 아이템 자동 수집 시 획득 토스트 (웹 이식)
        void OnLootCollected(LootDefinition definition, Vector3 worldPosition)
        {
            if (definition == null)
                return;

            SpawnToast($"{definition.displayName} +{definition.weight:F1}kg");

            // 아이템 위치 -> 가방으로 아이콘이 포물선 비행 (웹 flyToBag 이식).
            // 도착 시 가방 pop이 트리거된다 (SpawnFlyer가 도착 콜백에서 처리)
            SpawnFlyer(definition, worldPosition);
        }

        // 획득 아이콘 비행 생성 (웹 flyToBag): 메인 아이콘 1 + 꼬리 파티클 4개를
        // 아이템 월드좌표에서 가방까지 각자 다른 포물선/딜레이로 날린다
        void SpawnFlyer(LootDefinition definition, Vector3 worldPosition)
        {
            if (bagRoot == null)
                return;

            Canvas canvas = GetComponentInParent<Canvas>();

            if (canvas == null)
                return;

            Camera cam = Camera.main;

            if (cam == null)
                return;

            // 시작점: 아이템 월드좌표 -> 스크린 -> 캔버스 로컬
            Vector3 screen = cam.WorldToScreenPoint(worldPosition + Vector3.up * 0.5f);

            // 카메라 뒤면 비행 생략 (pop만)
            if (screen.z <= 0f)
            {
                bagPopTimer = BagPopSeconds;
                return;
            }

            RectTransform canvasRect = canvas.transform as RectTransform;

            Vector2 startLocal;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screen, canvas.worldCamera, out startLocal);

            // 도착점: 가방 패널 상단 중앙 (웹 bagPanel top+22 근사)
            RectTransform bagRect = bagRoot.transform as RectTransform;
            Vector3 bagScreen = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, bagRect.position);

            Vector2 endLocal;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, bagScreen, canvas.worldCamera, out endLocal);

            Color color = LootDefinition.GradeColor(definition.tier);
            Sprite sprite = LootIcons.Get(definition.id);

            // 메인 아이콘 (크고, 종류 스프라이트, 도착 시 pop)
            SpawnOneFlyer(canvasRect, startLocal, endLocal, color, sprite, FlyerMainStartSize,
                isMain: true, delay: 0f, duration: FlyerMainDuration);

            // 꼬리 파티클 4개 (작은 색 점, 순차 딜레이 + 랜덤 포물선)
            for (int i = 0; i < FlyerParticleCount; i++)
            {
                float delay = 0.03f + i * 0.045f;
                float duration = 0.5f + NextFlyerRand() * 0.15f;

                SpawnOneFlyer(canvasRect, startLocal, endLocal, color, null, FlyerDotSize,
                    isMain: false, delay: delay, duration: duration);
            }
        }

        void SpawnOneFlyer(
            RectTransform canvasRect, Vector2 start, Vector2 end, Color color, Sprite sprite,
            float size, bool isMain, float delay, float duration)
        {
            // 제어점: 두 점 중간 + 좌우 랜덤 흔들림, 위로 솟는 포물선 정점 (웹 cx/cy)
            float jitterX = (NextFlyerRand() - 0.5f) * 70f;
            float rise = 80f + NextFlyerRand() * 60f;
            Vector2 control = (start + end) * 0.5f + new Vector2(jitterX, rise);

            GameObject iconObject = new GameObject(isMain ? "Flyer" : "FlyerDot", typeof(RectTransform));
            RectTransform rect = (RectTransform)iconObject.transform;
            rect.SetParent(canvasRect, false);
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = start;

            Image icon = iconObject.AddComponent<Image>();
            icon.color = color;
            icon.raycastTarget = false;

            // 메인은 종류 아이콘 스프라이트, 파티클은 색 점(스프라이트 없음)
            if (sprite != null)
            {
                icon.sprite = sprite;
                icon.preserveAspect = true;
            }

            flyers.Add(new Flyer
            {
                Icon = icon,
                Start = start,
                Control = control,
                End = end,
                Age = -delay,
                Duration = duration,
                IsMain = isMain,
            });
        }

        // 결정적일 필요 없는 연출용 난수 (프레임/카운터 변주). RunManager.Rng 오염 금지
        float NextFlyerRand()
        {
            flyerSeed = flyerSeed * 1103515245 + 12345;
            uint bits = (uint)flyerSeed >> 8;

            return (bits & 0xFFFF) / 65535f;
        }

        // 비행 진행: 딜레이 대기 -> 베지어 이동 + 축소 + 페이드 -> 도착 시 제거.
        // 메인 아이콘 도착 시에만 가방 pop (웹: 꽂히는 순간 튐)
        void UpdateFlyers()
        {
            for (int i = flyers.Count - 1; i >= 0; i--)
            {
                Flyer flyer = flyers[i];
                flyer.Age += Time.deltaTime;

                // 아직 딜레이 중 (음수 Age) - 대기하되 시작점에 숨겨둔다
                if (flyer.Age < 0f)
                {
                    if (flyer.Icon != null)
                    {
                        Color hidden = flyer.Icon.color;
                        hidden.a = 0f;
                        flyer.Icon.color = hidden;
                    }

                    continue;
                }

                float t = Mathf.Clamp01(flyer.Age / flyer.Duration);

                if (flyer.Icon != null)
                {
                    Vector2 pos = QuadraticBezier(flyer.Start, flyer.Control, flyer.End, t);
                    RectTransform rect = flyer.Icon.rectTransform;
                    rect.anchoredPosition = pos;

                    // 메인만 축소 (웹 scale 1-0.35u), 파티클은 크기 유지
                    if (flyer.IsMain)
                    {
                        float size = Mathf.Lerp(FlyerMainStartSize, FlyerMainEndSize, t);
                        rect.sizeDelta = new Vector2(size, size);
                    }

                    // 도착 직전(u>0.85) 페이드아웃, 그 전엔 불투명
                    Color color = flyer.Icon.color;
                    color.a = t > 0.85f ? Mathf.InverseLerp(1f, 0.85f, t) : 1f;
                    flyer.Icon.color = color;
                }

                if (t < 1f)
                    continue;

                if (flyer.Icon != null)
                    Destroy(flyer.Icon.gameObject);

                flyers.RemoveAt(i);

                // 메인 아이콘이 꽂히는 순간에만 가방 pop
                if (flyer.IsMain)
                    bagPopTimer = BagPopSeconds;
            }
        }

        static Vector2 QuadraticBezier(Vector2 p0, Vector2 p1, Vector2 p2, float t)
        {
            float inv = 1f - t;

            return inv * inv * p0 + 2f * inv * t * p1 + t * t * p2;
        }

        // 제한 시간 카운트다운 (웹 이식, ADR-0008): MM:SS, 30초 미만 빨강.
        // 시간 압박 인지 표현 - 실수치를 그대로 보여준다(웹처럼). 붕괴와 이중 압박.
        void UpdateTimer(RunManager run, bool running)
        {
            if (!running || run.Timer == null || run.Timer.LimitSeconds <= 0f)
            {
                SetActive(timerRoot, false);
                return;
            }

            SetActive(timerRoot, true);

            if (timerText == null)
                return;

            float remaining = run.Timer.Remaining;
            int minutes = Mathf.FloorToInt(remaining / 60f);
            int seconds = Mathf.FloorToInt(remaining % 60f);

            timerText.text = $"{minutes:00}:{seconds:00}";

            // 30초 미만 = 골드에서 빨강으로 (웹 warn)
            if (remaining < 30f)
                timerText.color = new Color(1f, 0.36f, 0.36f);
            else
                timerText.color = new Color(1f, 0.843f, 0.369f);
        }

        // 무게 바 (웹 이식): 적재 비율로 Fill 폭, 단계 색, "무게 / 최대 (단계)" 텍스트
        void UpdateWeight(bool running)
        {
            if (carryLoad == null)
                carryLoad = FindFirstObjectByType<CarryLoad>();

            if (carryLoad == null || !running)
            {
                SetActive(weightRoot, false);
                return;
            }

            SetActive(weightRoot, true);

            if (weightText != null)
            {
                string stage = CarryLoad.StageLabel(carryLoad.Stage);
                weightText.text = $"{carryLoad.TotalWeight:F1} / {carryLoad.maxCarryWeight:F0}kg ({stage})";
            }

            // 무게 바도 부드럽게 차오른다 (실시간 모션 - 획득 시 스르륵 참)
            weightShown = Mathf.Lerp(weightShown, carryLoad.LoadRatio, BarLerpSpeed * Time.deltaTime);
            FillBar(weightFill, weightShown);
        }

        // 가방 그리드 (웹 이식): (id, 등급) 스택마다 슬롯 하나. 등급 외곽선 색 구분.
        // 슬롯은 풀에서 재사용하고 남는 슬롯은 비활성화한다
        void UpdateBag(RunManager run, bool running)
        {
            if (bagRoot == null || bagGrid == null || bagSlotTemplate == null || !running)
            {
                SetActive(bagRoot, false);
                return;
            }

            SetActive(bagRoot, true);

            if (bagWeightText != null)
                bagWeightText.text = $"{run.Inventory.TotalWeight:F1}kg";

            var entries = run.Inventory.Entries;

            for (int i = 0; i < entries.Count; i++)
            {
                GameObject slot = ResolveSlot(i);
                FillSlot(slot, entries[i]);
            }

            // 남는 슬롯 비활성화
            for (int i = entries.Count; i < bagSlots.Count; i++)
                bagSlots[i].SetActive(false);

            ApplyBagPop();
        }

        // 가방 pop: 획득 순간 살짝 커졌다 원래대로 (웹 bagpop). 사인 커브로 부드럽게
        void ApplyBagPop()
        {
            RectTransform bagRect = bagRoot.transform as RectTransform;

            if (bagRect == null)
                return;

            if (bagPopTimer <= 0f)
            {
                bagRect.localScale = Vector3.one;
                return;
            }

            bagPopTimer -= Time.deltaTime;

            // 0->1 진행. sin(pi*t)로 한 번 부풀었다 돌아온다
            float progress = 1f - Mathf.Clamp01(bagPopTimer / BagPopSeconds);
            float scale = 1f + Mathf.Sin(progress * Mathf.PI) * BagPopScale;

            bagRect.localScale = new Vector3(scale, scale, 1f);
        }

        GameObject ResolveSlot(int index)
        {
            if (index < bagSlots.Count)
            {
                bagSlots[index].SetActive(true);
                return bagSlots[index];
            }

            GameObject slot = Instantiate(bagSlotTemplate, bagGrid);
            slot.SetActive(true);
            bagSlots.Add(slot);

            return slot;
        }

        // 슬롯 하나에 아이템 이름/개수/등급 외곽선 반영
        void FillSlot(GameObject slot, RunInventory.Entry entry)
        {
            // 외곽선 = 등급 색 (일반/희귀/레어)
            Image outline = slot.GetComponent<Image>();

            if (outline != null)
                outline.color = ResolveGradeColor(entry.Grade);

            // 아이콘 = 아이템 종류별 스프라이트 (웹 이식 - 이모지 대신 도형 아이콘)
            Transform iconTransform = slot.transform.Find("Icon");

            if (iconTransform != null)
            {
                Image iconImage = iconTransform.GetComponent<Image>();

                if (iconImage != null)
                {
                    Sprite sprite = LootIcons.Get(entry.Definition.id);

                    iconImage.sprite = sprite;
                    iconImage.enabled = sprite != null;

                    // 아이콘 색은 아이템의 시작 등급, 외곽선은 합성으로 오른 현재 등급
                    iconImage.color = LootDefinition.GradeColor(entry.Definition.tier);
                }
            }

            // 텍스트 = 개수만 (웹처럼 아이콘 중심, 이름 라벨 제거)
            Text label = slot.GetComponentInChildren<Text>();

            if (label == null)
                return;

            label.text = entry.Count > 1 ? $"x{entry.Count}" : "";
            label.alignment = TextAnchor.LowerRight;
        }

        // 등급 색은 아이템 데이터가 정본 (일반 흰 / 희귀 파랑 / 영웅 보라 / 전설 빨강)
        static Color ResolveGradeColor(int grade)
        {
            Color color = LootDefinition.GradeColor(grade);

            if (grade <= 1)
                color.a = NormalGradeOutlineAlpha;

            return color;
        }

        // 일반 등급은 접두어를 붙이지 않는다 (기본 상태라 이름이 길어지기만 한다)
        static string ResolveGradePrefix(int grade)
        {
            if (grade <= 1)
                return "";

            return $"{LootDefinition.GradeName(grade)} ";
        }

        // 탐색 목표 (웹 이식): 수집 종류 수 / 총 가치
        void UpdateObjective(RunManager run, bool running)
        {
            if (objectiveRoot == null || !running)
            {
                SetActive(objectiveRoot, false);
                return;
            }

            SetActive(objectiveRoot, true);

            if (objectiveText != null)
                objectiveText.text = $"전리품 수집\n종류 {run.Inventory.Entries.Count} · 가치 {run.Inventory.TotalValue}";
        }

        // 진행 강조 메시지 (웹 이식): 25/50/75/90% 최초 통과 시 중앙 표시.
        // 진행률은 붕괴 전선 대비 플레이어 위치로 근사 (숨김 실거리 노출 금지 규약 -
        // 구간 길이 기준 비율만 사용, 실수치는 표시하지 않음)
        void UpdateAnnounce(RunManager run, bool running)
        {
            // 새 런 시작(정지->진행 전이) 시 마일스톤 리셋 - 다음 런에서 다시 표시
            if (running && !wasRunning)
                ResetMilestones();

            wasRunning = running;

            if (running)
                CheckMilestones();

            if (announceRemaining > 0f)
                announceRemaining -= Time.deltaTime;

            bool visible = announceRemaining > 0f;
            SetActive(announceRoot, visible);

            // 등장 팝 + 만료 페이드 (실시간 모션, 웹 announce 이식)
            if (visible)
                AnimateAnnounce();
        }

        void AnimateAnnounce()
        {
            RectTransform rect = announceRoot.transform as RectTransform;

            if (rect == null)
                return;

            // 표시 초반 0.3s 동안 0.96 -> 1.05로 팝
            float age = AnnounceSeconds - announceRemaining;
            float appear = Mathf.Clamp01(age / 0.3f);
            float scale = Mathf.Lerp(0.96f, 1.05f, appear);

            rect.localScale = new Vector3(scale, scale, 1f);

            if (announceText == null)
                return;

            // 만료 직전 0.4s 페이드아웃
            float alpha = 1f;

            if (announceRemaining < 0.4f)
                alpha = announceRemaining / 0.4f;

            Color color = announceText.color;
            color.a = alpha;
            announceText.color = color;
        }

        void ResetMilestones()
        {
            for (int i = 0; i < milestonesPassed.Length; i++)
                milestonesPassed[i] = false;

            announceRemaining = 0f;
        }

        void CheckMilestones()
        {
            Field.FieldSpawner field = Field.FieldSpawner.Instance;

            if (field == null || player == null)
                return;

            float progress = field.StageProgress01;

            for (int i = 0; i < Milestones.Length; i++)
            {
                if (milestonesPassed[i])
                    continue;

                if (progress < Milestones[i])
                    continue;

                milestonesPassed[i] = true;
                ShowAnnounce(i < Milestones.Length - 1 ? "탈출 지역에 가까워지고 있습니다" : "탈출 지점이 근처에 있습니다!");
            }
        }

        void ShowAnnounce(string message)
        {
            announceRemaining = AnnounceSeconds;

            if (announceText != null)
                announceText.text = message;
        }

        // 획득 토스트: 새 토스트를 스택 상단에 추가하고 오래된 것은 밀어낸다
        void SpawnToast(string message)
        {
            if (toastRoot == null || toastTemplate == null)
                return;

            Text label = Instantiate(toastTemplate, toastRoot);
            label.gameObject.SetActive(true);
            label.text = message;

            toasts.Insert(0, new Toast { Label = label, Remaining = ToastSeconds });

            // 최대 개수 초과분 즉시 제거
            while (toasts.Count > MaxToasts)
            {
                Toast oldest = toasts[toasts.Count - 1];
                toasts.RemoveAt(toasts.Count - 1);

                if (oldest.Label != null)
                    Destroy(oldest.Label.gameObject);
            }
        }

        // 토스트 수명 감소 + 등장 슬라이드인/만료 페이드아웃 (실시간 모션) + 제거
        void UpdateToasts()
        {
            for (int i = toasts.Count - 1; i >= 0; i--)
            {
                Toast toast = toasts[i];
                toast.Remaining -= Time.deltaTime;
                toast.Age += Time.deltaTime;

                if (toast.Remaining <= 0f)
                {
                    if (toast.Label != null)
                        Destroy(toast.Label.gameObject);

                    toasts.RemoveAt(i);
                    continue;
                }

                AnimateToast(toast);
            }
        }

        // 등장: 왼쪽에서 슬라이드인(0.18s) + 페이드인. 만료 직전(0.4s) 페이드아웃
        void AnimateToast(Toast toast)
        {
            if (toast.Label == null)
                return;

            RectTransform rect = toast.Label.rectTransform;

            float appear = Mathf.Clamp01(toast.Age / 0.18f);
            float slideX = Mathf.Lerp(-16f, 0f, appear);

            Vector2 pos = rect.anchoredPosition;
            pos.x = slideX;
            rect.anchoredPosition = pos;

            float alpha = appear;

            if (toast.Remaining < 0.4f)
                alpha = Mathf.Min(alpha, toast.Remaining / 0.4f);

            Color color = toast.Label.color;
            color.a = alpha;
            toast.Label.color = color;
        }

        // 바 Fill 폭 조정 공용 (체력/무게). 부모 폭 기준 비율
        void FillBar(RectTransform fill, float ratio01)
        {
            if (fill == null)
                return;

            RectTransform track = fill.parent as RectTransform;

            if (track == null)
                return;

            float maxWidth = track.rect.width - GaugeFillPadding;
            fill.sizeDelta = new Vector2(maxWidth * Mathf.Clamp01(ratio01), fill.sizeDelta.y);
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

        // 자동 수집(ADR-0008 웹 이식)으로 아이템 상호작용 프롬프트는 사라졌다.
        // 상호작용 키 프롬프트 자체는 향후 보급상자 등 홀드 상호작용용으로 유지 -
        // 현재는 대상이 없으므로 null (선택지 프롬프트는 choiceRoot가 별도 처리)
        static string BuildInteractPrompt()
        {
            return null;
        }

        // 자동 수집이라 루팅 게이지가 없다 - 항상 숨김 (게이지 오브젝트는 프리팹에 잔존)
        void UpdateGauge()
        {
            SetActive(gaugeRoot, false);
        }

        // 끝 지점 웨이포인트(ADR-0008)는 밟으면 자동 발동 - 선택 UI가 필요 없다.
        // 선택지 프롬프트는 항상 숨김 (choiceRoot 오브젝트는 프리팹에 잔존)
        void UpdateChoice()
        {
            SetActive(choiceRoot, false);
        }

        // 거리 신호(SignalEmitter)는 폐기됨 - 배너는 마일스톤 안내만 사용한다
        void UpdateSignal()
        {
            SetActive(signalRoot, false);
        }

        // 시간 압박 인지 표현: 바닥 제거 기준선 근접 경고 (필드 규칙 3.2)
        void UpdateCollapse(bool running)
        {
            Field.FieldSpawner field = Field.FieldSpawner.Instance;

            bool visible = running
                && field != null
                && field.RemoveLineDistanceToPlayer <= CollapseWarningDistance;

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

        // 프롬프트 박스 = 모바일 홀드 버튼 (M5-1). EventSystem 없이 포인터 직접 판독.
        // 전체 터치를 순회 - 왼손이 조이스틱을 잡고 있어도 오른손 홀드가 동작한다
        // (primaryTouch 한정 금지, Codex 교차 검토). 누르는 동안 매 프레임 공급
        void UpdateInteractHold()
        {
            if (!TryResolvePlayer())
                return;

            bool held = interactRoot != null
                && interactRoot.activeSelf
                && AnyPointerHeldInside((RectTransform)interactRoot.transform);

            player.SetExternalInteractHeld(held);
        }

        bool TryResolvePlayer()
        {
            if (player != null)
                return true;

            player = FindFirstObjectByType<PlayerController>();
            return player != null;
        }

        // 누르고 있는 포인터(전체 터치 + 마우스) 중 rect 안에 있는 것이 있는가
        static bool AnyPointerHeldInside(RectTransform rect)
        {
            Touchscreen touch = Touchscreen.current;

            if (touch != null)
            {
                foreach (UnityEngine.InputSystem.Controls.TouchControl control in touch.touches)
                {
                    if (!control.press.isPressed)
                        continue;

                    if (RectTransformUtility.RectangleContainsScreenPoint(
                            rect, control.position.ReadValue(), null))
                        return true;
                }
            }

            Mouse mouse = Mouse.current;

            if (mouse != null && mouse.leftButton.isPressed)
                return RectTransformUtility.RectangleContainsScreenPoint(
                    rect, mouse.position.ReadValue(), null);

            return false;
        }

        // 이번 프레임 시작된 포인터 프레스가 rect 안에 있는가 (탭 버튼용)
        static bool AnyPointerPressedInside(RectTransform rect)
        {
            Touchscreen touch = Touchscreen.current;

            if (touch != null)
            {
                foreach (UnityEngine.InputSystem.Controls.TouchControl control in touch.touches)
                {
                    if (!control.press.wasPressedThisFrame)
                        continue;

                    if (RectTransformUtility.RectangleContainsScreenPoint(
                            rect, control.position.ReadValue(), null))
                        return true;
                }
            }

            Mouse mouse = Mouse.current;

            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                return RectTransformUtility.RectangleContainsScreenPoint(
                    rect, mouse.position.ReadValue(), null);

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
