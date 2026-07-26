using UnityEngine;

namespace Scavenger.Field
{
    /// <summary>밝기 보정을 따로 걸 수 있는 갈래 (깊이 구분 때문에 레이어를 나눈다).</summary>
    public enum GreyboxTone
    {
        /// <summary>바닥/파밍 포인트/아이템 오브젝트 등 걸어다니는 구조물.</summary>
        Field,

        /// <summary>근경 배경 블록 (팔레트 0~5: 럽블 / 상부층 / 데브리).</summary>
        Background,

        /// <summary>중경 매스 (팔레트 6).</summary>
        Midground,

        /// <summary>원경 스카이라인 (팔레트 7).</summary>
        Far,

        /// <summary>플레이어 등 캐릭터.</summary>
        Character,
    }

    /// <summary>
    /// 그레이박스 룩 설정 (사용자 지시 2026-07-26: 밝기를 에디터에서 조절할 것).
    ///
    /// 코드의 색 상수는 **기준색**(색상 + 채도)이고, 실제 밝기는 여기서 정한다.
    /// 이렇게 나눈 이유는 프리팹/머티리얼을 다시 구울 때마다 코드 상수가 이기기 때문이다 -
    /// 조절값이 에셋에 있어야 재생성을 견딘다.
    ///
    /// 보정 규칙: **색상/채도는 그대로 두고 명도(V)만 배수**. 배수 결과가 1을 넘으면
    /// 채널을 개별로 자르지 않고 채도를 지키며 V=1까지만 올린다 (개별로 자르면 색이 틀어진다).
    ///
    /// 편집은 메뉴 Scavenger > Greybox Brightness. 값을 바꾼 뒤 "적용"을 눌러야
    /// 이미 만들어진 머티리얼/프리팹에 반영된다 (런타임 생성물은 즉시 반영).
    /// </summary>
    [CreateAssetMenu(menuName = "Scavenger/Greybox Theme", fileName = "GreyboxTheme")]
    public sealed class GreyboxTheme : ScriptableObject
    {
        /// <summary>기본 명도 배수. 2026-07-26 밝기 보정에서 정한 값.</summary>
        public const float DefaultBrightness = 1.7f;

        [Header("전체 명도 배수 (V). 색상/채도는 건드리지 않는다")]
        [Range(0.25f, 3f)] public float brightness = DefaultBrightness;

        [Header("갈래별 추가 배수 (전체 배수에 곱해진다)")]
        [Range(0.25f, 2f)] public float fieldBrightness = 1f;
        [Range(0.25f, 2f)] public float backgroundBrightness = 1f;
        [Range(0.25f, 2f)] public float characterBrightness = 1f;

        [Header("깊이 레이어 미세 조정 (배경 배수 위에 곱해진다)")]
        [Range(0.25f, 2f)] public float midgroundBrightness = 1f;
        [Range(0.25f, 2f)] public float farBrightness = 1f;

        /// <summary>런타임에서 쓰는 설정. FieldSpawner가 직렬화 참조로 주입한다.</summary>
        public static GreyboxTheme Active { get; private set; }

        public static void SetActive(GreyboxTheme theme)
        {
            Active = theme;
        }

        /// <summary>
        /// 기준색에 밝기 보정을 적용한다. 설정 에셋이 없으면 기본 배수를 쓴다 -
        /// 0으로 두면 배선을 잊었을 때 화면이 통째로 어두워져 원인 찾기가 어렵다.
        /// </summary>
        public static Color Tint(Color color, GreyboxTone tone)
        {
            if (Active != null)
                return Active.Apply(color, tone);

            return Scale(color, DefaultBrightness);
        }

        public Color Apply(Color color, GreyboxTone tone)
        {
            return Scale(color, ResolveScale(tone));
        }

        /// <summary>이 갈래에 걸리는 최종 명도 배수.</summary>
        public float ResolveScale(GreyboxTone tone)
        {
            float category = ResolveCategory(tone);

            return Mathf.Max(0f, brightness * category);
        }

        float ResolveCategory(GreyboxTone tone)
        {
            // 중경/원경은 배경 배수를 함께 받는다 - 배경 슬라이더 하나로 전체가 움직이고,
            // 레이어별 미세 조정은 그 위에 곱해진다
            if (tone == GreyboxTone.Midground)
                return backgroundBrightness * midgroundBrightness;

            if (tone == GreyboxTone.Far)
                return backgroundBrightness * farBrightness;

            if (tone == GreyboxTone.Background)
                return backgroundBrightness;

            if (tone == GreyboxTone.Character)
                return characterBrightness;

            return fieldBrightness;
        }

        /// <summary>
        /// 명도만 배수. V x 배수가 1을 넘으면 채도를 지키려 V=1까지만 올린다
        /// (채널을 개별로 클램프하면 밝은 채널만 멈춰 색상이 틀어진다).
        /// </summary>
        public static Color Scale(Color color, float multiplier)
        {
            float value = Mathf.Max(color.r, Mathf.Max(color.g, color.b));

            if (value <= 0.0001f)
                return color;

            float scale = Mathf.Min(multiplier, 1f / value);

            return new Color(color.r * scale, color.g * scale, color.b * scale, color.a);
        }

        void OnValidate()
        {
            brightness = Mathf.Max(0.05f, brightness);
            fieldBrightness = Mathf.Max(0.05f, fieldBrightness);
            backgroundBrightness = Mathf.Max(0.05f, backgroundBrightness);
            characterBrightness = Mathf.Max(0.05f, characterBrightness);
            midgroundBrightness = Mathf.Max(0.05f, midgroundBrightness);
            farBrightness = Mathf.Max(0.05f, farBrightness);
        }
    }
}
