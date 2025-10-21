using System;
using UnityEngine;

namespace Charasiew.ECS
{
    public class CameraTargetSingleton : MonoBehaviour
    {
        public static CameraTargetSingleton Instance;

        private void Awake()
        {
            if (Instance != null)
            {
                Debug.LogWarning("[CameraTargetSingleton] There is more than one instance of CameraTargetSingleton !", Instance);
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }
    }
}