using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using ObjectField = UnityEditor.UIElements.ObjectField;
using Vector3Field = UnityEngine.UIElements.Vector3Field;

namespace Scavenger.ArtTools
{
    /// <summary>
    /// 트림시트 큐브 오소링 창. 아틀라스 셀을 면에 어떻게 깔지 정하고
    /// 씬에 실제 오브젝트 + 구운 메시 에셋으로 떨어뜨린다.
    ///
    /// 런타임 부트스트랩을 만들지 않는다 - 룩은 에디트 모드에서 확정하고
    /// 결과물만 씬에 남는다 (프로젝트 규약).
    /// </summary>
    sealed class TrimSheetCubeWindow : EditorWindow
    {
        static readonly Color WindowBackground = new Color(0.22f, 0.22f, 0.22f, 1f);
        static readonly Color PanelBackground = new Color(0.235f, 0.235f, 0.235f, 1f);
        static readonly Color PanelBorderColor = new Color(0.17f, 0.17f, 0.17f, 1f);
        static readonly Color HeaderBackground = new Color(0.235f, 0.235f, 0.235f, 1f);
        static readonly Color AccentColor = new Color(0.36f, 0.36f, 0.36f, 1f);
        static readonly Color SubtleTextColor = new Color(0.78f, 0.78f, 0.78f, 1f);
        static readonly Color InfoAccentColor = new Color(0.4f, 0.6f, 1f, 1f);
        static readonly Color WarnAccentColor = new Color(1f, 0.6f, 0.2f, 1f);

        const float LabelWidth = 120f;

        /// <summary>에셋 참조라 도메인 리로드를 견딘다.</summary>
        [SerializeField] TrimSheetDefinition definition;

        [SerializeField] Vector3 cubeSize = Vector3.one;
        [SerializeField] float worldUnitsPerCell = TrimSheetCubeBuilder.UnitCellSpan;
        [SerializeField] int crackSeed = TrimSheetCrackBaker.DefaultSeed;
        [SerializeField] TrimSheetCellMode cellMode = TrimSheetCellMode.MixPerTile;
        [SerializeField] int cellIndex;
        [SerializeField] bool randomRotation = true;
        [SerializeField] float shapeJitter = TrimSheetCubeBuilder.DefaultShapeJitter;
        [SerializeField] float shapePress = TrimSheetCubeBuilder.DefaultShapePress;
        [SerializeField] int seed = 12345;
        [SerializeField] string cubeName = "TrimSheetCube_01";

        [SerializeField] Vector3 variationOrigin = Vector3.zero;
        [SerializeField] int variationSeed = 20260725;

        ObjectField definitionField;
        IntegerField crackSeedField;
        Label atlasLabel;
        Label cellIndexHint;
        Label shapeHint;
        Label tileCountLabel;
        Label variationStatusLabel;
        Button clearVariationsButton;

        [MenuItem("Scavenger/Trim Sheet/Cube Authoring")]
        public static void Open()
        {
            TrimSheetCubeWindow window = GetWindow<TrimSheetCubeWindow>("Trim Sheet Cubes");
            window.minSize = new Vector2(420f, 600f);
        }

        void OnEnable()
        {
            if (definition == null)
                definition = AssetDatabase.LoadAssetAtPath<TrimSheetDefinition>(TrimSheetAssets.DefinitionPath);
        }

        void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.backgroundColor = WindowBackground;

            ScrollView scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1f;
            scroll.contentContainer.style.paddingLeft = 10f;
            scroll.contentContainer.style.paddingRight = 10f;
            scroll.contentContainer.style.paddingTop = 10f;
            scroll.contentContainer.style.paddingBottom = 10f;
            root.Add(scroll);

            scroll.Add(BuildAtlasSection());
            scroll.Add(BuildCubeSection());
            scroll.Add(BuildVariationSection());

            RefreshAtlasStatus();
            RefreshTileCount();
            RefreshVariationStatus();
        }

