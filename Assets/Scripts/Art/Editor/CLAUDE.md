# Art/Editor - 트림시트 오소링

네임스페이스: `Scavenger.ArtTools`
어셈블리: `Scavenger.Art.Editor` (에디터 전용, 외부 참조 없음)

아틀라스 텍스처 한 장으로 큐브 바리에이션을 만드는 트림시트 워크플로우.
런타임 코드는 없다. 결과물(구운 메시 + 머티리얼)만 씬에 남는다.

## 기준 규격

**1m 큐브 / 셀 0.5m = 면마다 2x2 타일** (사용자 지정).
`TrimSheetCubeBuilder.UnitBlockSize` / `UnitCellSpan`이 이 값을 들고 있고
1m 큐브는 24쿼드 96정점이 된다.

## 정점 변위 (쉐입 바리에이션)

같은 크기 큐브도 쉐입이 조금씩 다르도록 격자점을 흔든다. **끝쪽 정점을 더 크게** 움직인다:
큐브 코너(3축이 경계) 1.0배, 엣지(2축) 0.6배, 면 내부(1축) 0.25배.
`shapePress`는 안쪽으로 밀어 모서리가 닳은 느낌을, `shapeJitter`는 사방 산포를 만든다.

**변위는 격자 좌표의 해시로 결정한다.** 순차 난수로 흔들면 인접한 두 면이 공유 정점을
서로 다르게 밀어 큐브 모서리가 갈라진다. 해시는 어느 면에서 계산해도 같은 값이 나오므로
변위 후에도 메시가 닫혀 있다. 이게 이 기능의 핵심 제약이다.

변위 상한은 가장 작은 타일의 1/4 (`MaxOffsetPerTile`). 넘으면 이웃 쿼드를 관통해
면이 뒤집힌다. 창이 상한과 잘림 여부를 표시한다.

법선은 변위된 실제 코너에서 다시 구한다 (축 법선을 그대로 쓰면 눌린 게 안 보인다).

## 왜 UV를 메시에 굽는가

아틀라스 셀은 텍스처의 일부라서 UV를 1 밖으로 늘려 타일링할 수 없다
(이웃 셀이 새어 나온다). 그래서 각 면을 셀 크기의 쿼드 격자로 쪼개고
**쿼드 하나가 셀 하나를 꽉 채우도록** UV를 굽는다.

결과:
- 머티리얼 한 장 (`Common.mat` 복사본). 커스텀 셰이더 불필요
- 큐브 크기와 무관하게 일정한 텍셀 밀도
- 타일 단위 셀 혼합 / 90도 회전으로 반복감 제거
- 크랙 타일을 **별도 셀**로 두고 가중치만 낮춰 손상 빈도를 조절

## 에셋 경로

| 경로 | 내용 |
|------|------|
| `Assets/Art/Tex/TrimSheet_Stone_01.png` | 소스: 512x256, 깨끗한 셀 2장 (크랙 베이커 입력) |
| `Assets/Art/Tex/TrimSheet_Stone_02.png` | 실사용: 512x512, 셀 4장 (아래 줄 깨끗, 위 줄 크랙) |
| `Assets/Materials/Greybox/TrimSheet_Stone_01.mat` | `Common.mat` 복사 + Base Map만 교체 |
| `Assets/Art/TrimSheets/TrimSheet_Stone_01.asset` | `TrimSheetDefinition` (2x2, 셀 0.5m, 가중치 1/1/0.3/0.3) |
| `Assets/Art/Meshes/TrimSheet/*.asset` | 구운 큐브 메시 |

셀 인덱스는 좌하단 0부터 가로 우선: **0,1 = 깨끗 / 2,3 = 크랙**.

### 소스 아틀라스의 유래

`TrimSheet_Stone_01.png`은 `Assets/Art/Tex/test.png`(281x131 크롭)에서 다시 자른 것이다.
원본은 모르타르(어두운 띠) 중심이 x=2 / 146 / 276, y=2 / 125에 있어
**띠 중심에서 잘라야** 각 셀이 상하좌우로 이음선 없이 반복된다.
셀 A = 원본 (2, 2, 144, 123), 셀 B = (146, 2, 130, 123), 각각 256x256으로 리샘플.

