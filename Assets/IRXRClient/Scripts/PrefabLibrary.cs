using System.Collections.Generic;
using UnityEngine;

namespace IRXR
{
    [CreateAssetMenu(fileName = "AssetLibrary", menuName = "Custom/Asset Library")]
    public class PrefabLibrary : ScriptableObject
    {
        [System.Serializable]
        public class Entry<T>
        {
            public string key;
            public T item;
        }

        [SerializeField]
        private List<Entry<GameObject>> prefabEntries = new List<Entry<GameObject>>();
        [SerializeField]
        private List<Entry<Material>> materialEntries = new List<Entry<Material>>();

        private Dictionary<string, GameObject> prefabDictionary;
        private Dictionary<string, Material> materialDictionary;

        public void BuildDictionary()
        {
            prefabDictionary = new Dictionary<string, GameObject>();
            materialDictionary = new Dictionary<string, Material>();

            foreach (Entry<GameObject> entry in prefabEntries)
            {
                if (!prefabDictionary.ContainsKey(entry.key))
                {
                    prefabDictionary.Add(entry.key, entry.item);
                }
                else
                {
                    Debug.LogWarning("Duplicate key found in Prefab Dictionary: " + entry.key);
                }
            }

            foreach (Entry<Material> entry in materialEntries)
            {
                if (!materialDictionary.ContainsKey(entry.key))
                {
                    materialDictionary.Add(entry.key, entry.item);
                }
                else
                {
                    Debug.LogWarning("Duplicate key found in Material Dictionary: " + entry.key);
                }
            }

        }

        public GameObject InstantiateNewGameObject(string key)
        {
            if (prefabDictionary == null)
            {
                BuildDictionary();
            }

            if (prefabDictionary.ContainsKey(key))
            {
                return Instantiate(prefabDictionary[key]);
            }
            Debug.LogWarning($"Wrong Object Type: {key}");
            return null;
        }

        public Material GetMaterial(string key)
        {
            if (materialDictionary == null)
            {
                BuildDictionary();
            }

            if (materialDictionary.ContainsKey(key))
            {
                // return Instantiate(materialDictionary[key]);
                return materialDictionary[key];
            }
            return null;
        }

    }

}
