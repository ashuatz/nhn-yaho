# Rendering/ - 포그 및 렌더링 디버그

두 시스템이 있다.

- `LutHeightFog/` + `Assets/Shaders/LutHeightFog/`, `Assets/Shaders/ScavengerSimpleLit/` - 2D LUT 거리/높이 포그
- `DofDebug/` + `Assets/Shaders/DofDebug/` - DOF 포커스 디버그 뷰

---

# LUT 거리/높이 포그

네임스페이스: `Scavenger.Rendering` (런타임), `Scavenger.Rendering.EditorTools` (에디터)

어셈블리: `Scavenger.Rendering`, `Scavenger.Rendering.Editor`
(URP Runtime/Core Runtime 참조. `Scavenger.Game`은 건드리지 않았다.)

2D LUT 한 장으로 거리 포그와 높이 포그를 동시에 표현한다.
`Magic/Packages/com.com2us.tadept.originstylizedpbr`의 CustomForwardFog(LUT HeightFog 모드)를
클린룸으로 재구현한 것.

## 두 가지 적용 모드

`LutHeightFogRendererFeature.applyMode`로 고른다. **상호 배타다** (동시 적용하면 포그가 두 번 얹힌다).

| | `ScreenSpacePass` | `ForwardMaterial` |
|---|---|---|
| 합성 위치 | 깊이 기반 풀스크린 패스 | `Scavenger/Simple Lit` 포워드 프래그먼트 |
| 대상 | 스톡 URP 셰이더 포함 화면 전체 | 그 셰이더를 쓰는 오브젝트만 |
| 하늘 | `skyDensity`로 처리 가능 | 미적용 (스카이박스는 별개 셰이더) |
| 비용 | 컬러 사본 1장 + 풀스크린 패스 | 추가 패스 없음 |
| 깊이 텍스처 | 필요 (`ConfigureInput(Depth)`) | 불필요 |
| 파라미터 전달 | 패스 내부 MaterialPropertyBlock | 글로벌 `Shader.SetGlobalXxx` |
| Deferred 렌더러 | 동작 | **불투명 오브젝트 미적용** (GBuffer 경로) |

현재 렌더러는 PC=Forward+(`m_RenderingMode: 2`), Mobile=Forward(`0`)라 둘 다 쓸 수 있다.

`ForwardMaterial`이 원본(Magic 패키지)과 같은 구조다. `ScreenSpacePass`는 셰이더를 하나도
건드리지 않고 화면 전체에 넣기 위한 경로다. LUT 의미(UV 매핑, 알파=농도, 선형 역보간)는
두 모드와 원본이 모두 동일하며, 수식은 `LutHeightFogCore.hlsl` 한 곳에만 있다.

## 원본과의 차이

| | 원본 (Magic 패키지) | 여기 |
|---|---|---|
| 모드 | DefaultFog / MipmapFog / LUTFog / LUTHeightFog 4종 | LUT 높이 포그만 |
| 셰이더 키워드 | `_LUTFOG` / `_MIPMAPFOG` 멀티컴파일 | 없음 (전용 셰이더/패스) |
| 거리 팩터 | Unity `unity_FogParams` 재사용 | 볼륨의 `distanceRange`로 직접 계산 |
| 하늘 | 별도 `SkyBoxWithFog.shader` | `ScreenSpacePass`의 `skyDensity` |
| 램프 편집 | 볼륨 인스펙터 내 커스텀 드로어 | 별도 베이커 윈도우 |

거리 팩터를 Unity 포그 설정에서 떼어낸 이유: 원본은 `unity_FogParams`를 덮어써서
Unity 기본 포그 설정과 얽혔다. 여기서는 볼륨 값만으로 계산해 두 모드가 같은 결과를 낸다.

## LUT 규약

