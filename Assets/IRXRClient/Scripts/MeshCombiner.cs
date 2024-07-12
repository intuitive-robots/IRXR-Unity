using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// src: https://docs.unity3d.com/ScriptReference/Mesh.CombineMeshes.html 

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class MeshCombiner : MonoBehaviour
{
    public Mesh combinedMesh;

    private void Start()
    {
        //set default box mesh at start
        MeshCollider mc = GetComponentInParent<MeshCollider>();
        mc.sharedMesh = combinedMesh;
    }
    public void Run()
    {
        //combine meshes of children in combinedMesh and then sets parent mesh collider to this mesh 
        MeshFilter[] meshFilters = GetComponentsInChildren<MeshFilter>();
        CombineInstance[] combine = new CombineInstance[meshFilters.Length];

        int i = 0;
        while (i < meshFilters.Length)
        {
            combine[i].mesh = meshFilters[i].sharedMesh;
            combine[i].transform = meshFilters[i].transform.localToWorldMatrix;
            meshFilters[i].gameObject.SetActive(false);

            i++;
        }

        combinedMesh = new Mesh();
        combinedMesh.CombineMeshes(combine);
        transform.GetComponent<MeshFilter>().sharedMesh = combinedMesh;
        transform.gameObject.SetActive(true);

        MeshCollider mc = GetComponentInParent<MeshCollider>();
        mc.sharedMesh = combinedMesh;
    }
}
