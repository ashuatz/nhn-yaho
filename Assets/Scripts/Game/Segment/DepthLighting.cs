using Scavenger.Run;
using UnityEngine;

namespace Scavenger.Segment
{
    /// <summary>
    /// 깊이별 조도 변화 (M4-2, 라이팅 카테고리). 깊이가 깊어질수록 앰비언트와
    /// 주광을 어둡게 내려 깊이 체감을 만든다. 씬의 베이스 라이팅 값을 런 시작 시
    /// 캡처하고 배율만 적용 - 씬 튜닝 값을 훼손하지 않는다.
    /// 시인성 가드: 배율 하한(minAmbientFactor/minLightFactor)이 완전 암전을 막는다.
    /// 수치는 프리팹 튜닝 지점.
    /// </summary>
    public sealed class DepthLighting : MonoBehaviour
    {
        [Header("주광 (비우면 씬에서 Directional 자동 탐색)")]
        public Light directionalLight;

        [Header("어두워지는 속도: 이 깊이에서 최저 조도 도달")]
        public int depthForDarkest = 8;

        [Header("배율 하한 = 시인성 가드")]
        [Range(0.2f, 1f)] public float minAmbientFactor = 0.55f;
        [Range(0.2f, 1f)] public float minLightFactor = 0.65f;

        [Header("전환 시간 (초)")]
        public float transitionSeconds = 1.5f;

        Color baseAmbientSky;
        Color baseAmbientEquator;
        Color baseAmbientGround;
        float baseLightIntensity;
        bool baseCaptured;

        float currentDarkness01;
        float targetDarkness01;
        RunManager subscribedRun;

        void Start()
        {
            TrySubscribe();
        }

        void OnDestroy()
        {
            if (subscribedRun == null)
                return;

            subscribedRun.RunStarted -= OnRunStarted;
            subscribedRun.DepthChanged -= OnDepthChanged;
        }

        void Update()
        {
            // RunManager가 늦게 뜨는 씬 구성 대비
            if (subscribedRun == null)
            {
                TrySubscribe();
                return;
            }

            if (!baseCaptured)
                return;

            float speed = 1f / Mathf.Max(0.01f, transitionSeconds);
            currentDarkness01 = Mathf.MoveTowards(currentDarkness01, targetDarkness01, speed * Time.deltaTime);

            ApplyDarkness(currentDarkness01);
        }

        void TrySubscribe()
        {
            RunManager run = RunManager.Instance;

            if (run == null)
                return;

            subscribedRun = run;
            run.RunStarted += OnRunStarted;
            run.DepthChanged += OnDepthChanged;
        }

        void OnRunStarted()
        {
            CaptureBaseOnce();
            SetTargetForDepth(subscribedRun.Depth);
        }

        void OnDepthChanged(int depth)
        {
            SetTargetForDepth(depth);
        }

        void SetTargetForDepth(int depth)
        {
            if (depthForDarkest <= 1)
            {
                targetDarkness01 = 1f;
                return;
            }

            targetDarkness01 = Mathf.Clamp01((depth - 1) / (float)(depthForDarkest - 1));
        }

        void CaptureBaseOnce()
        {
            if (baseCaptured)
                return;

            baseAmbientSky = RenderSettings.ambientSkyColor;
            baseAmbientEquator = RenderSettings.ambientEquatorColor;
            baseAmbientGround = RenderSettings.ambientGroundColor;

            if (directionalLight == null)
                directionalLight = FindDirectionalLight();

            if (directionalLight != null)
                baseLightIntensity = directionalLight.intensity;

            baseCaptured = true;
        }

        void ApplyDarkness(float darkness01)
        {
            float ambientFactor = Mathf.Lerp(1f, minAmbientFactor, darkness01);
            float lightFactor = Mathf.Lerp(1f, minLightFactor, darkness01);

            RenderSettings.ambientSkyColor = baseAmbientSky * ambientFactor;
            RenderSettings.ambientEquatorColor = baseAmbientEquator * ambientFactor;
            RenderSettings.ambientGroundColor = baseAmbientGround * ambientFactor;

            if (directionalLight != null)
                directionalLight.intensity = baseLightIntensity * lightFactor;
        }

        static Light FindDirectionalLight()
        {
            Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);

            foreach (Light light in lights)
            {
                if (light.type == LightType.Directional)
                    return light;
            }

            return null;
        }
    }
}
