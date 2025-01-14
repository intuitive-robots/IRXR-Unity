using UnityEngine;
using NetMQ;
using NetMQ.Sockets;
using System;
using System.Runtime.InteropServices;
using IRXR.Node;


public class PointCloudLoader : MonoBehaviour
{
    private ParticleSystem _particleSystem = null;
    private ParticleSystem.Particle[] voxels;
    private Subscriber<byte[]> _pointCloudSubscriber;

    private void Start()
    {
        _particleSystem = GetComponent<ParticleSystem>();
        _pointCloudSubscriber = new Subscriber<byte[]>("PointCloud", UpdatePointCloud);
        IRXRNetManager.Instance.OnConnectionStart += _pointCloudSubscriber.StartSubscription;
        Debug.Log("Connected to the server");
    }

    public static float[] ByteArrayToFloatArray(byte[] byteArray)
    {
        if (byteArray == null || byteArray.Length % 4 != 0)
            throw new ArgumentException("Invalid byte array length. Must be a multiple of 4.");

        // Cast the byte array to a ReadOnlySpan<float>
        ReadOnlySpan<byte> byteSpan = byteArray;
        ReadOnlySpan<float> floatSpan = MemoryMarshal.Cast<byte, float>(byteSpan);

        // Convert the ReadOnlySpan<float> to a float[]
        return floatSpan.ToArray();
    }

    private void UpdatePointCloud(byte[] pointCloudMsg)
    {
        Debug.Log("Received point cloud data");
        // Convert the byte array to a string
        float[] pointCloud = ByteArrayToFloatArray(pointCloudMsg);
        if (pointCloud.Length % 6 != 0)
        {
            Debug.LogError("Invalid point cloud data");
            return;
        }
        int pointNum = pointCloud.Length / 6;
        // Convert the data to the format that Unity's Particle System can use
        if (voxels == null || voxels.Length != pointNum)
        {
            voxels = new ParticleSystem.Particle[pointNum];
            
        }
        Debug.Log("pointNum: " + pointNum);
        for (int i = 0; i < pointNum; i++)
        {
            voxels[i].position = new Vector3(pointCloud[i * 6], pointCloud[i * 6 + 1], pointCloud[i * 6 + 2]);
            voxels[i].startColor = new Color(pointCloud[i * 6 + 3], pointCloud[i * 6 + 4], pointCloud[i * 6 + 5]);
            voxels[i].startSize = 0.01f;
        }
        Debug.Log("Set particles");
        _particleSystem.SetParticles(voxels);
    }

}