- `U` = 카메라 선형 시야 거리. 0 = `distanceRange.start`, 1 = `distanceRange.end`
- `V` = 월드 Y 높이. 0 = `heightRange.start`, 1 = `heightRange.end`
- `RGB` = 포그 색 (sRGB), `A` = 포그 농도
- 합성식: `lerp(sceneColor, lut.rgb, saturate(lut.a * density))`
- 텍스처는 256x64 무압축 sRGB, 밉맵 없음, Clamp

## 파일 목차

### 런타임 (`LutHeightFog/`)

- **LutHeightFogParameters.cs** (82줄)
  - L11-L51 `FogRange` 구조체: `start`/`end`. L32 `InverseSpan()` = 셰이더용 `1/(end-start)`.
    구간이 0에 붕괴하면 `MinSpan`(1e-4)으로 나눗셈을 막고, `start > end` 역전은
    유효한 연출이라 부호를 유지한다.
  - L53-L72 `FogRangeParameter`: `VolumeParameter<FogRange>` + `Interp`. `TypeName`은 드로어가 검사.
  - L74-L81 `LutHeightFogShaderIds`: 셰이더 프로퍼티 ID 캐시.

- **LutHeightFogVolume.cs** (70줄)
  - `VolumeComponent` + `IPostProcessComponent`. 볼륨 메뉴 `Scavenger/LUT Height Fog`.
  - L15-L31 파라미터: `fogLut`, `distanceRange`(0~120), `heightRange`(0~20), `density`(0~2), `skyDensity`(0~1)
    `fogLut`은 `TextureDimension.Tex2D`로 못박았다. 셰이더가 `TEXTURE2D`로 선언하므로
    큐브맵/3D/배열이 들어오면 바인딩이 깨진다.
  - L33-L45 `IsActive()`: LUT 없거나 density 0이면 패스를 건너뛴다.
  - L50-L60 `GetRangeParams()` = `_FogRangeParams` (거리시작, 거리역구간, 높이시작, 높이역구간)
  - L65-L68 `GetBlendParams()` = `_FogBlendParams` (density, skyDensity, 0, 0)

- **LutHeightFogPass.cs** (107줄)
  - `ScriptableRenderPass`. RenderGraph 전용 (`RecordRenderGraph`).
  - L26-L33 생성자: `ConfigureInput(Depth)` + `requiresIntermediateTexture = true`.
    후자가 없으면 백버퍼 직행 시 씬 컬러를 읽을 수 없다.
  - L40-L84 `RecordRenderGraph`: 볼륨 조회 → 씬 컬러 사본 생성(`AddBlitPass`)
    → 래스터 패스에서 사본+깊이를 읽어 활성 컬러에 되그린다.
    같은 타겟을 읽으며 쓸 수 없어 사본이 필요하다.
  - L86-L96 `ExecuteFogPass`: MaterialPropertyBlock 세팅 + `DrawProcedural` 풀스크린 삼각형.
  - L98-L105 `PassData`

- **LutHeightFogRendererFeature.cs** (172줄)
  - `ScriptableRendererFeature`. 렌더러 에셋(`Assets/Settings/PC_Renderer.asset` 등)에 추가한다.
  - L15-L30 `FogApplyMode`: L21 `ScreenSpacePass` / L29 `ForwardMaterial`
  - L45-L50 `InjectionPoint`: `ScreenSpacePass` 합성 시점. 기본 `BeforeRenderingPostProcessing`.
  - L55-L61 `Create()`
  - L63-L100 `AddRenderPasses()`: Preview/Reflection 카메라와 `IsOffscreenDepthTexture`
    (깊이 전용 타겟) 카메라를 제외한다. 후자는 컬러 리소스가 없어 사본 생성이 성립하지 않는다.
    이후 모드에 따라 글로벌만 채우고 리턴하거나, 풀스크린 패스를 큐에 넣는다.
  - L102-L117 `PushGlobalFogParams()`: `Scavenger/Simple Lit`이 읽는 글로벌을 채운다.
    임의 머티리얼이 대상이라 MaterialPropertyBlock으로는 전달할 수 없다.
  - L119-L122 `DisableGlobalFogParams()`: 농도를 0으로 눌러 포워드 셰이더의 이중 적용을 막는다.
    `ScreenSpacePass` 모드에서 매 프레임 호출된다.
  - L124-L144 `TryGetMaterial()`: 셰이더 미지원/누락 시 경고 로그 후 패스 스킵.
  - L146-L153 `Dispose()`: 머티리얼 파괴.
  - L157-L169 `OnValidate` (에디터 전용): `Shader.Find` 결과를 에셋에 직렬화.
    Shader.Find는 빌드에서 신뢰할 수 없어 여기서 한 번 고정해야 빌드에 포함된다.

