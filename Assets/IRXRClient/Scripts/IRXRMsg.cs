using UnityEngine;
using System.Collections.Generic;

namespace IRXR
{    

    public class IRXRMsgPack
    {
        static string HEADER_SEPERATOR = ":::";
        public string header;
        public string msg;
        public IRXRMsgPack(string package)
        {
            string[] split = package.Split(HEADER_SEPERATOR);
            Debug.Log(package);
            if (split.Length != 2) 
            {
                Debug.LogWarning($"Invalid message formatting {package}, this message will be ignored");
                return;
            }            
            this.header = split[0];
            this.msg = split[1];
        }

        public IRXRMsgPack(string header, string msg)
        {
            this.header = header;
            this.msg = msg;
        }
    }

    // [System.Serializable]
    // public class IRXRData : Dictionary<string, IRXRDataCell>
    // {
        
    // }

    // [System.Serializable]
    // public class IRXRDataCell : Dictionary<string, object>
    // {
    //     public T GetValueFromKey<T>(string key)
    //     {
    //         return (T)this[key];
    //     }
    // }
}





