// using NetMQ;
// using NetMQ.Sockets;
// using UnityEngine;
// using System;
// using System.Net;
// using System.Net.Sockets;
// using System.Collections.Generic;
// using System.Net.NetworkInformation;


// namespace IRXR.Utilities
// {
// 	public static class NetworkUtils
// 	{
// 		public static string CalculateBroadcastAddr(string ipAddr, string netmask = "255.255.255.0")
// 		{
// 			uint ipBin = BitConverter.ToUInt32(IPAddress.Parse(ipAddr).GetAddressBytes().Reverse().ToArray(), 0);
// 			uint netmaskBin = BitConverter.ToUInt32(IPAddress.Parse(netmask).GetAddressBytes().Reverse().ToArray(), 0);
// 			uint broadcastBin = ipBin | ~netmaskBin;

// 			byte[] broadcastBytes = BitConverter.GetBytes(broadcastBin).Reverse().ToArray();
// 			return new IPAddress(broadcastBytes).ToString();
// 		}

// 		public static Socket CreateUdpSocket()
// 		{
// 			return new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
// 		}

// 		public static string GetLocalIPsInSameSubnet(string inputIPAddress)
// 		{
// 			IPAddress inputIP;
// 			if (!IPAddress.TryParse(inputIPAddress, out inputIP))
// 			{
// 				throw new ArgumentException("Invalid IP address format.", nameof(inputIPAddress));
// 			}
// 			IPAddress subnetMask = IPAddress.Parse("255.255.255.0");
// 			// Get all network interfaces
// 			NetworkInterface[] networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
// 			foreach (NetworkInterface ni in networkInterfaces)
// 			{
// 				// Get IP properties of the network interface
// 				IPInterfaceProperties ipProperties = ni.GetIPProperties();
// 				UnicastIPAddressInformationCollection unicastIPAddresses = ipProperties.UnicastAddresses;
// 				foreach (UnicastIPAddressInformation ipInfo in unicastIPAddresses)
// 				{
// 					if (ipInfo.Address.AddressFamily == AddressFamily.InterNetwork)
// 					{
// 						IPAddress localIP = ipInfo.Address;
// 						// Check if the IP is in the same subnet
// 						if (IsInSameSubnet(inputIP, localIP, subnetMask))
// 						{
// 							return localIP.ToString(); ;
// 						}
// 					}
// 				}
// 			}
// 			return "127.0.0.1";
// 		}

// 		private static bool IsInSameSubnet(IPAddress ip1, IPAddress ip2, IPAddress subnetMask)
// 		{
// 			byte[] ip1Bytes = ip1.GetAddressBytes();
// 			byte[] ip2Bytes = ip2.GetAddressBytes();
// 			byte[] maskBytes = subnetMask.GetAddressBytes();

// 			for (int i = 0; i < ip1Bytes.Length; i++)
// 			{
// 				if ((ip1Bytes[i] & maskBytes[i]) != (ip2Bytes[i] & maskBytes[i]))
// 				{
// 					return false;
// 				}
// 			}
// 			return true;
// 		}

// 	}
// }