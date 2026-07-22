using System.Reflection;
using UnityEngine;

namespace Scavenger.Tests
{
    /// <summary>
    /// EditMode에서는 AddComponent가 Awake/OnDestroy를 호출하지 않는다.
    /// MonoBehaviour 수명 주기가 필요한 테스트는 이 헬퍼로 명시 호출한다.
    /// </summary>
    static class EditModeLifecycle
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        public static void InvokeAwake(MonoBehaviour target)
        {
            Invoke(target, "Awake");
        }

        public static void InvokeOnDestroy(MonoBehaviour target)
        {
            Invoke(target, "OnDestroy");
        }

        static void Invoke(MonoBehaviour target, string methodName)
        {
            if (target == null)
                return;

            MethodInfo method = target.GetType().GetMethod(methodName, Flags);

            if (method == null)
                return;

            method.Invoke(target, null);
        }
    }
}