        // ---------------------------------------------------------------------
        // 섹션 구성
        // ---------------------------------------------------------------------

        VisualElement BuildAtlasSection()
        {
            VisualElement section = CreateSectionShell("아틀라스", "ATLAS", out VisualElement body);

            definitionField = new ObjectField("트림시트 규격")
            {
                objectType = typeof(TrimSheetDefinition),
                allowSceneObjects = false,
                value = definition,
            };
            definitionField.labelElement.style.minWidth = LabelWidth;
            definitionField.RegisterValueChangedCallback(OnDefinitionChanged);
            body.Add(definitionField);

            atlasLabel = new Label();
            atlasLabel.style.color = SubtleTextColor;
            atlasLabel.style.fontSize = 11;
            atlasLabel.style.whiteSpace = WhiteSpace.Normal;
            atlasLabel.style.marginTop = 8f;
            body.Add(atlasLabel);

            Button setupButton = new Button(SetupAssets) { text = "돌 아틀라스 에셋 셋업" };
            setupButton.style.marginTop = 8f;
            body.Add(setupButton);

            body.Add(CreateInfoBox(
                $"규격 에셋 / 머티리얼 / 텍스처 임포트 설정을 한 번에 맞춘다. "
                + $"머티리얼은 {TrimSheetAssets.BaseMaterialPath}를 복사해 만들고 "
                + "Base Map만 아틀라스로 바꾼다. 아틀라스가 없으면 크랙 셀까지 구워 만든다."));

            VisualElement crackRow = new VisualElement();
            crackRow.style.flexDirection = FlexDirection.Row;
            crackRow.style.alignItems = Align.Center;

            Label crackLabel = new Label("크랙 시드");
            crackLabel.style.width = LabelWidth;
            crackLabel.style.flexShrink = 0f;
            crackRow.Add(crackLabel);

            crackSeedField = new IntegerField { value = crackSeed };
            crackSeedField.style.flexGrow = 1f;
            crackSeedField.RegisterValueChangedCallback(OnCrackSeedChanged);
            crackRow.Add(crackSeedField);

            body.Add(crackRow);

            Button bakeCrackButton = new Button(BakeCrackedAtlas) { text = "크랙 아틀라스 다시 굽기" };
            bakeCrackButton.style.marginTop = 4f;
            body.Add(bakeCrackButton);

            body.Add(CreateHintLabel(
                "크랙 셀(인덱스 2, 3)은 깨끗한 셀에 절차적으로 갈라짐과 깨진 자리를 그린 것이다. "
                + "시드를 바꿔 다시 구우면 같은 에셋이 갱신되므로 머티리얼 참조는 유지된다. "
                + "손상 빈도는 규격 에셋의 Cell Weights로 조절한다."));

            body.Add(CreateHintLabel(
                "셀 UV는 메시에 구워진다. 머티리얼 쪽 Tiling/Offset을 건드리면 "
                + "타일이 이웃 셀로 밀려 나가므로 1/0으로 둔다."));

            return section;
        }

