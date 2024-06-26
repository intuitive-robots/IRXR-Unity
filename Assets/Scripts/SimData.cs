using System.Collections.Generic;
using UnityEngine;

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

  public SimJoint joint;

  public List<SimBody> children;
}

public class SimScene {
  public string id;
  
  public SimBody root;

  public List<SimMesh> meshes;

  public List<SimMaterial> materials;

  public List<SimTexture> textures;
}