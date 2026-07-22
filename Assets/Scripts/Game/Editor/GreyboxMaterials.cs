using UnityEditor;
using UnityEngine;

namespace Scavenger.EditorTools
{
    /// <summary>
    /// 그레이박스 머티리얼 에셋 관리. new Material(...)을 프리팹/씬에 그대로 물리면
    /// 디스크 에셋이 아니라서 리로드 후 참조가 깨져 마젠타가 된다 (실제 발생).
    /// 색상별 머티리얼을 Assets/Materials/Greybox/에 에셋으로 만들어 재사용한다.
    /// </summary>
    public static class GreyboxMaterials
    {
        const string FolderParent = "Assets/Materials";
        const string Folder = "Assets/Materials/Greybox";

        public static Material Ensure(string materialName, Color color)
        {
            EnsureFolder();

            string path = $"{Folder}/{materialName}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material != null)
                return material;

            Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
            material = new Material(litShader);
            material.color = color;

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>색상 기반 자동 이름 (팔레트류 - 같은 색은 같은 에셋 재사용).</summary>
        public static Material EnsureForColor(Color color)
        {
            string hex = ColorUtility.ToHtmlStringRGB(color);
            return Ensure($"Env_{hex}", color);
        }

        static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(FolderParent))
                AssetDatabase.CreateFolder("Assets", "Materials");

            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder(FolderParent, "Greybox");
        }
    }
}