        VisualElement BuildCubeSection()
        {
            VisualElement section = CreateSectionShell("큐브 하나", "CUBE", out VisualElement body);

            Vector3Field sizeField = new Vector3Field("크기 (m)") { value = cubeSize };
            sizeField.labelElement.style.minWidth = LabelWidth;
            sizeField.RegisterValueChangedCallback(OnCubeSizeChanged);
            body.Add(sizeField);

            FloatField cellSpanField = new FloatField("셀 크기 (m)") { value = worldUnitsPerCell };
            cellSpanField.labelElement.style.minWidth = LabelWidth;
            cellSpanField.RegisterValueChangedCallback(OnCellSpanChanged);
            body.Add(cellSpanField);

            tileCountLabel = new Label();
            tileCountLabel.style.color = SubtleTextColor;
            tileCountLabel.style.fontSize = 10;
            tileCountLabel.style.whiteSpace = WhiteSpace.Normal;
            tileCountLabel.style.marginTop = 4f;
            tileCountLabel.style.marginBottom = 6f;
            body.Add(tileCountLabel);

            EnumField cellModeField = new EnumField("셀 사용", cellMode);
            cellModeField.labelElement.style.minWidth = LabelWidth;
            cellModeField.RegisterValueChangedCallback(OnCellModeChanged);
            body.Add(cellModeField);

            IntegerField cellIndexField = new IntegerField("셀 인덱스") { value = cellIndex };
            cellIndexField.labelElement.style.minWidth = LabelWidth;
            cellIndexField.RegisterValueChangedCallback(OnCellIndexChanged);
            body.Add(cellIndexField);

            cellIndexHint = new Label();
            cellIndexHint.style.color = SubtleTextColor;
            cellIndexHint.style.fontSize = 10;
            cellIndexHint.style.marginBottom = 6f;
            body.Add(cellIndexHint);

            Toggle rotationToggle = new Toggle("타일 랜덤 회전") { value = randomRotation };
            rotationToggle.labelElement.style.minWidth = LabelWidth;
            rotationToggle.RegisterValueChangedCallback(OnRandomRotationChanged);
            body.Add(rotationToggle);

            FloatField jitterField = new FloatField("정점 산포 (m)") { value = shapeJitter };
            jitterField.labelElement.style.minWidth = LabelWidth;
            jitterField.RegisterValueChangedCallback(OnShapeJitterChanged);
            body.Add(jitterField);

            FloatField pressField = new FloatField("정점 눌림 (m)") { value = shapePress };
            pressField.labelElement.style.minWidth = LabelWidth;
            pressField.RegisterValueChangedCallback(OnShapePressChanged);
            body.Add(pressField);

            shapeHint = new Label();
            shapeHint.style.color = SubtleTextColor;
            shapeHint.style.fontSize = 10;
            shapeHint.style.whiteSpace = WhiteSpace.Normal;
            shapeHint.style.marginBottom = 6f;
            body.Add(shapeHint);

            IntegerField seedField = new IntegerField("시드") { value = seed };
            seedField.labelElement.style.minWidth = LabelWidth;
            seedField.RegisterValueChangedCallback(OnSeedChanged);
            body.Add(seedField);

            TextField nameField = new TextField("이름") { value = cubeName };
            nameField.labelElement.style.minWidth = LabelWidth;
            nameField.RegisterValueChangedCallback(OnCubeNameChanged);
            body.Add(nameField);

            Button createButton = new Button(CreateSingleCube) { text = "큐브 만들기" };
            createButton.style.marginTop = 8f;
            body.Add(createButton);

            body.Add(CreateHintLabel(
                $"메시는 {TrimSheetAssets.MeshFolder}/[이름].asset으로 저장된다. "
                + "같은 이름이 이미 있으면 내용만 갈아끼우므로 그 메시를 쓰는 오브젝트가 함께 갱신된다."));

            return section;
        }

