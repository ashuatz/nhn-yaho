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
        float baseAmbientIntensity;
        float baseLightIntensity;
        bool baseCaptured;

        float currentDarkness01;
        float targetDarkness01;
        RunManager subscribedRun;

        void OnEnable()
        {
            TrySubscribe();
        }

        // 비활성화 시 전역 상태(RenderSettings/라이트)를 원복하고 구독 해제
        // (Codex 검토 반영: 컴포넌트를 꺼도 마지막 어둠이 남지 않게)
        void OnDisable()
        {
            Unsubscribe();

            if (!baseCaptured)
                return;

            currentDarkness01 = 0f;
            targetDarkness01 = 0f;
            ApplyDarkness(0f);
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
            if (subscribedRun != null)
                return;

            RunManager run = RunManager.Instance;

            if (run == null)
                return;

            subscribedRun = run;
            run.RunStarted += OnRunStarted;
            run.DepthChanged += OnDepthChanged;

            // Start 순서 경합으로 RunStarted를 놓친 경우 동기화 (Codex 검토 반영)
            if (run.StateMachine != null && run.StateMachine.Current == RunState.Running)
                OnRunStarted();
        }

        void Unsubscribe()
        {
            if (subscribedRun == null)
                return;

            subscribedRun.RunStarted -= OnRunStarted;
            subscribedRun.DepthChanged -= OnDepthChanged;
            subscribedRun = null;
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
            baseAmbientIntensity = RenderSettings.ambientIntensity;

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

            // Skybox 앰비언트 모드에서는 3색이 아니라 강도가 조도를 결정한다
            // (Greybox 씬이 Skybox 모드 - Codex 검토 반영, 양쪽 모두 커버)
            RenderSettings.ambientIntensity = baseAmbientIntensity * ambientFactor;

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
