using System;
using UnityEngine;
using NetMQ;
using NetMQ.Sockets;
using System.Collections.Generic;

public class StreamingPublisher : MonoBehaviour {

  private PublisherSocket _pubSocket;
  private IRXRNetManager _netManager;
  [SerializeField] private string port;

  void Awake() {
    _pubSocket = new PublisherSocket();
    _pubSocket.Bind($"tcp://*:port");
  }

  void Start() {

  }

}