        VisualElement BuildVariationSection()
        {
            VisualElement section = CreateSectionShell("바리에이션 세트", "VARIATIONS", out VisualElement body);

            body.Add(CreateHintLabel(
                $"{TrimSheetCubeBuilder.Variations.Length}종을 X축으로 늘어놓는다. "
                + "셀 단독 / 타일 혼합 / 텍셀 밀도 / 얇은 판 조합을 한 번에 비교할 수 있다."));

            Vector3Field originField = new Vector3Field("배치 원점") { value = variationOrigin };
            originField.labelElement.style.minWidth = LabelWidth;
            originField.style.marginTop = 8f;
            originField.RegisterValueChangedCallback(OnVariationOriginChanged);
            body.Add(originField);

            Button pivotButton = new Button(() => FillOriginFromSceneView(originField)) { text = "씬 뷰 중심으로" };
            pivotButton.style.marginTop = 4f;
            body.Add(pivotButton);

            IntegerField variationSeedField = new IntegerField("시드") { value = variationSeed };
            variationSeedField.labelElement.style.minWidth = LabelWidth;
            variationSeedField.style.marginTop = 6f;
            variationSeedField.RegisterValueChangedCallback(OnVariationSeedChanged);
            body.Add(variationSeedField);

            variationStatusLabel = new Label();
            variationStatusLabel.style.color = SubtleTextColor;
            variationStatusLabel.style.fontSize = 11;
            variationStatusLabel.style.whiteSpace = WhiteSpace.Normal;
            variationStatusLabel.style.marginTop = 10f;
            variationStatusLabel.style.marginBottom = 8f;
            body.Add(variationStatusLabel);

            VisualElement buttonRow = new VisualElement();
            buttonRow.style.flexDirection = FlexDirection.Row;

            Button buildButton = new Button(BuildVariations) { text = "세트 만들기" };
            buildButton.style.flexGrow = 1f;
            buttonRow.Add(buildButton);

            clearVariationsButton = new Button(ClearVariations) { text = "지우기" };
            clearVariationsButton.style.flexGrow = 1f;
            buttonRow.Add(clearVariationsButton);

            body.Add(buttonRow);

            body.Add(CreateWarnBox(
                $"세트를 다시 만들면 씬의 '{TrimSheetCubeBuilder.VariationRootName}' 루트를 지우고 "
                + "새로 만든다. 그 아래에서 손으로 다듬은 배치는 남지 않는다."));

            return section;
        }

        // ---------------------------------------------------------------------
        // 입력 처리
        // ---------------------------------------------------------------------

        void OnDefinitionChanged(ChangeEvent<Object> evt)
        {
            definition = evt.newValue as TrimSheetDefinition;

            if (definition != null)
                worldUnitsPerCell = definition.worldUnitsPerCell;

            RefreshAtlasStatus();
            RefreshTileCount();
        }

        void OnCubeSizeChanged(ChangeEvent<Vector3> evt)
        {
            cubeSize = evt.newValue;
            RefreshTileCount();
        }

        void OnCellSpanChanged(ChangeEvent<float> evt)
        {
            worldUnitsPerCell = Mathf.Max(TrimSheetDefinition.MinWorldUnitsPerCell, evt.newValue);
            RefreshTileCount();
        }

        void OnCellModeChanged(ChangeEvent<System.Enum> evt)
        {
            cellMode = (TrimSheetCellMode)evt.newValue;
            RefreshAtlasStatus();
        }

        void OnCellIndexChanged(ChangeEvent<int> evt)
        {
            cellIndex = Mathf.Max(0, evt.newValue);
            RefreshAtlasStatus();
        }

        void OnRandomRotationChanged(ChangeEvent<bool> evt)
        {
            randomRotation = evt.newValue;
        }

        void OnShapeJitterChanged(ChangeEvent<float> evt)
        {
            shapeJitter = Mathf.Max(0f, evt.newValue);
            RefreshTileCount();
        }

        void OnShapePressChanged(ChangeEvent<float> evt)
        {
            shapePress = Mathf.Max(0f, evt.newValue);
            RefreshTileCount();
        }

        void OnSeedChanged(ChangeEvent<int> evt)
        {
            seed = evt.newValue;
        }

        void OnCubeNameChanged(ChangeEvent<string> evt)
        {
            cubeName = evt.newValue;
        }

        void OnVariationOriginChanged(ChangeEvent<Vector3> evt)
        {
            variationOrigin = evt.newValue;
        }

        void OnVariationSeedChanged(ChangeEvent<int> evt)
        {
            variationSeed = evt.newValue;
        }

        void OnCrackSeedChanged(ChangeEvent<int> evt)
        {
            crackSeed = evt.newValue;
        }

