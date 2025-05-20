// Copyright (c) Meta Platforms, Inc. and affiliates.

using Meta.XR;
using Meta.XR.Samples;
using UnityEngine;

#if UNITY_ANDROID
using UnityEngine.Android;
#endif

    public class EnvironmentRayCastSampleManager : MonoBehaviour
    {
        private const string SPATIALPERMISSION = "com.oculus.permission.USE_SCENE";
        [SerializeField] private EnvironmentRaycastManager m_raycastManager;

        private void Start()
        {
            if (!EnvironmentRaycastManager.IsSupported)
            {
                Debug.LogError("EnvironmentRaycastManager is not supported: please read the official documentation to get more details. (https://developers.meta.com/horizon/documentation/unity/unity-depthapi-overview/)");
            }
        }

        public bool HasScenePermission()
        {
#if UNITY_ANDROID
            return Permission.HasUserAuthorizedPermission(SPATIALPERMISSION);
#else
            return true;
#endif
        }

        public (Vector3?, Vector3?) PlaceGameObjectByScreenPosAndRot(Ray ray)
        {
            if (EnvironmentRaycastManager.IsSupported)
            {
                if (m_raycastManager.Raycast(ray, out var hitInfo))
                {
                    return (hitInfo.point, hitInfo.normal);
                }
                else
                {
                    Debug.Log("RaycastManager failed");
                    return (null, null);
                }
            }
            else
            {
                Debug.LogError("EnvironmentRaycastManager is not supported");
                return (null, null);
            }
        }
    }
