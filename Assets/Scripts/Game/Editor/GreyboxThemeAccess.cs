using Scavenger.Field;
using UnityEditor;
using UnityEngine;

namespace Scavenger.EditorTools
{
    /// <summary>
    /// 에디터에서 그레이박스 밝기 설정(GreyboxTheme)을 읽는 창구.
    ///
    /// 프리팹/머티리얼을 굽는 코드는 전부 여기를 거쳐 색을 보정한다 - 보정 지점이
    /// 흩어지면 어떤 에셋은 배수가 걸리고 어떤 에셋은 안 걸리는 상태가 된다.
    /// 에셋이 없으면 기본값으로 1회 만든다 (없다고 조용히 원색을 쓰면
    /// 재생성 때마다 화면이 어두워졌다 밝아졌다 한다).
    /// </summary>
    public static class GreyboxThemeAccess
    {
        public const string ThemePath = "Assets/Settings/GreyboxTheme.asset";

        public static GreyboxTheme Load()
        {
            GreyboxTheme existing = AssetDatabase.LoadAssetAtPath<GreyboxTheme>(ThemePath);

            if (existing != null)
                return existing;

            if (!AssetDatabase.IsValidFolder("Assets/Settings"))
                AssetDatabase.CreateFolder("Assets", "Settings");

            GreyboxTheme created = ScriptableObject.CreateInstance<GreyboxTheme>();
            AssetDatabase.CreateAsset(created, ThemePath);

            UnityEngine.Debug.Log($"[Greybox] 밝기 설정 생성: {ThemePath}");

            return created;
        }

        /// <summary>기준색에 현재 설정의 밝기 보정을 적용한다.</summary>
        public static Color Tint(Color baseColor, GreyboxTone tone)
        {
            GreyboxTheme theme = Load();

            if (theme == null)
                return GreyboxTheme.Scale(baseColor, GreyboxTheme.DefaultBrightness);

            return theme.Apply(baseColor, tone);
        }
    }
}