        void FillOriginFromSceneView(Vector3Field originField)
        {
            SceneView sceneView = SceneView.lastActiveSceneView;

            if (sceneView == null)
                return;

            variationOrigin = sceneView.pivot;
            originField.SetValueWithoutNotify(variationOrigin);
        }

        // ---------------------------------------------------------------------
        // 동작
        // ---------------------------------------------------------------------

        void SetupAssets()
        {
            TrimSheetDefinition ensured = TrimSheetAssets.EnsureStoneDefinition();

            if (ensured == null)
                return;

            definition = ensured;
            definitionField.SetValueWithoutNotify(ensured);
            worldUnitsPerCell = ensured.worldUnitsPerCell;

            EditorGUIUtility.PingObject(ensured);

            RefreshAtlasStatus();
            RefreshTileCount();
        }

        void BakeCrackedAtlas()
        {
            Texture2D baked = TrimSheetCrackBaker.Bake(crackSeed);

            if (baked == null)
            {
                atlasLabel.text = $"크랙 아틀라스 굽기 실패. 소스가 있는지 확인한다: {TrimSheetCrackBaker.SourceAtlasPath}";
                return;
            }

            EditorGUIUtility.PingObject(baked);
            RefreshAtlasStatus();
        }

        void CreateSingleCube()
        {
            if (definition == null)
            {
                atlasLabel.text = "규격 에셋이 없다. '돌 아틀라스 에셋 셋업'을 먼저 실행한다.";
                return;
            }

            string objectName = cubeName;

            if (string.IsNullOrWhiteSpace(objectName))
                objectName = "TrimSheetCube";

            TrimSheetCubeOptions options = new TrimSheetCubeOptions
            {
                size = cubeSize,
                worldUnitsPerCell = worldUnitsPerCell,
                cellMode = cellMode,
                cellIndex = cellIndex,
                randomRotation = randomRotation,
                shapeJitter = shapeJitter,
                shapePress = shapePress,
                seed = seed,
            };

            Vector3 position = Vector3.zero;
            SceneView sceneView = SceneView.lastActiveSceneView;

            if (sceneView != null)
                position = sceneView.pivot;

            GameObject cube = TrimSheetCubeBuilder.CreateCube(definition, options, objectName, position);

            if (cube == null)
                return;

            Selection.activeGameObject = cube;
            EditorGUIUtility.PingObject(cube);
        }

        void BuildVariations()
        {
            if (definition == null)
            {
                variationStatusLabel.text = "규격 에셋이 없다. '돌 아틀라스 에셋 셋업'을 먼저 실행한다.";
                return;
            }

            GameObject root = TrimSheetCubeBuilder.BuildVariationSet(definition, variationOrigin, variationSeed);

            if (root == null)
                return;

            Selection.activeGameObject = root;
            EditorGUIUtility.PingObject(root);

            RefreshVariationStatus();
        }

        void ClearVariations()
        {
            TrimSheetCubeBuilder.ClearVariationSet();
            RefreshVariationStatus();
        }

        // ---------------------------------------------------------------------
        // 표시 갱신
        // ---------------------------------------------------------------------

        void RefreshAtlasStatus()
        {
            if (definition == null)
            {
                atlasLabel.text = "규격 에셋이 없다. '돌 아틀라스 에셋 셋업'으로 만든다.";
                cellIndexHint.text = string.Empty;

                return;
            }

            if (definition.atlas == null)
            {
                atlasLabel.text = $"아틀라스 텍스처가 비었다: {AssetDatabase.GetAssetPath(definition)}";
            }
            else
            {
                atlasLabel.text =
                    $"{definition.atlas.name} {definition.atlas.width}x{definition.atlas.height}, "
                    + $"셀 {definition.columns}x{definition.rows} ({definition.CellCount}장), "
                    + $"셀당 {definition.atlas.width / Mathf.Max(1, definition.columns)}px";
            }

            if (definition.material == null)
                atlasLabel.text += " / 머티리얼 누락";

            if (cellMode == TrimSheetCellMode.MixPerTile)
            {
                cellIndexHint.text = $"혼합 모드에서는 셀 인덱스를 쓰지 않는다. 가중치 {DescribeCellWeights(definition)}.";
                return;
            }

            cellIndexHint.text =
                $"0 ~ {definition.CellCount - 1} (범위를 넘으면 감싼다). "
                + "기본 아틀라스: 0,1 = 깨끗 / 2,3 = 크랙.";
        }

