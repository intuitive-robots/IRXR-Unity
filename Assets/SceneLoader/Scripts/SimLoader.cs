using System;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using System.Threading.Tasks;
using IRXR.Node;
using IRXR.Utilities;

namespace IRXR.SceneLoader
{

    public class SimLoader : MonoBehaviour
    {

        private object updateActionLock = new();
        private Action updateAction;
        public Action OnSceneLoaded;
        public Action OnSceneCleared;
        private IRXRNetManager _netManager;
        private GameObject _simSceneObj;
        private SimScene _simScene;
        private Dictionary<string, Transform> _simObjTrans = new();
        private Dictionary<string, List<Tuple<SimMesh, MeshFilter>>> _pendingMesh = new();
        private Dictionary<string, List<Tuple<SimTexture, Material>>> _pendingTexture = new();
        // Services
        private Service<SimScene, IRXRSignal> loadSimSceneService;

        void Start()
        {
            _netManager = IRXRNetManager.Instance;
            updateAction = () => { };
            OnSceneLoaded += () => Debug.Log("Scene Loaded");
            OnSceneCleared += () => Debug.Log("Scene Cleared");
            loadSimSceneService = new Service<SimScene, IRXRSignal>("LoadSimScene", LoadSimScene, true);
        }

        private IRXRSignal LoadSimScene(SimScene simScene)
        {
            ClearScene();
            _simScene = simScene;
            updateAction += BuildScene;
            Debug.Log("Downloaded scene json and starting to build scene");
            return new IRXRSignal(IRXRSignal.SUCCESS);
        }

        void BuildScene()
        {
            // Don't include System.Diagnostics, Debug becomes ambiguous
            // It is more accurate to use System.Diagnostics.Stopwatch, theoretically
            var local_watch = new System.Diagnostics.Stopwatch();
            local_watch.Start();
            // Debug.Log("Start Building Scene");
            _simSceneObj = CreateObject(gameObject.transform, _simScene.root);
            local_watch.Stop();
            Debug.Log($"Building Scene in {local_watch.ElapsedMilliseconds} ms");
            Task.Run(() => DownloadAssets());
            OnSceneLoaded.Invoke();
        }

        public void DownloadAssets()
        {
            var local_watch = new System.Diagnostics.Stopwatch();
            local_watch.Start();
            int totalMeshSize = 0;
            int totalTextureSize = 0;
            foreach (string hash in _pendingMesh.Keys)
            {
                byte[] meshData = _netManager.CallBytesService("Asset", hash);
                foreach (var item in _pendingMesh[hash])
                {
                    var (simMesh, meshFilter) = item;
                    RunOnMainThread(() => BuildMesh(meshData, simMesh, meshFilter));
                }
                totalMeshSize += meshData.Length;
            }
            foreach (string hash in _pendingTexture.Keys)
            {
                byte[] texData = _netManager.CallBytesService("Asset", hash);
                foreach (var item in _pendingTexture[hash])
                {
                    var (simTex, material) = item;
                    RunOnMainThread(() => BuildTexture(texData, simTex, material));
                }
                totalTextureSize += texData.Length;
            }

            double meshSizeMB = Math.Round(totalMeshSize / Math.Pow(2, 20), 2);
            double textureSizeMB = Math.Round(totalTextureSize / Math.Pow(2, 20), 2);

            local_watch.Stop();
            _pendingMesh.Clear();
            _pendingTexture.Clear();

            // When debug run in the sub thread, it will not send the log to the server
            RunOnMainThread(() => Debug.Log($"Downloaded {meshSizeMB}MB meshes, {textureSizeMB}MB textures."));
            RunOnMainThread(() => Debug.Log($"Downloaded Asset in {local_watch.ElapsedMilliseconds} ms"));
        }

        void RunOnMainThread(Action action)
        {
            lock (updateActionLock)
            {
                updateAction += action;
            }
        }

        void Update()
        {
            lock (updateActionLock)
            {
                updateAction.Invoke();
                updateAction = () => { };
            }
        }

        void ApplyTransform(Transform uTransform, SimTransform simTrans)
        {
            uTransform.localPosition = simTrans.GetPos();
            uTransform.localRotation = simTrans.GetRot();
            uTransform.localScale = simTrans.GetScale();
        }

