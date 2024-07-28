using System;
using UnityEngine;
using NetMQ;
using NetMQ.Sockets;
using System.Collections.Generic;

[RequireComponent(typeof(IRXRNetManager))]
public class StreamPublisher : MonoBehaviour {

  private PublisherSocket _pubSocket;
  private IRXRNetManager _netManager;
  [SerializeField] private string port;
  [SerializeField] private string topic;

  void Start() {
    _netManager = gameObject.GetComponent<IRXRNetManager>();
    _pubSocket = _netManager.CreatePublisherSocket();
    _pubSocket.Bind($"tcp://*:{port}");
  }

}