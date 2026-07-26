using System.Collections.Generic;
using Scavenger.Field;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Scavenger.EditorTools
{
    /// <summary>
    /// 그레이박스 밝기 조절 창 (사용자 지시 2026-07-26).
    ///
    /// 코드의 색 상수는 기준색(색상 + 채도)이고, 명도만 여기서 배수로 올린다.
    /// 값은 Assets/Settings/GreyboxTheme.asset에 저장되므로 프리팹을 다시 구워도 남는다.
    ///
    /// "적용"을 눌러야 이미 구워진 머티리얼/프리팹에 반영된다 -
    /// 슬라이더만 움직이면 미리보기와 런타임 생성물만 바뀐다.
    /// </summary>
    public sealed class GreyboxBrightnessWindow : EditorWindow
    {
        // -- 색 (Unity-Editor-Layout 규약) ------------------------------------

        static readonly Color windowBackground = new Color(0.22f, 0.22f, 0.22f, 1f);
        static readonly Color panelBackground = new Color(0.235f, 0.235f, 0.235f, 1f);
        static readonly Color panelBorderColor = new Color(0.17f, 0.17f, 0.17f, 1f);
        static readonly Color headerBackground = new Color(0.235f, 0.235f, 0.235f, 1f);
        static readonly Color accentColor = new Color(0.36f, 0.36f, 0.36f, 1f);
        static readonly Color subtleTextColor = new Color(0.78f, 0.78f, 0.78f, 1f);
        static readonly Color noticeAccent = new Color(1f, 0.6f, 0.2f, 1f);

        const float LabelWidth = 120f;
        const float SwatchSize = 18f;

        GreyboxTheme theme;
        SerializedObject serializedTheme;

        // 미리보기 행은 한 번만 만들고 값만 갈아끼운다 (슬라이더마다 트리를 다시 짓지 않는다)
        readonly List<PreviewRow> previewRows = new List<PreviewRow>();

        sealed class PreviewRow
        {
            public GreyboxThemeApplier.Sample Sample;
            public VisualElement BaseSwatch;
            public VisualElement TintedSwatch;
            public Label ValueLabel;
        }

        [MenuItem("Scavenger/Greybox Brightness")]
        public static void Open()
        {
            GreyboxBrightnessWindow window = GetWindow<GreyboxBrightnessWindow>();
            window.titleContent = new GUIContent("Greybox Brightness");
            window.minSize = new Vector2(420f, 520f);
        }

        void OnEnable()
        {
            theme = GreyboxThemeAccess.Load();
        }

        void CreateGUI()
        {
            if (theme == null)
                theme = GreyboxThemeAccess.Load();

            serializedTheme = new SerializedObject(theme);

            VisualElement root = rootVisualElement;
            root.style.backgroundColor = windowBackground;

            ScrollView scroll = new ScrollView();
            scroll.style.flexGrow = 1f;
            scroll.contentContainer.style.paddingLeft = 10f;
            scroll.contentContainer.style.paddingRight = 10f;
            scroll.contentContainer.style.paddingTop = 10f;
            scroll.contentContainer.style.paddingBottom = 10f;

            scroll.Add(BuildSettingsSection());
            scroll.Add(BuildPreviewSection());
            scroll.Add(BuildApplySection());

            root.Add(scroll);

            RefreshPreview();
        }

        // -- 섹션: 배수 --------------------------------------------------------

        VisualElement BuildSettingsSection()
        {
            VisualElement body;
            VisualElement section = CreateSectionShell("명도 배수", "V SCALE", out body);

            body.Add(CreateNotice(
                "색상/채도는 그대로 두고 명도(V)만 곱한다. 배수 결과가 1을 넘으면 "
                + "채도를 지키려 V=1까지만 올린다."));

            body.Add(CreateSlider("전체", "brightness", 0.25f, 3f));
            body.Add(CreateSlider("필드", "fieldBrightness", 0.25f, 2f));
            body.Add(CreateSlider("배경", "backgroundBrightness", 0.25f, 2f));
            body.Add(CreateSlider("캐릭터", "characterBrightness", 0.25f, 2f));

            Button reset = new Button(ResetToDefault);
            reset.text = $"기본값 (전체 x{GreyboxTheme.DefaultBrightness:0.##})";
            reset.style.marginTop = 6f;
            reset.style.height = 24f;
            body.Add(reset);

            return section;
        }

        VisualElement CreateSlider(string label, string propertyName, float min, float max)
        {
            SerializedProperty property = serializedTheme.FindProperty(propertyName);

            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 4f;

            Label caption = new Label(label);
            caption.style.width = LabelWidth;
            caption.style.flexShrink = 0f;
            caption.style.color = subtleTextColor;
            caption.style.fontSize = 11;
            row.Add(caption);

            Slider slider = new Slider(min, max);
            slider.style.flexGrow = 1f;
            slider.showInputField = true;

            // 확장 메서드를 명시 호출한다 - UnityEditor.UIElements를 using으로 열면
            // ObjectField 등 같은 이름 타입이 양쪽에 있어 CS0104가 난다 (아트 창에서 겪음)
            UnityEditor.UIElements.BindingExtensions.BindProperty(slider, property);

            // 값이 바뀌면 미리보기만 갈아끼운다 (트리 재생성 금지)
            slider.RegisterValueChangedCallback(_ => RefreshPreview());

            row.Add(slider);

            return row;
        }

        void ResetToDefault()
        {
            Undo.RecordObject(theme, "Reset Greybox Brightness");

            theme.brightness = GreyboxTheme.DefaultBrightness;
            theme.fieldBrightness = 1f;
            theme.backgroundBrightness = 1f;
            theme.characterBrightness = 1f;

            EditorUtility.SetDirty(theme);
            serializedTheme.Update();

            RefreshPreview();
        }

        // -- 섹션: 미리보기 ----------------------------------------------------

        VisualElement BuildPreviewSection()
        {
            VisualElement body;
            VisualElement section = CreateSectionShell("미리보기", "BASE -> TINTED", out body);

            previewRows.Clear();

            foreach (GreyboxThemeApplier.Sample sample in GreyboxThemeApplier.CollectSamples())
                body.Add(CreatePreviewRow(sample));

            return section;
        }

        VisualElement CreatePreviewRow(GreyboxThemeApplier.Sample sample)
        {
            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 3f;

            Label caption = new Label(sample.Label);
            caption.style.width = 150f;
            caption.style.flexShrink = 0f;
            caption.style.color = subtleTextColor;
            caption.style.fontSize = 11;
            row.Add(caption);

            VisualElement baseSwatch = CreateSwatch();
            row.Add(baseSwatch);

            Label arrow = new Label("->");
            arrow.style.color = subtleTextColor;
            arrow.style.fontSize = 10;
            arrow.style.marginLeft = 4f;
            arrow.style.marginRight = 4f;
            row.Add(arrow);

            VisualElement tintedSwatch = CreateSwatch();
            row.Add(tintedSwatch);

            Label valueLabel = new Label();
            valueLabel.style.marginLeft = 8f;
            valueLabel.style.color = Color.white;
            valueLabel.style.fontSize = 11;
            row.Add(valueLabel);

            previewRows.Add(new PreviewRow
            {
                Sample = sample,
                BaseSwatch = baseSwatch,
                TintedSwatch = tintedSwatch,
                ValueLabel = valueLabel,
            });

            return row;
        }

        static VisualElement CreateSwatch()
        {
            VisualElement swatch = new VisualElement();
            swatch.style.width = SwatchSize;
            swatch.style.height = SwatchSize;
            swatch.style.flexShrink = 0f;
            swatch.style.borderLeftWidth = 1f;
            swatch.style.borderRightWidth = 1f;
            swatch.style.borderTopWidth = 1f;
            swatch.style.borderBottomWidth = 1f;
            swatch.style.borderLeftColor = panelBorderColor;
            swatch.style.borderRightColor = panelBorderColor;
            swatch.style.borderTopColor = panelBorderColor;
            swatch.style.borderBottomColor = panelBorderColor;

            return swatch;
        }

        void RefreshPreview()
        {
            if (theme == null)
                return;

            foreach (PreviewRow row in previewRows)
            {
                Color baseColor = row.Sample.BaseColor;
                Color tinted = theme.Apply(baseColor, row.Sample.Tone);

                row.BaseSwatch.style.backgroundColor = baseColor;
                row.TintedSwatch.style.backgroundColor = tinted;

                Color.RGBToHSV(baseColor, out float _, out float _, out float baseValue);
                Color.RGBToHSV(tinted, out float _, out float _, out float tintedValue);

                row.ValueLabel.text = $"V {baseValue:0.00} -> {tintedValue:0.00}";
            }
        }

        // -- 섹션: 적용 --------------------------------------------------------

        VisualElement BuildApplySection()
        {
            VisualElement body;
            VisualElement section = CreateSectionShell("적용", "REBUILD", out body);

            body.Add(CreateNotice(
                "런타임 생성물은 즉시 반영된다. 이미 구워진 머티리얼/프리팹(바닥 타일, "
                + "파밍 포인트, 아이템 오브젝트, 배경 블록)은 아래 버튼으로 다시 만든다.",
                noticeAccent));

            Button apply = new Button(ApplyToAssets);
            apply.text = "적용 (프리팹 / 배경 블록 다시 굽기)";
            apply.style.height = 28f;
            apply.style.marginTop = 4f;
            body.Add(apply);

            Button select = new Button(SelectAsset);
            select.text = "설정 에셋 선택";
            select.style.height = 22f;
            select.style.marginTop = 6f;
            body.Add(select);

            return section;
        }

        void ApplyToAssets()
        {
            serializedTheme.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();

            GreyboxThemeApplier.ApplyToAssets();

            RefreshPreview();
        }

        void SelectAsset()
        {
            Selection.activeObject = theme;
            EditorGUIUtility.PingObject(theme);
        }

        // -- 레이아웃 헬퍼 (Unity-Editor-Layout 규약) --------------------------

        VisualElement CreateSectionShell(string title, string badge, out VisualElement bodyContainer)
        {
            VisualElement shell = new VisualElement();

            shell.style.flexDirection = FlexDirection.Column;
            shell.style.backgroundColor = panelBackground;
            shell.style.overflow = Overflow.Hidden;
            shell.style.marginBottom = 10f;

            shell.style.borderLeftWidth = 1f;
            shell.style.borderRightWidth = 1f;
            shell.style.borderTopWidth = 1f;
            shell.style.borderBottomWidth = 1f;
            shell.style.borderLeftColor = panelBorderColor;
            shell.style.borderRightColor = panelBorderColor;
            shell.style.borderTopColor = panelBorderColor;
            shell.style.borderBottomColor = panelBorderColor;

            shell.Add(CreateSectionHeader(title, badge));
            shell.Add(CreateSectionAccentBar());

            bodyContainer = new VisualElement();
            bodyContainer.style.flexGrow = 1f;
            bodyContainer.style.flexDirection = FlexDirection.Column;
            bodyContainer.style.paddingLeft = 14f;
            bodyContainer.style.paddingRight = 14f;
            bodyContainer.style.paddingTop = 12f;
            bodyContainer.style.paddingBottom = 14f;
            bodyContainer.style.backgroundColor = panelBackground;

            shell.Add(bodyContainer);

            return shell;
        }

        VisualElement CreateSectionHeader(string title, string badgeText)
        {
            VisualElement header = new VisualElement();
            header.style.height = 40f;
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.backgroundColor = headerBackground;
            header.style.paddingLeft = 14f;
            header.style.paddingRight = 14f;

            VisualElement leftGroup = new VisualElement();
            leftGroup.style.flexDirection = FlexDirection.Row;
            leftGroup.style.alignItems = Align.Center;

            VisualElement accent = new VisualElement();
            accent.style.width = 2f;
            accent.style.height = 18f;
            accent.style.backgroundColor = accentColor;
            accent.style.marginRight = 8f;
            leftGroup.Add(accent);

            Label titleLabel = new Label(title);
            titleLabel.style.color = Color.white;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.fontSize = 12;
            leftGroup.Add(titleLabel);

            header.Add(leftGroup);

            if (!string.IsNullOrEmpty(badgeText))
            {
                Label badge = new Label(badgeText.ToUpperInvariant());
                badge.style.color = subtleTextColor;
                badge.style.unityFontStyleAndWeight = FontStyle.Bold;
                badge.style.fontSize = 9;
                badge.style.paddingLeft = 8f;
                badge.style.paddingRight = 8f;
                badge.style.paddingTop = 2f;
                badge.style.paddingBottom = 2f;
                header.Add(badge);
            }

            return header;
        }

        VisualElement CreateSectionAccentBar()
        {
            VisualElement bar = new VisualElement();
            bar.style.height = 1f;
            bar.style.backgroundColor = panelBorderColor;

            return bar;
        }

        VisualElement CreateNotice(string message)
        {
            return CreateNotice(message, accentColor);
        }

        VisualElement CreateNotice(string message, Color edgeColor)
        {
            VisualElement box = new VisualElement();
            box.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.3f);
            box.style.paddingLeft = 12f;
            box.style.paddingRight = 12f;
            box.style.paddingTop = 10f;
            box.style.paddingBottom = 10f;
            box.style.borderLeftWidth = 2f;
            box.style.borderLeftColor = edgeColor;
            box.style.marginBottom = 12f;

            Label label = new Label(message);
            label.style.color = subtleTextColor;
            label.style.fontSize = 11;
            label.style.whiteSpace = WhiteSpace.Normal;
            box.Add(label);

            return box;
        }
    }
}
