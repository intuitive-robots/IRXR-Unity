using UnityEngine;
using System.Collections.Generic;

namespace IRXR
{    
    [System.Serializable]
    public class IRXRMsg
    {
    public string header;
        public IRXRData data;
    }

    [System.Serializable]
    public class IRXRData : Dictionary<string, IRXRDataCell>
    {
        
    }

    [System.Serializable]
    public class IRXRDataCell : Dictionary<string, object>
    {
        public T GetValueFromKey<T>(string key)
        {
            return (T)this[key];
        }
    }
}