        static string DescribeCellWeights(TrimSheetDefinition definition)
        {
            string[] parts = new string[definition.CellCount];

            for (int i = 0; i < parts.Length; i++)
                parts[i] = definition.GetCellWeight(i).ToString("0.##");

            return "[" + string.Join(", ", parts) + "]";
        }

        void RefreshTileCount()
        {
            float span = Mathf.Max(TrimSheetDefinition.MinWorldUnitsPerCell, worldUnitsPerCell);
            Vector3 size = TrimSheetCubeMesh.ClampSize(cubeSize);

            int tilesX = TrimSheetCubeMesh.ResolveTileCount(size.x, span);
            int tilesY = TrimSheetCubeMesh.ResolveTileCount(size.y, span);
            int tilesZ = TrimSheetCubeMesh.ResolveTileCount(size.z, span);

            int quadCount = 2 * (tilesX * tilesY + tilesY * tilesZ + tilesX * tilesZ);

            tileCountLabel.text =
                $"면 분할 {tilesX} x {tilesY} x {tilesZ} -> 쿼드 {quadCount}장, 정점 {quadCount * 4}개. "
                + $"축당 상한 {TrimSheetCubeMesh.MaxTilesPerAxis}장.";

            // 타일이 면을 정확히 나눠 덮으므로 크기가 셀의 정수배가 아니면 타일이 늘어난다.
            // 조용히 넘어가면 텍셀 밀도가 어긋난 걸 눈으로만 잡아야 하니 수치로 알린다.
            float stretchX = size.x / tilesX / span;
            float stretchY = size.y / tilesY / span;
            float stretchZ = size.z / tilesZ / span;

            float worstStretch = Mathf.Max(
                Mathf.Abs(stretchX - 1f),
                Mathf.Max(Mathf.Abs(stretchY - 1f), Mathf.Abs(stretchZ - 1f)));

            if (worstStretch < 0.01f)
            {
                tileCountLabel.text += " 타일 왜곡 없음.";
                return;
            }

            tileCountLabel.text +=
                $" 타일 왜곡 {stretchX * 100f:F0}% / {stretchY * 100f:F0}% / {stretchZ * 100f:F0}%"
                + $" (크기를 셀 {span:0.##}m의 정수배로 맞추면 100%가 된다).";

            RefreshShapeHint(size, tilesX, tilesY, tilesZ);
        }

        /// <summary>
        /// 변위 상한을 보여준다. 빌더가 조용히 잘라내면 값을 올려도 반응이 없는 것처럼 보인다.
        /// </summary>
        void RefreshShapeHint(Vector3 size, int tilesX, int tilesY, int tilesZ)
        {
            if (shapeHint == null)
                return;

            float smallestTile = Mathf.Min(size.x / tilesX, Mathf.Min(size.y / tilesY, size.z / tilesZ));
            float limit = smallestTile * 0.25f;

            if (shapeJitter <= 0f && shapePress <= 0f)
            {
                shapeHint.text = "변위 없음 - 완전한 박스. 코너 정점을 눌러 쉐입을 벌리려면 값을 올린다.";
                return;
            }

            shapeHint.text =
                $"코너 정점이 가장 많이 움직이고 (엣지 0.6배, 면 내부 0.25배), "
                + $"격자 좌표 해시라 인접 면이 같이 움직여 틈이 생기지 않는다. "
                + $"상한 {limit:0.###}m (가장 작은 타일 {smallestTile:0.##}m의 1/4).";

            if (shapeJitter > limit || shapePress > limit)
                shapeHint.text += " 지금 값은 상한으로 잘린다.";
        }