### 에디터 (`Editor/`)

- **FogLutGradientData.cs** (57줄): 거리/높이 `Gradient` 한 쌍을 담는 ScriptableObject.
  `EditorJsonUtility`로 직렬화하려면 SO여야 한다 (Gradient는 JsonUtility 미지원).
  L24-L55 `ResetToDefault()` = 가까울수록 투명한 무채색 기본값.

- **FogLutBaker.cs** (207줄)
  - L16-L17 `LutWidth`=256, `LutHeight`=64 / L21 `OverridablePlatforms`
  - L26-L51 `EvaluatePixels()`: `픽셀(x,y) = 거리그라디언트(x) * 높이그라디언트(y)` (알파 포함 성분별 곱).
    `(width-1)`로 나눠 마지막 픽셀이 정확히 t=1이 되게 한다.
    반환 배열은 아래에서 위로(y=0이 V=0) 채워진다.
  - L53-L62 `FillPreview()`: 창 내부 임시 텍스처용.
  - L66-L73 `IsBakeablePath()`: **.png만 허용**. PNG로만 인코딩하므로 .tga/.jpg 경로에
    쓰면 그 에셋이 손상된다. `Bake()`가 이걸로 가드한다.
  - L79-L114 `Bake()`: 경로 검사 → PNG 기록 → 임포트 → 임포터 세팅 → userData에 그라디언트 실음.
  - L116-L143 `TryLoadGradients()`: userData에서 그라디언트 복원 (재편집 경로).
  - L145-L181 `ApplyImportSettings()`:
    - `alphaIsTransparency = false` **필수**. 켜면 Unity가 투명 영역 RGB를 확장해 포그 색이 뭉개진다.
    - `Uncompressed`. 64KB라 압축 이득이 없고 블록 압축은 계조에 밴딩을 만든다.
    - `sRGBTexture = true`. 그라디언트는 sRGB 표시 공간에서 편집된다.
  - L183-L195 `ClearPlatformOverrides()`: 기본 설정만 바꾸면 플랫폼별 오버라이드가 남아
    빌드에서 압축될 수 있다. Standalone/Android/iPhone/WebGL 오버라이드를 걷어낸다.
  - L197-L205 `ToAbsolutePath()`

- **FogRangePropertyDrawer.cs** (76줄): `FogRange`를 Start/End 한 줄로 그린다.
  `[CustomPropertyDrawer(typeof(FogRange))]` — **`VolumeParameterDrawer`가 아니다.**
  볼륨 인스펙터는 override 체크박스 높이를 `EditorGUI.GetPropertyHeight`로 미리 예약하고
  본문은 가로 레이아웃 스코프 안에서 그린다. 일반 `PropertyDrawer`로 두면 높이 계산과
  그리기가 같은 클래스에서 나와 어긋날 수 없다.
  (`VolumeParameterDrawer`에서 `GetControlRect`로 줄을 새로 잡으면 다음 파라미터와 겹친다 -
  실제로 그렇게 만들었다가 인스펙터가 겹쳐서 이 방식으로 교체했다.)

