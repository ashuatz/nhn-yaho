using Scavenger;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Scavenger.EditorTools
{
    /// <summary>
    /// 그레이박스 씬 원클릭 구성. 메뉴 실행 한 번으로 플레이 가능한 씬을 만든다.
    /// 씬에는 GameBootstrap 하나와 라이트만 있으면 된다 (나머지는 코드 조립).
    /// </summary>
    public static class GreyboxSceneSetup
    {
        const string ScenePath = "Assets/Scenes/Greybox.unity";

        [MenuItem("Scavenger/Setup Greybox Scene")]
        public static void SetupScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 부트스트랩: 런타임에 전체 시스템을 코드로 조립
            GameObject bootstrap = new GameObject("GameBootstrap");
            bootstrap.AddComponent<GameBootstrap>();

            // 그레이박스 라이트
            GameObject lightObject = new GameObject("Directional Light");
            Light directional = lightObject.AddComponent<Light>();
            directional.type = LightType.Directional;
            directional.intensity = 1.1f;
            lightObject.transform.rotation = Quaternion.Euler(55f, -35f, 0f);

            EditorSceneManager.SaveScene(scene, ScenePath);

            UnityEngine.Debug.Log($"[Setup] Greybox scene saved: {ScenePath}. Play를 눌러 실행.");
        }
    }
}
