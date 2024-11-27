using UnityEngine;


public class Streamer : MonoBehaviour {

  protected string _topic;
  protected Publisher _publisher;

  void Start() {
    SetupTopic();
    _publisher = new Publisher(_topic);
    Initialize();
  }

  protected virtual void SetupTopic() {}

  protected virtual void Initialize() {}

}