using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using NetMQ;

namespace IRXR.Utilities
{

	public static class UnityPortSet
	{
		public static readonly int DISCOVERY = 7720;
		public static readonly int HEARTBEAT = 7721;
		public static readonly int SERVICE = 7730;
		public static readonly int TOPIC = 7731;
	}

	public class NetAddress
	{
		public string ip { get; set; }  // IP address as a string
		public int port { get; set; }   // Port as an integer

		public NetAddress(string ip, int port)
		{
			this.ip = ip;
			this.port = port;
		}
	}


	public class NetComponentInfo
	{
		public string name { get; set; }
		public string type { get; set; }
		public string nodeID { get; set; }  // Assuming HashIdentifier is a string
		public NetAddress addr { get; set; }

		public NetComponentInfo(string name, string type, string nodeID, NetAddress addr)
		{
			this.name = name;
			this.type = type;
			this.nodeID = nodeID;
			this.addr = addr;
		}
	}


	public class NodeInfo
	{
		public string name { get; set; }
		public string nodeID { get; set; }  // Hash code represented as string
		public NetAddress addr { get; set; }
		public string type { get; set; }
		public Dictionary<string, NetComponentInfo> services { get; set; }
		public Dictionary<string, NetComponentInfo> topics { get; set; }

		public NodeInfo(string name, string nodeID, NetAddress addr, string type)
		{
			this.name = name;
			this.nodeID = nodeID;
			this.addr = addr;
			this.type = type;
			this.services = new Dictionary<string, NetComponentInfo>();
			this.topics = new Dictionary<string, NetComponentInfo>();
		}
	}


	public class NetInfo
	{
		public NodeInfo master { get; set; }
		public Dictionary<string, NetComponentInfo> topics { get; set; }
		public Dictionary<string, NetComponentInfo> services { get; set; }

		public NetInfo(NodeInfo master)
		{
			this.master = master;
			this.topics = new Dictionary<string, NetComponentInfo>();
			this.services = new Dictionary<string, NetComponentInfo>();
		}
	}

	public static class NetworkUtils
	{

		public static int GetZMQSocketPort(NetMQSocket socket)
		{
			// Retrieve the actual endpoint (e.g., "tcp://127.0.0.1:12345")
			string endpoint = socket.Options.LastEndpoint;
			// Extract the port number from the endpoint
			int port = int.Parse(endpoint.Split(':')[2]);
			return port;
		}

		public static UdpClient CreateUDPClient(IPEndPoint endPoint)
		{
			UdpClient udpClient = new UdpClient();
			udpClient.Client.Bind(endPoint);
			return udpClient;
		}

		public static UdpClient CreateUDPClient(int port)
		{
			return CreateUDPClient(new IPEndPoint(IPAddress.Any, port));
		}

		public static UdpClient CreateUDPClient(string localIP, int port)
		{
			IPAddress ipAddress = IPAddress.Parse(localIP);
			return CreateUDPClient(new IPEndPoint(ipAddress, port));
		}

		public static string GetLocalIPsInSameSubnet(string inputIPAddress)
		{
			if (inputIPAddress == "127.0.0.1")
			{
				return "127.0.0.1";
			}
			IPAddress inputIP;
			if (!IPAddress.TryParse(inputIPAddress, out inputIP))
			{
				throw new ArgumentException("Invalid IP address format.", nameof(inputIPAddress));
			}
			IPAddress subnetMask = IPAddress.Parse("255.255.255.0");
			// Get all network interfaces
			NetworkInterface[] networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
			foreach (NetworkInterface ni in networkInterfaces)
			{
				// Get IP properties of the network interface
				IPInterfaceProperties ipProperties = ni.GetIPProperties();
				UnicastIPAddressInformationCollection unicastIPAddresses = ipProperties.UnicastAddresses;
				foreach (UnicastIPAddressInformation ipInfo in unicastIPAddresses)
				{
					if (ipInfo.Address.AddressFamily == AddressFamily.InterNetwork)
					{
						IPAddress localIP = ipInfo.Address;
						// Check if the IP is in the same subnet
						if (IsInSameSubnet(inputIP, localIP, subnetMask))
						{
							return localIP.ToString();
						}
					}
				}
			}
			return null;
		}

		private static bool IsInSameSubnet(IPAddress ip1, IPAddress ip2, IPAddress subnetMask)
		{
			byte[] ip1Bytes = ip1.GetAddressBytes();
			byte[] ip2Bytes = ip2.GetAddressBytes();
			byte[] maskBytes = subnetMask.GetAddressBytes();

			for (int i = 0; i < ip1Bytes.Length; i++)
			{
				if ((ip1Bytes[i] & maskBytes[i]) != (ip2Bytes[i] & maskBytes[i]))
				{
					return false;
				}
			}
			return true;
		}

	}
}