- **LutHeightFogBakerWindow.cs** (559줄): 메뉴 `Scavenger/LUT Height Fog Baker`
  - L36 `targetIsOwnedLut`: 대상이 이 창에서 구운 PNG LUT인지. **임의 텍스처 덮어쓰기 방지 가드.**
  - L47-L51 `Open()`
  - L53-L86 `OnEnable`/`OnDisable`: 프리뷰 텍스처와 그라디언트 SO를 창 생명주기에 묶는다.
    `[SerializeField] targetLut`은 에셋 참조라 도메인 리로드를 견디고, 리로드 후
    그 텍스처의 userData에서 편집 상태를 복구한다.
  - L88-L110 `CreateGUI()`
  - L113-L236 섹션 구성 (L113 SOURCE / L155 PREVIEW / L191 OUTPUT)
  - L240-L275 `OnTargetChanged` + L257-L264 `IsOwnedLutPath()` (소유 판정)
  - L277-L326 `CreateNewLut`(SaveFilePanelInProject, png 강제) / `RebakeTargetLut` / `BakeTo`
  - L328-L379 `ReloadFromTarget` / `ResetGradients` / L356 `RefreshButtonStates`
    (소유 LUT이 아니면 다시 굽기/불러오기를 비활성화하고 이유를 표시)
  - L382-L415 `RefreshPreview()`: 체커보드 위에 `lerp(체커, LUT색, LUT알파)` 합성.
    셰이더의 합성식과 같아 알파가 농도로 읽힌다.
  - L428-L557 레이아웃 헬퍼 (Unity-Editor-Layout-SKILL 규약)

### 셰이더 (`Assets/Shaders/`)

#### `LutHeightFog/` - 공통 수식 + 스크린스페이스 경로

- **LutHeightFogCore.hlsl** (67줄) — **포그 수식의 유일한 소유자.** 두 모드가 함께 include한다.
  - L15-L30 `_FogLut` / `_FogRangeParams` / `_FogBlendParams` 선언과 매크로.
    글로벌이 안 채워진 상태(전부 0)에서는 농도 0이라 포그가 적용되지 않는다 - 안전한 기본값.
  - L34-L37 `ComputeFogLutCoord()`: `saturate((value - start) * invSpan)` 역보간
  - L42-L46 `ComputeLinearEyeDepthFromWorld()`: 뷰 행렬로 z를 뽑아 투영 방식과 무관하게 동작.
    `LinearEyeDepth`는 오소 카메라에서 틀린 값을 준다.
  - L48-L51 `SampleFogLut()`
  - L54-L65 `ApplyLutHeightFog(color, positionWS)`: 월드 좌표 하나로 합성. 포워드 경로가 쓴다.

- **LutHeightFog.shader** (33줄): `Hidden/Scavenger/LutHeightFog`. 단일 패스, ZWrite Off, ZTest Always.
- **LutHeightFog.hlsl** (44줄) — 스크린스페이스 프래그먼트만.
  - L11-L18 `IsSkyDepth()`: `UNITY_REVERSED_Z` 분기
  - L20-L42 `FragLutHeightFog()`: 하늘 조기 분기 → 월드 좌표 복원 → `ApplyLutHeightFog`

#### `ScavengerSimpleLit/` - 포워드 머티리얼 경로

- **ScavengerSimpleLit.shader** (327줄): `Scavenger/Simple Lit`
  - URP Simple Lit 파생. 프로퍼티/키워드/렌더스테이트를 원본과 동일하게 유지해
    머티리얼 인스펙터(`SimpleLitShader` GUI)와 SRP Batcher 호환을 살렸다.
  - 패스: L72 ForwardLit / L156 ShadowCaster / L202 DepthOnly / L246 DepthNormals / L293 Meta
  - **GBuffer / Universal2D / MotionVectors 패스는 없다.** 현재 렌더러가 Forward/Forward+라
    불필요하다. Deferred로 바꾸면 이 셰이더는 불투명 오브젝트를 그리지 못한다.
  - ForwardLit만 원본과 다르다: `#pragma fragment ScavengerSimpleLitFragment`

