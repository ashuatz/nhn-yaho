using Scavenger.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Scavenger.EditorTools
{
    /// <summary>
    /// HudCanvas 프리팹 템플릿 (M5-1 uGUI 전환). 최초 1회 프리팹 생성에만 쓰인다 -
    /// 이후 배치/수치 튜닝은 사용자가 프리팹에서 직접 (GreyboxSceneSetup 규약).
    /// EventSystem 불사용 - 조작 컴포넌트가 포인터를 직접 판독한다.
    /// 폰트는 내장 LegacyRuntime (동적 폰트 - 한글은 OS 폰트 폴백).
    /// </summary>
    public static class HudCanvasTemplate
    {
        static readonly Color PanelColor = new Color(0f, 0f, 0f, 0.45f);
        static readonly Color TextColor = new Color(0.92f, 0.92f, 0.92f);

        public static GameObject Build()
        {
            GameObject root = new GameObject("HudCanvas");

            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // -- 아이템 라벨 레이어 (다른 패널보다 먼저 = 뒤에 깔림) ------------
            RectTransform labelLayer = CreateStretchRect(root.transform, "LabelLayer");
            Text labelTemplate = CreateText(labelLayer, "LabelTemplate", 20, TextAnchor.LowerCenter);
            SetRect(labelTemplate.rectTransform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f),
                Vector2.zero, new Vector2(380f, 64f));
            labelTemplate.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
            labelTemplate.gameObject.SetActive(false);

            // -- 조이스틱 (좌하단) ------------------------------------------
            RectTransform joystickBase = CreateImageRect(
                root.transform, "JoystickBase",
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0.5f, 0.5f),
                new Vector2(116f, 116f), new Vector2(160f, 160f),
                new Color(1f, 1f, 1f, 0.16f));

            RectTransform joystickKnob = CreateImageRect(
                joystickBase, "JoystickKnob",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(60f, 60f),
                new Color(1f, 1f, 1f, 0.55f));

            // -- 가방 패널 (조이스틱 오른쪽): 가치/무게/아이템 목록 ----------------
            RectTransform valueBox = CreatePanel(
                root.transform, "BagPanel",
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(240f, 16f), new Vector2(330f, 170f));
            Text valueText = CreateText(valueBox, "Text", 20, TextAnchor.UpperLeft, padding: 12f);

            // -- 체력/스테미나 바 (좌상단) -------------------------------------
            RectTransform healthBar = CreatePanel(
                root.transform, "HealthBar",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(16f, -16f), new Vector2(260f, 28f));

            RectTransform healthFill = CreateImageRect(
                healthBar, "Fill",
                new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                new Vector2(2f, 0f), new Vector2(0f, -4f),
                new Color(0.85f, 0.3f, 0.25f, 0.9f));

            Text healthLabel = CreateText(healthBar, "Label", 18, TextAnchor.MiddleCenter);
            healthLabel.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);

            RectTransform staminaBar = CreatePanel(
                root.transform, "StaminaBar",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(16f, -50f), new Vector2(260f, 28f));

            RectTransform staminaFill = CreateImageRect(
                staminaBar, "Fill",
                new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                new Vector2(2f, 0f), new Vector2(0f, -4f),
                new Color(0.4f, 0.75f, 0.35f, 0.9f));

            Text staminaLabel = CreateText(staminaBar, "Label", 18, TextAnchor.MiddleCenter);
            staminaLabel.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);

            // -- 상호작용 프롬프트 (우하단, 홀드 버튼 겸용) ---------------------
            RectTransform interactBox = CreatePanel(
                root.transform, "InteractPrompt",
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-16f, 16f), new Vector2(310f, 64f));
            Text interactText = CreateText(interactBox, "Text", 22, TextAnchor.MiddleCenter);

            // -- 루팅 게이지 (화면 중앙 아래) ---------------------------------
            RectTransform gauge = CreatePanel(
                root.transform, "LootGauge",
                new Vector2(0.5f, 0.38f), new Vector2(0.5f, 0.38f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(260f, 24f));

            RectTransform gaugeFill = CreateImageRect(
                gauge, "Fill",
                new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                new Vector2(2f, 0f), new Vector2(0f, -4f),
                new Color(0.85f, 0.85f, 0.8f, 0.9f));

            Text gaugeLabel = CreateText(gauge, "Label", 20, TextAnchor.LowerCenter);
            SetRect(gaugeLabel.rectTransform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0f),
                new Vector2(0f, 4f), new Vector2(340f, 24f));
            gaugeLabel.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);

            // -- 선택지 프롬프트 (전진/탈출 = 터치 버튼, 키보드 W/E 병행) ----------
            RectTransform choiceBox = CreatePanel(
                root.transform, "ChoicePrompt",
                new Vector2(0.5f, 0.7f), new Vector2(0.5f, 0.7f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(460f, 110f));

            Text choiceText = CreateText(choiceBox, "Title", 22, TextAnchor.UpperCenter);
            choiceText.rectTransform.offsetMin = new Vector2(4f, 62f);
            choiceText.rectTransform.offsetMax = new Vector2(-4f, -6f);

            RectTransform choiceAdvance = CreateImageRect(
                choiceBox, "AdvanceButton",
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(10f, 10f), new Vector2(215f, 50f),
                new Color(1f, 1f, 1f, 0.12f));
            Text advanceText = CreateText(choiceAdvance, "Text", 20, TextAnchor.MiddleCenter);
            advanceText.text = "W: 더 깊이 전진\n(고가치/고위험)";

            RectTransform choiceExtract = CreateImageRect(
                choiceBox, "ExtractButton",
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-10f, 10f), new Vector2(215f, 50f),
                new Color(1f, 1f, 1f, 0.12f));
            Text extractText = CreateText(choiceExtract, "Text", 20, TextAnchor.MiddleCenter);
            extractText.text = "E: 탈출\n(확정)";

            // -- 신호 배너 (상단) --------------------------------------------
            RectTransform signalBox = CreatePanel(
                root.transform, "SignalBanner",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -28f), new Vector2(430f, 36f));
            Text signalText = CreateText(signalBox, "Text", 22, TextAnchor.MiddleCenter);

            // -- 붕괴 근접 경고 ----------------------------------------------
            RectTransform collapseBox = CreatePanel(
                root.transform, "CollapseWarning",
                new Vector2(0.5f, 0.82f), new Vector2(0.5f, 0.82f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(400f, 36f));
            Text collapseText = CreateText(collapseBox, "Text", 22, TextAnchor.MiddleCenter);
            collapseText.color = new Color(1f, 0.55f, 0.45f);
            collapseText.text = "뒤에서 바닥이 무너지고 있다!";

            // -- 런 결과 -----------------------------------------------------
            RectTransform resultBox = CreatePanel(
                root.transform, "ResultPanel",
                new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(430f, 84f));
            Text resultText = CreateText(resultBox, "Text", 24, TextAnchor.MiddleCenter);

            // -- 컴포넌트 배선 ------------------------------------------------
            HudController hud = root.AddComponent<HudController>();
            hud.valueRoot = valueBox.gameObject;
            hud.valueText = valueText;
            hud.healthRoot = healthBar.gameObject;
            hud.healthFill = healthFill;
            hud.healthLabel = healthLabel;
            hud.staminaRoot = staminaBar.gameObject;
            hud.staminaFill = staminaFill;
            hud.staminaLabel = staminaLabel;
            hud.interactRoot = interactBox.gameObject;
            hud.interactText = interactText;
            hud.gaugeRoot = gauge.gameObject;
            hud.gaugeFill = gaugeFill;
            hud.gaugeLabel = gaugeLabel;
            hud.choiceRoot = choiceBox.gameObject;
            hud.choiceText = choiceText;
            hud.choiceAdvanceButton = choiceAdvance;
            hud.choiceExtractButton = choiceExtract;
            hud.signalRoot = signalBox.gameObject;
            hud.signalText = signalText;
            hud.collapseRoot = collapseBox.gameObject;
            hud.resultRoot = resultBox.gameObject;
            hud.resultText = resultText;

            LootLabelLayer labels = root.AddComponent<LootLabelLayer>();
            labels.labelRoot = labelLayer;
            labels.labelTemplate = labelTemplate;

            VirtualJoystick joystick = root.AddComponent<VirtualJoystick>();
            joystick.baseRect = joystickBase;
            joystick.knobRect = joystickKnob;

            return root;
        }

        // -- 빌더 유틸 --------------------------------------------------------

        static RectTransform CreateStretchRect(Transform parent, string rectName)
        {
            GameObject rectObject = new GameObject(rectName, typeof(RectTransform));
            RectTransform rect = (RectTransform)rectObject.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        static RectTransform CreatePanel(
            Transform parent, string panelName,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 anchoredPosition, Vector2 size)
        {
            return CreateImageRect(
                parent, panelName, anchorMin, anchorMax, pivot, anchoredPosition, size, PanelColor);
        }

        static RectTransform CreateImageRect(
            Transform parent, string rectName,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 anchoredPosition, Vector2 size, Color color)
        {
            GameObject imageObject = new GameObject(rectName, typeof(RectTransform));
            RectTransform rect = (RectTransform)imageObject.transform;
            rect.SetParent(parent, false);
            SetRect(rect, anchorMin, anchorMax, pivot, anchoredPosition, size);

            Image image = imageObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;

            return rect;
        }

        static Text CreateText(
            Transform parent, string textName, int fontSize, TextAnchor alignment, float padding = 4f)
        {
            GameObject textObject = new GameObject(textName, typeof(RectTransform));
            RectTransform rect = (RectTransform)textObject.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padding, padding);
            rect.offsetMax = new Vector2(-padding, -padding);

            Text text = textObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = TextColor;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            return text;
        }

        static void SetRect(
            RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 anchoredPosition, Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }
    }
}