이 리팩은 코드에 없다 (1회 작업). 원본이 갱신되면 이 사각형을 다시 재서 리팩하고
`Bake Cracked Atlas`를 다시 돌린다.

## 파일 목차

- **TrimSheetDefinition.cs** (151줄): 아틀라스 규격 ScriptableObject.
  메뉴 `Assets > Create > Scavenger > Trim Sheet Definition`.
  - L17-L38 `atlas` / `material` / `columns` / `rows` / `worldUnitsPerCell` / `insetHalfTexel` / `cellWeights`
  - L42 `CellCount` / L45-L58 `IsReady()` - 머티리얼과 **아틀라스**가 모두 있어야 통과.
    아틀라스가 없으면 하프 텍셀 인셋을 계산할 수 없어 경계에서 이웃 셀이 새어 나온다
  - L61-L71 `GetCellWeight()` - 지정 없으면 1 (균등)
  - L76-L100 `PickWeightedCell()` - 가중치 추첨. 전부 0이면 균등으로 폴백
  - L106-L128 `GetCellUv()` - 셀 인덱스 -> UV Rect. 범위 초과는 감싼다
  - L132-L150 `ApplyHalfTexelInset()` - 셀 경계를 반 텍셀 안으로. 바이리니어 번짐 차단
  - **에디터 전용 어셈블리에 있다.** 씬/프리팹이 이 에셋을 참조하면 빌드에서 끊어진다

- **TrimSheetCubeMesh.cs** (261줄): 메시 빌더 (순수 계산, 에셋 접근 없음).
  - L8-L16 `TrimSheetCellMode`: `Single` / `MixPerTile`
  - L18-L35 `TrimSheetCubeOptions`: size / worldUnitsPerCell / cellMode / cellIndex / randomRotation / seed
  - L48 `MaxTilesPerAxis` = 32 (정점 폭발 방어) / L53-L61 `Face` 구조체
  - L67-L109 `Build()` - 시드 고정 `System.Random`. 정점 65535 초과 시 `IndexFormat.UInt32`
  - L111-L154 `BuildFaces()` - **와인딩 규약: 삼각형 (0,1,2)의 앞면 법선 = cross(uDir, vDir).**
    각 면은 `cross(uDir, vDir) == normal`을 만족한다 (Unity 기본 쿼드와 같은 규약).
    피벗은 Unity 기본 큐브와 같은 중심
  - L156-L201 `AppendFace()` - 면을 정확히 나눠 덮으므로 타일이 잘리지 않는다. 면마다 하드 엣지
  - L203-L212 `ClampSize()` - 메시가 실제로 쓰는 크기. **콜라이더도 이걸 써야 어긋나지 않는다**
  - L214-L227 `ResolveTileCount()` - `round(면길이 / 셀크기)`, 최소 1 최대 32.
    **크기가 셀의 정수배가 아니면 타일이 늘어난다** (아래 "타일 왜곡" 참조)
  - L229-L259 `AppendTileUvs()` - `PickWeightedCell` 추첨 + 90도 회전.
    셀 사방에 모르타르가 있어 회전해도 이음선은 유지된다

- **TrimSheetCrackBaker.cs** (397줄): 크랙 아틀라스 베이크.
  메뉴 `Scavenger > Trim Sheet > Bake Cracked Atlas`
  - L18-L21 소스/출력 경로 + `DefaultSeed`
  - L46-L100 `Bake()` - 2x1 소스를 2x2로 확장. 아래 줄은 원본 복사,
    위 줄은 복사 후 크랙/치핑을 그린다. 셀마다 시드를 `+ cellX * 7919`로 벌린다
  - L103-L134 `LoadSourceAtlas()` - **PNG를 파일에서 직접 읽는다.**
    임포트된 텍스처는 `isReadable`이 꺼져 있어 `GetPixels32`를 쓸 수 없다 (임포터 미변경 우회)
  - L137-L150 `DrawCracks()` - 셀당 2~3줄. 모르타르에서 출발해 안쪽으로 파고든다
  - L152-L196 `GetEdgeStart()` - 네 변 중 하나에서 시작점 + 안쪽 방향 (+-40도 산포)
  - L198-L251 `WalkCrack()` - 걸어가며 어둡게. 중간에 1회 분기, 끝으로 갈수록 옅어진다
  - L253-L304 `DrawChips()` - 깨진 자리. **방향별 반지름을 흔들어 각진 윤곽**을 만든다.
    원형으로 찍으면 구멍을 뚫은 것처럼 보인다. 안쪽은 밝게(갓 드러난 면), 테두리 25%만 그늘
  - L306-L321 `SampleSectorRadius()` - 섹터 반지름 보간
  - L323-L374 `DarkenBrush` / `DarkenPixel` / `BrightenPixel`.
    `DarkenPixel`은 **0.55까지만** 누른다 - 원본 돌이 이미 어두워(휘도 80 남짓)
    세게 누르면 잉크로 그은 선처럼 뜬다
  - L376-L396 `WriteAtlas()` - 같은 경로에 덮어쓴다 (에셋 갱신이라 머티리얼 참조 유지)