- **ScavengerSimpleLitForwardPass.hlsl** (59줄)
  - URP `SimpleLitForwardPass.hlsl`을 include해 `Varyings` / `LitPassVertexSimple` /
    `InitializeInputData` / `InitializeSimpleLitSurfaceData`를 그대로 재사용한다.
    재구현한 코드가 없어 URP 업데이트 시 표류 위험이 작다.
  - L16-L57 `ScavengerSimpleLitFragment()`: URP `LitPassFragmentSimple`의 본문과 같고
    L48 한 줄만 다르다 — `MixFog(...)` → `ApplyLutHeightFog(color.rgb, inputData.positionWS)`

## 셋업 순서

공통:
1. `Scavenger > LUT Height Fog Baker`로 LUT 텍스처를 굽는다.
2. 렌더러 에셋(`Assets/Settings/*_Renderer.asset`)에 `LUT Height Fog` 피처를 추가한다.
3. 씬의 Volume 프로파일에 `Scavenger > LUT Height Fog` 오버라이드를 추가하고
   `Fog Lut`에 1번 텍스처를 지정한다. 각 파라미터의 override 체크박스를 켜야 값이 적용된다.

`ScreenSpacePass` 모드 (스톡 셰이더 그대로 쓰고 싶을 때):
4. 피처의 `Apply Mode`를 `ScreenSpacePass`로 둔다. 끝.

`ForwardMaterial` 모드 (원본과 같은 구조, 더 저렴):
4. 피처의 `Apply Mode`를 `ForwardMaterial`로 바꾼다.
5. 포그에 잠겨야 하는 오브젝트의 머티리얼 셰이더를 `Scavenger/Simple Lit`으로 바꾼다.
   이 셰이더를 안 쓰는 오브젝트와 스카이박스는 포그가 적용되지 않는다.

## 주의

- 두 모드를 동시에 켤 수 없다. 피처가 `ScreenSpacePass`일 때 글로벌 농도를 0으로 눌러
  포워드 셰이더 쪽 이중 적용을 막는다.
- `ScreenSpacePass`는 깊이 텍스처가 필요하다. 피처가 `ConfigureInput(Depth)`로 요청하지만,
  렌더러에서 Depth Texture를 강제로 끄면 동작하지 않는다.
- `ScreenSpacePass`는 씬 컬러 사본 때문에 풀스크린 텍스처 1장이 추가로 잡힌다.
  모바일에서 대역폭이 문제되면 `ForwardMaterial` 모드를 쓴다.
- `skyDensity = 0`으로 두면 스카이박스를 건드리지 않아 원본과 같은 동작이 된다.
- **`distanceRange`는 카메라 눈 깊이 기준이라 카메라 거리에 직접 묶인다.**
  게임 카메라는 초망원 원근(`FollowCamera`, fov 18)이라 오쏘 기준보다 51.7656만큼
  뒤에 있고, `distanceRange`도 그만큼 밀어(51.7656~171.7656) 같은 구간을 덮는다.
  `FollowCamera.fieldOfView`를 바꾸면 후퇴량이 바뀌므로 여기도 다시 밀어야 한다
  (DOF `focusDistance`, 씬 빌트인 포그 start/end도 동일).
- 렌더러를 Deferred로 바꾸면 `ForwardMaterial`은 불투명 오브젝트에 적용되지 않는다
  (GBuffer 경로로 가고 이 셰이더엔 GBuffer 패스가 없다). 그때는 `ScreenSpacePass`를 쓴다.

## 검토 후 의도적으로 남긴 설계 (재지적 방지)

- **원거리 평면 깊이를 전부 하늘로 취급한다** (`IsSkyDepth`).
  깊이만으로는 하늘과 원거리 평면에 붙은 지오메트리를 구분할 수 없다는 지적이 있었으나,
  원거리 평면을 넘는 지오메트리는 이미 클리핑되어 그려지지 않으므로 실제 오작동 경로가 없다.
  URP 스크린스페이스 포그의 통상적인 방식이기도 하다.
  하늘에 별도 룩이 필요해지면 그때 스텐실/하늘 마스크를 도입한다.
