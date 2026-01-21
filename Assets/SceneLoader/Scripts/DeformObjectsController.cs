using IRXR.Node;
using IRXR.SceneLoader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

[RequireComponent(typeof(SimLoader))]
public class DeformObjectsController : MonoBehaviour
{
    public Dictionary<string, List<MeshFilter>> _objectsMeshes;
    private Subscriber<byte[]> _subscriber;

    void Start()
    {
        gameObject.GetComponent<SimLoader>().OnSceneLoaded += StartSubscription;
        gameObject.GetComponent<SimLoader>().OnSceneCleared += StopSubscription;
        _subscriber = new Subscriber<byte[]>("DeformUpdate", SubscribeCallback);
    }

    public void StartSubscription()
    {
        _objectsMeshes = gameObject.GetComponent<SimLoader>().GetObjectMeshes();
        Debug.Log("Start Update Deform");
        _subscriber.StartSubscription();
    }

    public void StopSubscription()
    {
        _subscriber.Unsubscribe();
    }

    public void SubscribeCallback(byte[] streamMsg)
    {
        // Decode update message
        // Structure:
        // 
        // L: Length of update string containing all deform meshes [ 4 bytes ]
        // S: Update string, semicolon seperated list of prims contained in thisupdate [ ? bytes ]
        // N: Number of verticies for each mesh in update string [ num_meshes x 4 bytes]
        // V: Verticies for each mesh [ ? bytes for each mesh ]
        // 
        //       | L | S ... S | N ... N | V ... V |
        //

        ReadOnlySpan<byte> msg = new ReadOnlySpan<byte>(streamMsg);
        Int32 updateListEndPos = BitConverter.ToInt32(msg.Slice(0, sizeof(Int32))); // L

        if (updateListEndPos == 0)
            return;

        string updateListContents = Encoding.UTF8.GetString(streamMsg.Skip(sizeof(Int32)).Take(updateListEndPos).ToArray()); // S

        if (updateListContents[updateListContents.Length - 1] == ';')
            updateListContents = updateListContents.Remove(updateListContents.Length - 1);

        string[] updateList = updateListContents.Split(';');

        Int32[] meshVertSizes = MemoryMarshal.Cast<byte, Int32>(msg.Slice(updateListEndPos + sizeof(Int32), updateList.Length * sizeof(Int32))).ToArray(); // N

        int currentPos = updateListEndPos + (updateList.Length + 1) * sizeof(Int32);
        for (int i = 0; i < updateList.Length; i++)
        {
            Vector3[] updatedVerticies = MemoryMarshal.Cast<byte, Vector3>(msg.Slice(currentPos, meshVertSizes[i] * sizeof(float))).ToArray(); // V
            List<MeshFilter> meshFilters;
            if (_objectsMeshes.TryGetValue(updateList[i], out meshFilters))
            {
                if (meshFilters.Count > 0)
                {
                    // first entry should be complete mesh, even if sub components exist
                    MeshFilter meshFilter = meshFilters.First();
                    meshFilter.mesh.vertices = updatedVerticies;
                    meshFilter.mesh.RecalculateNormals();
                }
            }
            currentPos += meshVertSizes[i] * sizeof(float);
        }
    }
}