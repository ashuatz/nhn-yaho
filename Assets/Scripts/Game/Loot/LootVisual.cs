using UnityEngine;

namespace Scavenger.Loot
{
    /// <summary>
    /// 길바닥 아이템 큐브의 발광/부유 연출 (웹 프로토타입 이식, ADR-0008).
    /// URP Lit의 emission을 켜 Bloom과 함께 빛나게 하고, 위아래 bob + 회전으로
    /// 눈에 띄게 한다. 순수 비주얼 - 판정은 LootSpot이 소유. 인스턴스 머티리얼은
    /// OnDestroy에서 해제(누수 방지). 색/세기/속도는 스포너가 주입하거나 기본값.
    /// </summary>
    public sealed class LootVisual : MonoBehaviour
    {
        [Header("발광 (Bloom과 함께 빛남)")]
        public Color glowColor = Color.white;
        public float emissionIntensity = 2.2f;

        [Header("부유 (bob) / 회전")]
        public float bobHeight = 0.12f;
        public float bobSpeed = 2.6f;
        public float spinDegreesPerSecond = 55f;

        [Header("바닥에 깔리는 발광 표식 (선택)")]
        public bool addPointLight = true;
        public float lightRange = 2.4f;
        public float lightIntensity = 1.6f;

        Transform visual;
        Material instanceMaterial;
        Light glowLight;

        float baseLocalY;
        float phase;

        // 스포너가 색만 지정할 때 쓰는 편의 초기화 - 큐브를 만들어 발광 세팅
        public void Configure(Color color)
        {
            glowColor = color;
        }

        void Start()
        {
            ResolveVisual();

            // 개체마다 살짝 다른 위상 - 전부 동시에 출렁이지 않게 (위치 기반 결정적)
            phase = (transform.position.x * 12.9898f + transform.position.z * 78.233f) % (Mathf.PI * 2f);

            SetupMaterial();
            SetupLight();
        }

        void OnDestroy()
        {
            if (instanceMaterial != null)
                Destroy(instanceMaterial);
        }

        void Update()
        {
            if (visual == null)
                return;

            // bob: 사인파로 위아래. 회전: y축 스핀
            float t = Time.time * bobSpeed + phase;
            float y = baseLocalY + Mathf.Sin(t) * bobHeight;

            Vector3 local = visual.localPosition;
            local.y = y;
            visual.localPosition = local;

            visual.Rotate(Vector3.up, spinDegreesPerSecond * Time.deltaTime, Space.Self);
        }

        void ResolveVisual()
        {
            // 자식 "Visual"이 있으면 그것을, 없으면 자기 자신을 대상으로
            Transform child = transform.Find("Visual");

            if (child != null)
                visual = child;
            else
                visual = transform;

            baseLocalY = visual.localPosition.y;
        }

        void SetupMaterial()
        {
            Renderer renderer = visual.GetComponent<Renderer>();

            if (renderer == null)
                return;

            // 인스턴스 머티리얼 - 개체별 발광색. 파괴 시 해제
            instanceMaterial = renderer.material;

            instanceMaterial.color = glowColor;

            instanceMaterial.EnableKeyword("_EMISSION");
            instanceMaterial.SetColor("_EmissionColor", glowColor * emissionIntensity);
            instanceMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        void SetupLight()
        {
            if (!addPointLight)
                return;

            GameObject lightObject = new GameObject("GlowLight");
            lightObject.transform.SetParent(visual, false);
            lightObject.transform.localPosition = Vector3.up * 0.2f;

            glowLight = lightObject.AddComponent<Light>();
            glowLight.type = LightType.Point;
            glowLight.color = glowColor;
            glowLight.range = lightRange;
            glowLight.intensity = lightIntensity;
            glowLight.shadows = LightShadows.None;
        }
    }
}