- 정확한 하늘 분리가 필요하면 `skyDensity = 0`으로 하늘을 아예 빼는 경로가 이미 있다.

---

# DOF 포커스 디버그 뷰

`focusDistance` / `focalLength`를 조절할 때 **어디가 또렷하게 보이는지** 흑백으로 확인하는 도구.
URP `DepthOfField` 볼륨의 실제 값을 읽어 **URP와 동일한 CoC(착란원) 수식**으로 계산한다.
디버그 뷰가 실제 블러와 어긋나면 쓸모가 없으므로 절대 다르게 계산하지 않는다.

## CoC 수식 (URP 원본과 동일)

Bokeh (`focusDistance` / `focalLength` / `aperture` 사용):
```
F = focalLength / 1000            // mm -> m
A = focalLength / aperture        // 조리개 지름
maxCoC = (A * F) / (P - F)        // P = focusDistance
coc = (1 - P / eyeDepth) * maxCoC // 0 = 완벽한 포커스, 부호는 근/원경
blurAmount = saturate(abs(coc))
```
출처: URP `PostProcessPassRenderGraph.cs` Bokeh 셋업 + `BokehDepthOfField.shader` `FragCoC`.

Gaussian (원경만 블러):
```
blurAmount = saturate((eyeDepth - gaussianStart) / (gaussianEnd - gaussianStart))
```

포커스 구간은 `|1 - P/d| * maxCoC = threshold`를 d에 대해 풀어 얻는다:
`near = P / (1 + t/maxCoC)`, `far = P / (1 - t/maxCoC)` (`t/maxCoC >= 1`이면 무한).

## 표시 방식

| 모드 | 내용 |
|---|---|
| `FocusMask` | **흰색 = 포커스, 검정 = 흐려지는 영역.** 기본값 |
| `Overlay` | 씬 위에 겹쳐 무엇이 포커스인지 형체로 확인 (블러 영역을 어둡게) |
| `BlurAmount` | 블러 강도 계조. 흰색 = 최대 블러. 전이 구간 확인용 |

`FocusThreshold` = 포커스로 인정할 CoC 크기. 마스크 두께와 포커스 구간 계산의 기준.

## 파일 목차

### 런타임 (`DofDebug/`)

- **DofDebugSettings.cs** (219줄)
  - L8-L20 `DofDebugView` 열거 (FocusMask / Overlay / BlurAmount)
  - L27-L71 `DofDebugState`: **세션 한정 static 상태.** 렌더러 에셋이나 볼륨을 더럽히지 않는다.
    도메인 리로드에서 자동 초기화되고 창이 닫히면 꺼진다.
    - L51-L55 `Disable()`
    - L58-L70 `IsRenderingLive()`: 패스가 최근 4프레임 안에 그려졌는지.
      피처를 렌더러에 추가하지 않으면 토글이 조용히 무반응이 되므로 창에서 이걸로 진단한다.
  - L74-L115 `DofDebugParams`: 볼륨에서 뽑은 CoC 계산 입력.
    - L96-L102 `HasDegenerateBokehCoC()`: `maxCoC <= 0` 감지.
      `focusDistance <= focalLength/1000`이면 CoC 부호가 뒤집힌다 (URP도 동일하게 깨진다).
    - L104-L110 `ToShaderParams()`
  - L117-L209 `DofDebugMath`
    - L123-L147 `TryGetParams()`: 볼륨 스택에서 읽고 Mode가 Off면 false.
    - L149-L171 `BuildParams()`: URP의 Bokeh 파라미터 계산 재현.
    - L176-L207 `TryComputeFocusRange()`: 포커스 거리 구간.
  - L212-L218 `DofDebugShaderIds`