        GameObject CreateObject(Transform root, SimBody body)
        {

            GameObject bodyRoot = new GameObject(body.name);
            bodyRoot.transform.SetParent(root, false);
            ApplyTransform(bodyRoot.transform, body.trans);
            if (body.visuals.Count != 0)
            {
                GameObject VisualContainer = new GameObject($"{body.name}_Visuals");
                VisualContainer.transform.SetParent(bodyRoot.transform, false);
                foreach (SimVisual visual in body.visuals)
                {
                    GameObject visualObj;
                    switch (visual.type)
                    {
                        case "MESH":
                            {
                                SimMesh simMesh = visual.mesh;
                                visualObj = new GameObject(simMesh.hash, typeof(MeshFilter), typeof(MeshRenderer));
                                if (!_pendingMesh.ContainsKey(simMesh.hash))
                                {
                                    _pendingMesh[simMesh.hash] = new List<Tuple<SimMesh, MeshFilter>>();
                                }
                                _pendingMesh[simMesh.hash].Add(new(simMesh, visualObj.GetComponent<MeshFilter>()));
                                break;
                            }
                        case "CUBE":
                            visualObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                            break;
                        case "PLANE":
                            visualObj = GameObject.CreatePrimitive(PrimitiveType.Plane);
                            break;
                        case "CYLINDER":
                            visualObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                            break;
                        case "CAPSULE":
                            visualObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                            break;
                        case "SPHERE":
                            visualObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                            break;
                        default:
                            Debug.LogWarning("Invalid visual, " + visual.type + body.name);
                            visualObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                            break;
                    }
                    Renderer renderer = visualObj.GetComponent<Renderer>();
                    if (visual.material != null)
                    {
                        renderer.material = BuildMaterial(visual.material, body.name);
                    }
                    else
                    {
                        Debug.LogWarning($"Material of {body.name}_Visuals not found");
                    }
                    visualObj.transform.SetParent(VisualContainer.transform, false);
                    ApplyTransform(visualObj.transform, visual.trans);
                }
            }
            body.children.ForEach(body => CreateObject(bodyRoot.transform, body));
            if (_simObjTrans.ContainsKey(body.name))
            {
                Debug.LogWarning($"Duplicate object name found: {body.name}");
                _simObjTrans.Remove(body.name);
            }
            _simObjTrans.Add(body.name, bodyRoot.transform);
            return bodyRoot;
        }

        void ClearScene()
        {
            OnSceneCleared.Invoke();
            if (_simSceneObj != null) Destroy(_simSceneObj);
            _pendingMesh.Clear();
            _pendingTexture.Clear();
            _simObjTrans.Clear();
            Debug.Log("Scene Cleared");
        }

        public Material BuildMaterial(SimMaterial simMat, string objName)
        {
            Material mat = new Material(Shader.Find("Standard"));
            if (simMat.color.Count == 3)
            {
                simMat.color.Add(1.0f);
            }
            else if (simMat.color.Count != 4)
            {
                Debug.LogWarning($"Invalid color for {objName}");
                simMat.emissionColor = new List<float> { 1.0f, 1.0f, 1.0f, 1.0f };
            }
            // Transparency
            if (simMat.color[3] < 1)
            {
                mat.SetFloat("_Mode", 2);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = 3000;
            }
            mat.SetColor("_Color", new Color(simMat.color[0], simMat.color[1], simMat.color[2], simMat.color[3]));
            if (simMat.emissionColor != null)
            {
                mat.SetColor("_emissionColor", new Color(simMat.emissionColor[0], simMat.emissionColor[1], simMat.emissionColor[2], simMat.emissionColor[3]));
            }
            mat.SetFloat("_specularHighlights", simMat.specular);
            mat.SetFloat("_Smoothness", simMat.shininess);
            mat.SetFloat("_GlossyReflections", simMat.reflectance);
            if (simMat.texture != null)
            {
                // Debug.Log($"Texture found for {objName}");
                SimTexture simTex = simMat.texture;
                if (!_pendingTexture.ContainsKey(simTex.hash))
                {
                    _pendingTexture[simTex.hash] = new();
                }
                _pendingTexture[simTex.hash].Add(new(simTex, mat));
            }
            return mat;
        }

        public static T[] DecodeArray<T>(byte[] data, int start, int length) where T : struct
        {
            return MemoryMarshal.Cast<byte, T>(new ReadOnlySpan<byte>(data, start, length)).ToArray();
        }

        public void BuildMesh(byte[] meshData, SimMesh simMesh, MeshFilter meshFilter)
        {
            meshFilter.mesh = new Mesh
            {
                vertices = DecodeArray<Vector3>(meshData, simMesh.verticesLayout[0], simMesh.verticesLayout[1]),
                normals = DecodeArray<Vector3>(meshData, simMesh.normalsLayout[0], simMesh.normalsLayout[1]),
                triangles = DecodeArray<int>(meshData, simMesh.indicesLayout[0], simMesh.indicesLayout[1]),
                uv = DecodeArray<Vector2>(meshData, simMesh.uvLayout[0], simMesh.uvLayout[1])
            };
        }

        public void BuildTexture(byte[] texData, SimTexture simTex, Material material)
        {
            Texture2D tex = new Texture2D(simTex.width, simTex.height, TextureFormat.RGB24, false);
            tex.LoadRawTextureData(texData);
            tex.Apply();
            material.mainTexture = tex;
            material.mainTextureScale = new Vector2(simTex.textureScale[0], simTex.textureScale[1]);
        }

        public Dictionary<string, Transform> GetObjectsTrans()
        {
            return _simObjTrans;
        }

        public GameObject GetSimObject()
        {
            return _simSceneObj;
        }

    }
}