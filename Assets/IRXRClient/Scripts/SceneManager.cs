/*
 * SFObjectManager.cs
 * For spawning object from initialization message
 * Containing the prefabs of robots and objects
 */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;

namespace IRXR
{
    public class SceneManager : IRXRSingleton<SceneManager> 
    {

        private IRXRClient _Client;
        private Dictionary<string, PrefabLibrary> _PrefabLibDict;
        public event Action<bool> OnSetRigidObjectInteractable;
        
        
        // Prefabs for robots
        // [SerializeField] public AssetLibrary sfPrefabLibrary;

        void Start()
        {
            _Client = IRXRClient.Instance;
            Action<IRXRData> initialiseAction = InitialiseScene;
            _Client.SubscribeCallback("initial_param", initialiseAction);
        }

        private void Update() {

        }

        public void RegisterPrefabLib(string libName, PrefabLibrary lib)
        {
            _PrefabLibDict[libName] = lib;
        }

        private void InitialiseScene(IRXRData data)
        {
            Debug.Log("Start Scene Initialization");
            foreach (KeyValuePair<string, IRXRDataCell> kvp in data)
            {
                string key = kvp.Key;
                IRXRDataCell value = kvp.Value;
                SpawnObject(kvp.Key, kvp.Value);
            }
            // OnSceneInitialization?.Invoke(msg.Data);
            Debug.Log("Finishing Scene Initialization");
        }

        private void SpawnObject(string name, IRXRDataCell objInitialMsg){
            Debug.LogFormat($"Spawning {name}");
            PrefabLibrary lib;
            if (_PrefabLibDict.TryGetValue(objInitialMsg.GetValueFromKey<string>("source"), out lib))
            {
                GameObject newObj;
                newObj = lib.InstantiateNewGameObject(objInitialMsg.GetValueFromKey<string>("source"));
                newObj.name = name;
                newObj.transform.parent = transform;
            }
        }

        // // Set all rigid objects' manipulable attribute
        // public void SetAllRigidObjectInteractable(bool flag){
        //     RigidObjectController[] rigidObjects;
        //     rigidObjects = gameObject.GetComponentsInChildren<RigidObjectController>();
        //     foreach (RigidObjectController item in rigidObjects)
        //     {
        //         item.SetInteractable(flag);
        //     }
        // }

        // public void SetEndEffectorRecorder(bool flag){
        //     EndEffectorRecorder[] recorders;
        //     recorders = gameObject.GetComponentsInChildren<EndEffectorRecorder>();
        //     foreach (EndEffectorRecorder item in recorders)
        //     {
        //         item.enabled = flag;
        //     }
        // }

        public void ClearAllObjects(){
            if (transform.childCount == 0)
            {
                return;
            }
            foreach (Transform child in transform)
            {
                Destroy(child.gameObject);
            }
        }
        
    }

}