- **DofDebugPass.cs** (103줄): `ScriptableRenderPass`, RenderGraph 전용.
  - L24-L31 생성자: `ConfigureInput(Depth)` + `requiresIntermediateTexture = true`
  - L38-L81 `RecordRenderGraph`: 상태/볼륨 확인 → 컬러 사본 → 래스터 패스.
    끝에서 `DofDebugState.LastRenderedFrame`을 기록한다 (창의 라이브 표시용).
  - L84-L93 `ExecuteDebugPass`

- **DofDebugRendererFeature.cs** (108줄)
  - L25-L34 `Create()`: 주입 시점을 `AfterRenderingPostProcessing`으로 고정.
    후처리 결과 위에 덮어써야 최종 화면 기준으로 판단할 수 있다.
  - L36-L59 `AddRenderPasses()`: 꺼져 있으면 셰이더 로드도 시도하지 않는다.
  - L61-L82 `TryGetMaterial()`

### 에디터

- **DofDebugWindow.cs** (563줄): 메뉴 `Scavenger > DOF Focus Debug`
  - L49-L53 `Open()`
  - L55-L62 `OnDisable()`: **디버그 뷰를 반드시 끈다.**
    화면 전체를 흑백으로 덮는 뷰가 켜진 채 남으면 혼란스럽다.
  - L64-L88 `CreateGUI()`: 끝에서 100ms 주기 갱신을 등록한다.
    볼륨 값은 렌더링 중에 갱신되므로 주기적으로 다시 읽어야 한다.
  - L91-L259 섹션 구성 (L91 VIEW / L146 READOUT / L197 VOLUMES)
  - L261-L330 `RefreshReadout()` 및 Bokeh/Gaussian 별 표시
  - L332-L346 `RefreshStatus()`: "그려지고 있음 / 켜져 있지만 그려지지 않음" 진단
  - L386-L408 `FindVolumesWithDepthOfField()`: `profile`이 아니라 `sharedProfile`을 읽는다.
    `profile` 게터는 런타임 사본을 만들어 부작용이 생긴다.
  - L441-L563 레이아웃 헬퍼 (Unity-Editor-Layout-SKILL 규약)

### 셰이더 (`Assets/Shaders/DofDebug/`)

- **DofDebug.shader** (33줄): `Hidden/Scavenger/DofDebug`
- **DofDebug.hlsl** (92줄)
  - L13-L31 `_DofDebugParams` / `_DofDebugView` 선언과 매크로
  - L35-L48 `ComputeDofBlurAmount()`: Bokeh/Gaussian 분기. URP 수식 그대로.
  - L51-L55 `ComputeFocusMask()`: 임계값 안쪽 흰색, 밖 검정. 경계만 살짝 부드럽게.
  - L58-L90 `FragDofDebug()`: 모드별 출력

## 셋업

1. 렌더러 에셋(`Assets/Settings/*_Renderer.asset`)에 `DOF Focus Debug` 피처를 추가한다. (1회)
2. 씬 Volume 프로파일에 `DepthOfField`를 추가하고 Mode를 `Bokeh`로 한다.
3. `Scavenger > DOF Focus Debug` 창을 열고 "디버그 뷰 켜기"를 체크한다.
4. 값 편집은 Volume 인스펙터에서. 창의 VOLUMES 섹션에서 해당 볼륨을 바로 선택할 수 있다.

## 주의

- 창을 닫으면 디버그 뷰가 꺼진다. 의도된 동작이다.
- 값 편집 기능은 창에 넣지 않았다. 여러 볼륨이 블렌딩될 때 어디에 써야 하는지 추측하지
  않기 위함이다. 창은 **적용 결과**(스택 보간 값)를 읽기 전용으로 보여준다.
- 깊이 변환에 `LinearEyeDepth`를 쓴다. 오소 카메라에서는 틀린 값이지만
  URP DOF 자체가 그렇게 계산하므로 일치시키는 쪽을 택했다 (DOF는 퍼스펙티브 전용 기능).
- `Mode: Gaussian`에는 `focusDistance` / `focalLength`가 없다. 창이 경고로 알려준다.
