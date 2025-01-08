using UnityEngine;
using NetMQ;
using NetMQ.Sockets;
using System;
using System.Runtime.InteropServices;
using UnityEngine.UIElements;


public class PointCloudTest : MonoBehaviour
{
    private ParticleSystem _particleSystem = null;
    private ParticleSystem.Particle[] voxels;
    private SubscriberSocket _subSocket;

    private void Awake()
    {
        AsyncIO.ForceDotNet.Force();
    }

    private void Start()
    {
        _particleSystem = GetComponent<ParticleSystem>();
        _subSocket = new SubscriberSocket();
        _subSocket.Connect("tcp://127.0.0.1:5556");
        _subSocket.Subscribe("");
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
    public void Update()
    {
        if (_subSocket.HasIn)
        {
            Debug.Log("Received point cloud data");
            UpdatePointCloud(_subSocket.ReceiveFrameBytes());
        }
        while (_subSocket.HasIn) _subSocket.SkipFrame();
    }

    private void UpdatePointCloud(byte[] pointCloudMsg)
    {
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


    private void OnApplicationQuit()
    {
        _subSocket.Close();
        NetMQConfig.Cleanup();
    }

}