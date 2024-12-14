using UnityEngine;
using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using IRXR.Node;

namespace IRXR.Utilities
{
	public static class EchoHeader
	{
		public static readonly byte[] PING = new byte[] { 0x00 };
		public static readonly byte[] HEARTBEAT = new byte[] { 0x01 };
	}

	public static class MSG
	{
		public static readonly byte[] SERVICE_ERROR = new byte[] { 0x03 };
		public static readonly byte[] SERVICE_TIMEOUT = new byte[] { 0x04 };
	}


	public static class MsgUtils
	{

		public const string SEPARATOR = "|";

		public static byte[][] SplitByte(byte[] bytesMsg)
		{
			int separatorIndex = Array.IndexOf(bytesMsg, Encoding.UTF8.GetBytes(SEPARATOR)[0]);
			if (separatorIndex == -1)
			{
				return new byte[][] { bytesMsg, Array.Empty<byte>() };
			}

			byte[] part1 = bytesMsg[..separatorIndex];
			byte[] part2 = bytesMsg[(separatorIndex + 1)..];
			return new byte[][] { part1, part2 };
		}

		public static string[] SplitByteToStr(byte[] bytesMsg)
		{
			byte[][] parts = SplitByte(bytesMsg);
			return new string[] { Encoding.UTF8.GetString(parts[0]), Encoding.UTF8.GetString(parts[1]) };
		}

		public static byte[] ConcatenateByteArrays(params byte[][] arrays)
		{
			using (MemoryStream ms = new MemoryStream())
			{
				foreach (byte[] array in arrays)
				{
					if (array != null)
					{
						ms.Write(array, 0, array.Length);
					}
				}
				return ms.ToArray();
			}
		}

		public static byte[] GenerateNodeMsg(NodeInfo nodeInfo)
		{
			string nodeId = nodeInfo.nodeID; // Assuming NodeInfo has a property "NodeID"
			string serializedInfo = JsonConvert.SerializeObject(nodeInfo);

			string combinedMessage = $"{nodeId}{SEPARATOR}{serializedInfo}";
			return ConcatenateByteArrays(EchoHeader.HEARTBEAT, Encoding.UTF8.GetBytes(combinedMessage));
		}

		public static byte[] Serialize2Byte<T>(T data)
		{
			string json = JsonConvert.SerializeObject(data);
			return Encoding.UTF8.GetBytes(json);
		}

		public static T Deserialize2Object<T>(byte[] byteMessage)
		{
			string jsonString = Encoding.UTF8.GetString(byteMessage);
			Debug.Log(jsonString);
			return JsonConvert.DeserializeObject<T>(jsonString);
		}

		public static string CombineHeaderWithMessage(string header, string message)
		{
			return $"{header}{SEPARATOR}{message}";
		}

	}
}