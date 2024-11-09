using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

public class SimTransform {
  public List<float> pos;
  public List<float> rot;
  public List<float> scale;

  public Vector3 GetPos() {
    return new Vector3(pos[0], pos[1], pos[2]);
  }

  public Quaternion GetRot() {
    return new Quaternion(rot[0], rot[1], rot[2], rot[3]);
  }

  public Vector3 GetScale() {
    return new Vector3(scale[0], scale[1], scale[2]);
  }

}

public class SimVisual {
  // public string name;
  public string type;
  public string mesh;
  public string material;
  public SimTransform trans;
  public List<float> color;
}


public class SimBody {
  public string name;
  public SimTransform trans;
  public List<SimVisual> visuals; 
  public List<SimBody> children;
}

public class SimScene {
  public string id;
  public SimBody root;
  public List<SimMesh> meshes;
  public List<SimMaterial> materials;
  public List<SimTexture> textures;
}

public class SimAsset {
  public string name;
}

public class SimMeshData {
  public int[] indices;
  public Vector3[] vertices;
  public Vector3[] normals;
  public Vector2[] uvs;
}

public class SimMesh : SimAsset {
  public string hash;
  public List<int> indicesLayout;

  public List<int> verticesLayout;

  public List<int> normalsLayout;

  public List<int> uvLayout;

  // [JsonIgnore]
  // public Mesh compiledMesh;

  
  // [JsonIgnore]
  // public SimMeshData rawData;
}

public class SimMaterial : SimAsset {
  public string hash;
  public List<float> color;
  public List<float> emissionColor;
  public float specular;
  public float shininess;
  public float reflectance;
  public string texture;
  public List<float> textureSize;

  // [JsonIgnore]
  // public Material compiledMaterial;
}

public class SimTexture  : SimAsset { 
  public string hash;
  public int width;

  public int height;

  public string texureType;
  
  // [JsonIgnore]
  // public byte[] textureData;

  // [JsonIgnore]
  // public Texture compiledTexture;
}