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
		public static readonly byte[] NODES = new byte[] { 0x02 };
	}

	public static class IRXRSignal
	{
		public static readonly string EMPTY = "EMPTY";
		public static readonly string SUCCESS = "SUCCESS";
		public static readonly string ERROR = "ERROR";
		public static readonly string NOSERVICE = "NOSERVICE";
		public static readonly string TIMEOUT = "TIMEOUT";
	}

	public static class MsgUtils
	{

		public const string SEPARATOR = "|";

		public static byte[][] SplitByte(byte[] bytesMsg)
		{
			int separatorIndex = Array.IndexOf(bytesMsg, Encoding.UTF8.GetBytes(SEPARATOR)[0]);
			if (separatorIndex == -1)
			{
				return new byte[][] { bytesMsg, null };
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

		public static byte[] String2Bytes(string str)
		{
			return Encoding.UTF8.GetBytes(str);
		}

		public static string Bytes2String(byte[] bytes)
		{
			return Encoding.UTF8.GetString(bytes);
		}

		public static byte[] GenerateHeartbeat(NodeInfo nodeInfo)
		{
			string serializedLocalInfo = JsonConvert.SerializeObject(nodeInfo);
			return ConcatenateByteArrays(EchoHeader.HEARTBEAT, String2Bytes(serializedLocalInfo));
		}

		public static byte[] Serialize2Byte<T>(T data)
		{
			return String2Bytes(JsonConvert.SerializeObject(data));
		}

		public static T StringDeserialize2Object<T>(string jsonString)
		{
			return JsonConvert.DeserializeObject<T>(jsonString);
		}

		public static T BytesDeserialize2Object<T>(byte[] byteMessage)
		{
			string jsonString = Encoding.UTF8.GetString(byteMessage);
			// Debug.Log(jsonString);
			return JsonConvert.DeserializeObject<T>(jsonString);
		}

		public static byte[] ObjectSerialize2Bytes<T>(T data)
		{
			return String2Bytes(JsonConvert.SerializeObject(data));
		}

		public static string CombineHeaderWithMessage(string header, string message)
		{
			return $"{header}{SEPARATOR}{message}";
		}

		public static byte[] CombineHeaderWithMessage(string header, byte[] message)
		{
			return ConcatenateByteArrays(String2Bytes(header), Encoding.UTF8.GetBytes(SEPARATOR), message);
		}

	}
}