        void RefreshVariationStatus()
        {
            int cubeCount = TrimSheetCubeBuilder.CountVariationCubes();

            clearVariationsButton.SetEnabled(cubeCount > 0);

            if (cubeCount == 0)
            {
                variationStatusLabel.text = "씬에 세트가 없다.";
                return;
            }

            variationStatusLabel.text =
                $"'{TrimSheetCubeBuilder.VariationRootName}' 아래 큐브 {cubeCount}개. 자유롭게 옮기고 지울 수 있다.";
        }

        // ---------------------------------------------------------------------
        // 레이아웃 헬퍼
        // ---------------------------------------------------------------------

        VisualElement CreateSectionShell(string title, string badge, out VisualElement bodyContainer)
        {
            VisualElement shell = new VisualElement();
            shell.style.flexDirection = FlexDirection.Column;
            shell.style.backgroundColor = PanelBackground;
            shell.style.overflow = Overflow.Hidden;
            shell.style.marginBottom = 10f;

            shell.style.borderLeftWidth = 1f;
            shell.style.borderRightWidth = 1f;
            shell.style.borderTopWidth = 1f;
            shell.style.borderBottomWidth = 1f;
            shell.style.borderLeftColor = PanelBorderColor;
            shell.style.borderRightColor = PanelBorderColor;
            shell.style.borderTopColor = PanelBorderColor;
            shell.style.borderBottomColor = PanelBorderColor;

            shell.Add(CreateSectionHeader(title, badge));
            shell.Add(CreateSectionAccentBar());

            bodyContainer = new VisualElement();
            bodyContainer.style.flexGrow = 1f;
            bodyContainer.style.flexDirection = FlexDirection.Column;
            bodyContainer.style.paddingLeft = 14f;
            bodyContainer.style.paddingRight = 14f;
            bodyContainer.style.paddingTop = 12f;
            bodyContainer.style.paddingBottom = 14f;
            bodyContainer.style.backgroundColor = PanelBackground;

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
            header.style.backgroundColor = HeaderBackground;
            header.style.paddingLeft = 14f;
            header.style.paddingRight = 14f;

            VisualElement leftGroup = new VisualElement();
            leftGroup.style.flexDirection = FlexDirection.Row;
            leftGroup.style.alignItems = Align.Center;

            VisualElement accent = new VisualElement();
            accent.style.width = 2f;
            accent.style.height = 18f;
            accent.style.backgroundColor = AccentColor;
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
                badge.style.color = SubtleTextColor;
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
            bar.style.backgroundColor = PanelBorderColor;

            return bar;
        }

        VisualElement CreateInfoBox(string message)
        {
            return CreateAccentBox(message, InfoAccentColor);
        }

        VisualElement CreateWarnBox(string message)
        {
            return CreateAccentBox(message, WarnAccentColor);
        }

        VisualElement CreateAccentBox(string message, Color accent)
        {
            VisualElement box = new VisualElement();
            box.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.3f);
            box.style.paddingLeft = 12f;
            box.style.paddingRight = 12f;
            box.style.paddingTop = 10f;
            box.style.paddingBottom = 10f;
            box.style.borderLeftWidth = 2f;
            box.style.borderLeftColor = accent;
            box.style.marginTop = 8f;
            box.style.marginBottom = 4f;

            Label label = new Label(message);
            label.style.color = SubtleTextColor;
            label.style.fontSize = 11;
            label.style.whiteSpace = WhiteSpace.Normal;
            box.Add(label);

            return box;
        }

        Label CreateHintLabel(string message)
        {
            Label label = new Label(message);
            label.style.color = SubtleTextColor;
            label.style.fontSize = 10;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginTop = 8f;

            return label;
        }
    }
}