- **TrimSheetAssets.cs** (296줄): 에셋 셋업. 메뉴 `Scavenger > Trim Sheet > Setup Stone Atlas Assets`
  - L17-L26 경로 상수 + `CrackedCellWeight`(0.3) + `_BaseMap` 프로퍼티 ID
  - L33-L78 `EnsureStoneDefinition()` - 기존 규격 에셋이 있으면 **사용자가 만진 값은 덮어쓰지 않고**
    끊어진 참조만 메운다
  - L80-L103 `CreateStoneDefinition()` - 신규 생성 시에만 2x2 / 셀 0.5m / 가중치 기본값을 넣는다
  - L105-L124 `EnsureAtlas()` - 아틀라스가 없으면 크랙 베이커로 굽는다
  - L126-L176 `EnsureAtlasImport()` - **`npotScale = None` 필수.**
    Unity가 POT로 리스케일하면 셀 경계가 텍셀 격자에서 밀려 UV 분할이 셀 중앙을 자른다.
    Repeat / Bilinear / 밉맵 켬 / aniso 4
  - L178-L229 `EnsureMaterial()` - `Common.mat`을 **복사**한다.
    `Shader.Find`로 새로 만들면 그레이박스 공통 룩(스무스니스/메탈릭/렌더 상태)이 어긋난다.
    Tiling/Offset을 1/0으로 강제 - UV가 메시에 구워져 있어 머티리얼 타일링은 반드시 1:1
  - L231-L276 `SaveMesh()` - 두 가지 안전장치:
    1. 같은 경로에 **메시가 아닌 에셋**이 있으면 거부한다.
       `AssetDatabase.CreateAsset`은 그 에셋을 통째로 날려버린다
    2. 기존 메시를 갈아끼울 때 `Undo.RegisterCompleteObjectUndo`를 먼저 건다.
       그 메시를 쓰는 다른 씬/프리팹의 지오메트리가 여기서 바뀌므로 되돌릴 길이 있어야 한다
    갱신은 `EditorUtility.CopySerialized` - 새 에셋으로 만들면 기존 참조가 끊긴다
  - L278-L295 `EnsureFolder()` - 중간 폴더까지 재귀 생성

- **TrimSheetCubeBuilder.cs** (210줄): 씬 배치. 창과 메뉴가 같은 결과를 내도록 생성 로직 단일 소유
  - L7-L34 `TrimSheetVariation` - 바리에이션 프리셋 항목
  - L46-L49 `UnitBlockSize`(1m) / `UnitCellSpan`(0.5m) - 기준 규격
  - L56-L74 `Variations` - 표준 11종: 깨끗 A/B 단독, 크랙 A/B 단독, 가중 혼합 3종,
    2m 큐브, 얇은 판, 벽, 기둥
  - L80-L130 `CreateCube()` - 메시 굽기 + 저장 + GameObject 배치.
    `BoxCollider` 크기는 `TrimSheetCubeMesh.ClampSize`를 거친다. `Undo.RegisterCreatedObjectUndo` 등록
  - L132-L182 `BuildVariationSet()` - X축 정렬. 피벗이 중심이라 바닥을 원점에 맞추려면 y를 절반 올린다.
    항목마다 시드를 `+ i * 977`로 벌려 혼합 패턴이 겹쳐 보이지 않게 한다
  - L184-L209 `FindVariationRoot()` / `ClearVariationSet()` / `CountVariationCubes()`

