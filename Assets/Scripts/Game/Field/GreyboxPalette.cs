using System.Collections.Generic;
using UnityEngine;

namespace Scavenger.Field
{
    /// <summary>
    /// 런타임 생성 오브젝트의 머티리얼 공급기 (사용자 지시).
    /// 기준 머티리얼은 Assets/Materials/Greybox/Common.mat - FieldSpawner가
    /// 직렬화 참조로 주입한다 (런타임 경로 하드코딩/Resources 의존 회피).
    ///
    /// 색이 같으면 같은 머티리얼 인스턴스를 돌려쓴다. 이전에는 오브젝트마다
    /// renderer.material로 인스턴스를 1장씩 만들어 존 하나에 수십 장이 생겼고,
    /// SRP 배칭도 색마다 끊겼다. 색 종류 수만큼만 만들면 그 문제가 사라진다.
    /// 기준 머티리얼이 없으면 기존 동작(프리미티브 기본 머티리얼 복제)으로 폴백한다.
    /// </summary>
    public static class GreyboxPalette
    {
        static Material baseMaterial;

        // 색 -> 공유 인스턴스. 도메인 리로드에서 초기화된다 (에디터 플레이 반복 안전)
        static readonly Dictionary<Color, Material> tintedCache = new Dictionary<Color, Material>();

        /// <summary>기준 머티리얼 주입. 바뀌면 캐시를 버린다.</summary>
        public static void SetBaseMaterial(Material material)
        {
            if (baseMaterial == material)
                return;

            baseMaterial = material;
            tintedCache.Clear();
        }

        /// <summary>
        /// 대상 렌더러에 색을 적용한다. 기준 머티리얼이 있으면 그것을 복제한
        /// 공유 인스턴스를, 없으면 기존처럼 프리미티브 기본 머티리얼을 복제해 쓴다.
        /// </summary>
        public static void Apply(GameObject target, Color color)
        {
            Apply(target, color, GreyboxTone.Field);
        }

        /// <summary>
        /// 갈래를 지정해 적용한다. 색은 기준색이고 밝기 보정(GreyboxTheme)은 여기서 걸린다 -
        /// 런타임 생성물의 밝기 보정 창구를 한 곳으로 모으기 위함.
        /// </summary>
        public static void Apply(GameObject target, Color color, GreyboxTone tone)
        {
            if (target == null)
                return;

            Renderer targetRenderer = target.GetComponent<Renderer>();

            if (targetRenderer == null)
                return;

            Color tinted = GreyboxTheme.Tint(color, tone);

            if (baseMaterial == null)
            {
                // 폴백: 기준 머티리얼 미주입 (에디터 프리뷰/테스트 경로)
                targetRenderer.material.color = tinted;
                return;
            }

            targetRenderer.sharedMaterial = Resolve(tinted);
        }

        /// <summary>
        /// 발광 머티리얼 (탈출 지점 랜드마크 등). 색마다 1장 공유.
        /// 기준 머티리얼이 없으면 렌더러 기본 머티리얼에 발광만 얹는다.
        /// </summary>
        public static void ApplyGlow(GameObject target, Color color, float emissionScale)
        {
            if (target == null)
                return;

            Renderer targetRenderer = target.GetComponent<Renderer>();

            if (targetRenderer == null)
                return;

            // 발광색은 밝기 보정을 걸지 않는다 - 이미 발광 강도로 밝기를 정하는 색이라
            // 배수를 또 걸면 랜드마크가 흰색으로 날아간다

            // 발광은 색+강도 조합이 키가 되므로 알파에 강도를 실어 캐시를 분리한다
            Color glowKey = new Color(color.r, color.g, color.b, emissionScale);

            if (tintedCache.TryGetValue(glowKey, out Material cached))
            {
                targetRenderer.sharedMaterial = cached;
                return;
            }

            Material glow = baseMaterial != null
                ? new Material(baseMaterial)
                : new Material(targetRenderer.sharedMaterial);

            glow.color = color;
            glow.EnableKeyword("_EMISSION");
            glow.SetColor("_EmissionColor", color * emissionScale);
            glow.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            glow.enableInstancing = true;

            tintedCache[glowKey] = glow;
            targetRenderer.sharedMaterial = glow;
        }

        /// <summary>
        /// 공유 틴트 머티리얼을 직접 얻는다 (기준 머티리얼 미주입이면 null).
        /// GPU 인스턴싱 드로우(EnvironmentRenderer)가 팔레트별로 참조한다 -
        /// 배경도 Common.mat 쉐이더를 쓰게 하는 경로 (사용자 지시).
        /// 반환된 머티리얼은 공유 자산이므로 호출자가 Destroy하면 안 된다.
        /// </summary>
        public static Material GetTinted(Color color)
        {
            return GetTinted(color, GreyboxTone.Field);
        }

        /// <summary>갈래를 지정해 공유 틴트 머티리얼을 얻는다 (배경 팔레트 등).</summary>
        public static Material GetTinted(Color color, GreyboxTone tone)
        {
            if (baseMaterial == null)
                return null;

            return Resolve(GreyboxTheme.Tint(color, tone));
        }

        static Material Resolve(Color color)
        {
            if (tintedCache.TryGetValue(color, out Material cached))
                return cached;

            Material tinted = new Material(baseMaterial);
            tinted.color = color;
            tinted.enableInstancing = true;

            tintedCache[color] = tinted;
            return tinted;
        }
    }
}
