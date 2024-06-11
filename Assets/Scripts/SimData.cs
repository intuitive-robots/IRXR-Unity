using System.Collections.Generic;

public class SimTransform {
  public List<float> Position;
  public List<float> Rotation;
  public List<float> Scale;
}

public class SimVisual {
  public string Name;
  public string Type;
  public string Mesh;
  public string Material;
  public SimTransform Transform;
  public List<float> Color;
}


public class SimBody {
  public string Name;
  public List<SimVisual> Visuals; 

  public List<SimJoint> Joints;

  public List<SimBody> Bodies;
}


public class SimJoint {
  public string Type;
  public string Name;
  public float Initial;
  public float Maxrot;
  public float Minrot;
  public List<float> Axis;
  public SimTransform Transform;
}

public class SimScene {
  public string Id;
  
  public SimBody WorldBody;

  public List<SimMesh> Meshes;

  public List<SimMaterial> Materials;

  public List<SimTexture> Textures;
}