- **TrimSheetCubeWindow.cs** (685줄): 메뉴 `Scavenger > Trim Sheet > Cube Authoring`
  - L52-L57 `Open()` / L59-L63 `OnEnable()` - 규격 에셋을 기본 경로에서 자동 복구
  - L65-L85 `CreateGUI()`
  - L91-L285 섹션 구성 (L91 ATLAS / L168 CUBE / L232 VARIATIONS)
  - L288-L370 입력 처리. L358-L368 `FillOriginFromSceneView()` - 씬 뷰 피벗을 배치 원점으로
  - L373-L470 동작 (`SetupAssets` / `BakeCrackedAtlas` / `CreateSingleCube` /
    `BuildVariations` / `ClearVariations`)
  - L476-L560 표시 갱신. L510-L545 `RefreshTileCount()` -
    쿼드/정점 수와 **타일 왜곡 비율**을 만들기 전에 보여준다
  - L553-L612 레이아웃 헬퍼 (Unity-Editor-Layout-SKILL 규약)
  - UIElements 타입 이름이 `UnityEditor.UIElements`와 `UnityEngine.UIElements` 양쪽에 있어
    `ObjectField` / `Vector3Field`는 using 별칭으로 못박았다 (CS0104 방지)

## 사용 순서

1. `Scavenger > Trim Sheet > Cube Authoring` 창을 연다
2. **돌 아틀라스 에셋 셋업** - 크랙 아틀라스 / 머티리얼 / 규격 에셋을 1회 생성
3. **세트 만들기** - 바리에이션 11종을 씬에 늘어놓고 룩을 확인
4. 크랙 모양이 마음에 안 들면 **크랙 시드**를 바꿔 **크랙 아틀라스 다시 굽기**.
   손상 빈도는 규격 에셋의 `Cell Weights`(크랙 셀 2,3)로 조절
5. 개별 큐브가 필요하면 CUBE 섹션에서 크기/셀/시드를 정해 **큐브 만들기**

현재 `Assets/Art/Scenes/Art_Test.unity`에 세트가 배치되어 있다 (z = -8).

## 주의

- **타일 왜곡**: 타일 수는 `round(면길이 / 셀크기)`로 정하고, 그 수로 면을 *정확히 나눠* 덮는다.
  즉 크기가 셀의 정수배가 아니면 타일이 늘거나 줄어든다 (1.4m / 셀 0.5m = 3장을 각 0.467m로).
  타일을 잘라서 텍셀 밀도를 지키는 대안은 면 경계에서 모르타르가 끊겨 더 나쁘다.
  창의 CUBE 섹션이 실제 왜곡 비율을 표시하므로 그걸 보고 크기를 셀의 정수배로 맞춘다.
  `MaxTilesPerAxis`(32) 상한에 걸리는 큰 면에서도 같은 일이 생긴다
- 머티리얼 Tiling/Offset을 건드리면 안 된다. UV가 메시에 구워져 있어
  타일이 이웃 셀로 밀려 나간다 (셋업이 매번 1/0으로 되돌린다)
- **원거리 밉 레벨에서는 셀이 서로 번진다** (512x512에 셀 4장이라 mip 8에서 2x2).
  크랙 셀과 깨끗한 셀은 거의 같은 회색이라 실제로는 눈에 띄지 않아 밉 제한을 걸지 않았다.
  색이 크게 다른 셀(다른 재질 등)을 추가하면 셀 사이 패딩이나 텍스처 배열이 필요해진다.
  하프 텍셀 인셋은 mip 0의 바이리니어 번짐만 막고 밉 번짐은 막지 못한다
- 랜덤 회전은 셀 사방에 모르타르가 있는 아틀라스에서만 안전하다.
  방향성 있는 트림(계단, 몰딩)을 셀로 넣으면 회전을 꺼야 한다
- 메시 이름이 곧 에셋 경로다. 같은 이름으로 다시 구우면 그 메시를 쓰는
  모든 씬/프리팹의 지오메트리가 바뀐다 (Undo에 등록되지만 의도한 것인지 확인할 것)
- 세트를 다시 만들면 `TrimSheet Variations` 루트를 지우고 새로 만든다.
  그 아래에서 손으로 다듬은 배치는 남지 않는다 (개별 큐브를 루트 밖으로 옮겨두면 유지된다)
- `BuildVariationSet`은 **활성 씬**에 만든다. 여러 씬을 오가며 작업할 때는
  의도한 씬이 활성인지 확인한다
