using UnityEngine;
using Newtonsoft.Json;
using System.Collections.Generic;


class PointCloudData {
    public float[] positions;
    public float[] colors;
}

[RequireComponent(typeof(ParticleSystem))]
public class PointCloudLoader : MonoBehaviour
{
    private ParticleSystem _particleSystem = null;
    private ParticleSystem.Particle[] voxels;
    bool voxelsUpdated = false;
    private IRXRNetManager _netManager;
    private Subscriber<PointCloudData> _pointCloudUpdateSubscriber;

    // Start is called before the first frame update
    void Start()
    {
        _particleSystem = GetComponent<ParticleSystem>();
        _netManager = IRXRNetManager.Instance;
        _netManager.RegisterServiceCallback("CreatePointCloud", CreatePointCloud);
        _pointCloudUpdateSubscriber = new Subscriber<PointCloudData>("PointCloudUpdate", UpdatePointCloud);
    }

    private void UpdatePointCloud(PointCloudData pointCloudData)
    {
        if (voxels == null)
        {
            return;
        }
        // Convert the data to the format that Unity's Particle System can use
        Vector3[] positions = new Vector3[pointCloudData.positions.Length / 3];
        Color[] colors = new Color[pointCloudData.colors.Length / 4];
        float[] positionArray = pointCloudData.positions;
        float[] colorArray = pointCloudData.colors;
        for (int i = 0; i < positions.Length; i++)
        {
            positions[i] = new Vector3(positionArray[i * 3], positionArray[i * 3 + 1], positionArray[i * 3 + 2]);
        }
        for (int i = 0; i < colors.Length; i++)
        {
            colors[i] = new Color(colorArray[i * 4], colorArray[i * 4 + 1], colorArray[i * 4 + 2], colorArray[i * 4 + 3]);
        }
        SetVoxels(positions, colors);
    }

    private string CreatePointCloud(string numPointsStr)
    {
        voxels = new ParticleSystem.Particle[int.Parse(numPointsStr)];
        return "Created Point Cloud";
    }

    public void SetVoxels(Vector3[] positions, Color[] colors)
    {
        if (voxels == null)
        {
            return;
        }
        for (int i = 0; i < positions.Length; i++)
        {
            voxels[i].position = positions[i];
            voxels[i].startColor = colors[i];
            voxels[i].startSize = 0.01f;
        }
        _particleSystem.SetParticles(voxels, voxels.Length);
    }

}
