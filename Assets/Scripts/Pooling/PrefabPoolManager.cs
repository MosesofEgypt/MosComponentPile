using System.Collections.Generic;
using UnityEngine;


[System.Serializable]
public class PrefabPool {
    [SerializeField] [Tooltip("Set this to the prefab this object is attached to.")]
    GameObject prefab;

    [Tooltip("The number of pooled instances to start with.")]
    public int initialSize = 8;
    [Tooltip("The number of instances to add if the PooledCount " +
             "would go below 0. Set to 0 to disable growing.")]
    public int growAmount = 8;
    [Tooltip("The max number of objects the pool can grow to contain.")]
    public int maxSize = 128;

    Dictionary<int, PoolablePrefab> pool = new Dictionary<int, PoolablePrefab>();
    HashSet<int> pooled = new HashSet<int>();
    System.Object lawk = new System.Object();  // why is lock a keyword?

    int poolSize = 0;

    static System.UInt64 currPooledInstanceId = 1;  // 0 is reversed for meaning invalid

    public GameObject Prefab { get { return prefab; } }
    public GameObject Root { get; private set; }

    public void Initialize(GameObject prefab = null, int initialSize = -1, int growAmount = -1, int maxSize = -1) {
        if (!PrefabPoolManager.Manager) {
            Debug.LogError("PrefabPoolManager instance does not exist. Cannot create PrefabPool instance.");
            return;
        }

        if (prefab)
            this.prefab = prefab;

        PoolablePrefab poolHandle = this.prefab.GetComponent<PoolablePrefab>() as PoolablePrefab;
        if (!poolHandle)
            poolHandle = this.prefab.AddComponent<PoolablePrefab>() as PoolablePrefab;

        if (poolHandle)
            poolHandle.Prefab = this.prefab;
        else
            Debug.Log("Could not locate or add PoolablePrefab component on the supplied prefab:\n" + this.prefab);

        Root = new GameObject(Prefab.name + "(Pool)");
        Root.transform.parent = PrefabPoolManager.Manager.gameObject.transform;

        if (initialSize >= 0)
            this.initialSize = initialSize;

        if (growAmount >= 0)
            this.growAmount = growAmount;

        if (maxSize >= 0)
            this.maxSize = maxSize;

        CreateInstances(this.initialSize);
    }

    public void SanitizeAvailableGameObjects() {
        lock (lawk) {
            pooled.Clear();
            foreach (KeyValuePair<int, PoolablePrefab> pair in pool) {
                if (!pair.Value)
                    continue;
                else if (!pair.Value.gameObject.activeSelf)
                    pooled.Add(pair.Key);
            }
        }
    }

    public void CreateInstances(int count) {
        lock (lawk) {
            if (!prefab)
                return;
            if (count <= 0)
                count = 1;

            // figure out how many instances we can add before hitting the max
            count = Mathf.Min(count, maxSize - poolSize);
            if (count <= 0)
                return;

            int key;
            string name = Prefab.name + ": INST ";
            GameObject instance;
            PoolablePrefab poolHandle;
            for (int i = count; i >= 0; i--) {
                instance = Object.Instantiate(prefab);
                poolHandle = instance.GetComponent<PoolablePrefab>() as PoolablePrefab;
                poolHandle.Prefab = prefab;

                instance.name = name + pool.Count;
                instance.SetActive(false);
                instance.transform.parent   = Root.gameObject.transform;
                instance.transform.position = Root.gameObject.transform.position;
                poolHandle.CacheActiveStates();

                key = instance.GetInstanceID();
                pooled.Add(key);
                pool[key] = poolHandle;
            }

            poolSize += count;
        }
    }

    public bool StoreInstance(GameObject instance) {
        if (!instance)
            return false;

        lock (lawk) {
            int key = instance.GetInstanceID();
            if (instance == prefab)
                // NEVER store the prefab as a pooled object
                return false;
            else if (!pool.ContainsKey(key))
                // only store instances found in the pool
                return false;

            instance.SetActive(false);
            instance.transform.parent = Root.gameObject.transform;
            instance.transform.position = Root.gameObject.transform.position;
            pooled.Add(key);
        }
        return true;
    }

    public bool RemoveInstance(GameObject instance) {
        if (!instance)
            return false;

        lock (lawk) {
            int key = instance.GetInstanceID();
            pooled.Remove(key);
            pool.Remove(key);
        }
        return true;
    }

    public GameObject GetInstance(Vector3 position, bool expandPoolIfEmpty = true) {
        PoolablePrefab instanceHandle = GetInstanceHandle(position, expandPoolIfEmpty);
        if (!instanceHandle)
            return null;
        return instanceHandle.gameObject;
    }
    public PoolablePrefab GetInstanceHandle(Vector3 position, bool expandPoolIfEmpty = true) {
        lock (lawk) {
            if (pooled.Count == 0 && expandPoolIfEmpty)
                // no free instances to grab from pool. create more.
                CreateInstances(growAmount);

            if (pooled.Count == 0)
                // still no free instances. cant return one
                return null;

            int keyToUse = -1;
            bool found = false, cleanPooled = false;
            PoolablePrefab poolablePrefab = null;
            HashSet<int> keysToRemove = null;
            // find the first pooled GameObject
            foreach (int key in pooled) {
                if (pool.ContainsKey(key)) {
                    poolablePrefab = pool[key];
                    if (poolablePrefab)
                        if (!poolablePrefab.gameObject.activeSelf) {
                            found = true;
                            keyToUse = key;
                            break;
                        }
                }
                // couldnt use this pooled key for some reason, so it isnt valid to keep
                // it in the pooled HashSet. Remove it and warn that it was in there.
                if (!cleanPooled) {
                    keysToRemove = new HashSet<int>();
                    cleanPooled = true;
                }
                keysToRemove.Add(key);
            }

            if (cleanPooled) {
                Debug.LogWarning(keysToRemove.Count + " invalid keys exist in '" + Prefab.name + "' pool.");
                foreach (int key in keysToRemove)
                    // remove the invalid keys
                    pooled.Remove(key);

                if (!found && expandPoolIfEmpty) {
                    // if we didnt find any and the pool had to be cleaned, try to create more.
                    // An unclean pooled HashSet can prevent growing the pool when it runs out.
                    CreateInstances(growAmount);

                    // call this method again, but without the automatic expansion
                    return GetInstanceHandle(position);
                }
            }

            if (!found)
                return null;

            // remove its key from the hashset
            pooled.Remove(keyToUse);

            poolablePrefab.pooledInstanceId = currPooledInstanceId;
            currPooledInstanceId++;
            currPooledInstanceId = currPooledInstanceId % 0x7FffFFffFFffFFff;

            GameObject obj = poolablePrefab.gameObject;
            obj.transform.parent = null;
            obj.transform.position = position;
            obj.SetActive(true);
            return poolablePrefab;
        }
    }
}

[AddComponentMenu("Pooling/Prefab Pool Manager")]
public class PrefabPoolManager : MonoBehaviour {
    [SerializeField] [Tooltip("When created procedurally, a new PrefabPools Initial Size defaults to this.")]
    int defaultInitialSize = 16;
    [SerializeField] [Tooltip("When created procedurally, a new PrefabPools Grow Amount defaults to this.")]
    int defaultGrowAmount = 16;
    [SerializeField] [Tooltip("When created procedurally, a new PrefabPools Max Size defaults to this.")]
    int defaultMaxSize = 128;
    [SerializeField] [Tooltip("Add any prefabs to make instance pools for to this array.")]
    List<PrefabPool> prefabPools;

    Dictionary<int, int> prefabIdToIndex   = new Dictionary<int, int>();

    static int prefabPoolManagerCount = 0;
    System.Object lawk = new System.Object();  // why is lock a keyword?

    public static PrefabPoolManager Manager { get; private set; }
    public static bool IsInitialized { get { return prefabPoolManagerCount > 0; } }
    public static bool CanQuit { get; private set; }

    void OnEnable() {
        if (prefabPoolManagerCount == 0)
            Awake();
    }

    void Awake() {
        prefabPoolManagerCount++;
        if (prefabPoolManagerCount > 1)
            Debug.LogError("More than one PrefabPoolManager was created. " + prefabPoolManagerCount);
        else if (!Manager) {
            Manager = this;
            DontDestroyOnLoad(gameObject);
        }

        PrefabPool pool;
        GameObject prefab;
        int id;
        for (int i = prefabPools.Count - 1; i >= 0; i--) {
            pool = prefabPools[i];
            prefab = pool.Prefab;
            if (!prefab) {
                Debug.LogWarning("The PrefabPool at index# " + i + " does not have a prefab specified.");
                continue;
            }

            id = prefab.GetInstanceID();

            prefabPools[i].Initialize(prefab, pool.initialSize, pool.growAmount, pool.maxSize);

            if (prefabIdToIndex.ContainsKey(id))
                Debug.LogWarning("The prefab in PrefabPool index# " + i + " already " +
                                 "exists in PrefabPool index# " + prefabIdToIndex[id]);
            else
                prefabIdToIndex[id] = i;
        }
    }

    void OnApplicationQuit() {
        CanQuit = true;
    }

    void OnDestroy() {
        if (!CanQuit)
            Debug.LogError("PrefabPoolManager was destroyed!");
        prefabPoolManagerCount--;
    }

    public bool AddPool(GameObject prefab, int initialSize = -1, int growAmount = -1, int maxSize = -1) {
        if (!prefab) {
            Debug.LogError("prefab was null.");
            return false;
        }
        int prefabId = prefab.GetInstanceID();

        lock (lawk) {
            if (initialSize < 0)
                initialSize = defaultInitialSize;
            if (growAmount < 0)
                growAmount = defaultGrowAmount;
            if (maxSize < 0)
                maxSize = defaultMaxSize;

            if (prefabIdToIndex == null)
                Debug.LogError("prefabIdToIndex is null.");
            else if (prefabIdToIndex.ContainsKey(prefabId))
                Debug.LogError("Prefab with the id " + prefabId + " already exists.");
            else {
                PrefabPool newPool = new PrefabPool();

                newPool.Initialize(prefab, initialSize, growAmount, maxSize);
                prefabIdToIndex[prefabId] = prefabPools.Count;

                prefabPools.Add(newPool);
                return true;
            }
        }
        return false;
    }

    public PrefabPool GetPool(int prefabId) {
        lock (lawk) {
            if (prefabIdToIndex == null) {
                Debug.LogError("prefabIdToIndex is null.");
                return null;
            } else if (!prefabIdToIndex.ContainsKey(prefabId)) {
                Debug.LogWarning("Could not locate prefab by the id " + prefabId);
                return null;
            }
            int index = prefabIdToIndex[prefabId];
            if (index >= 0 && index < prefabPools.Count)
                return prefabPools[index];

            Debug.LogError("prefabIdToIndex mapping is corrupt.");
            return null;
        }
    }
    public PrefabPool GetPool(GameObject prefab, bool createPoolIfNoneExists = false) {
        if (!prefab) {
            Debug.LogError("prefab was null.");
            return null;
        }
        PrefabPool pool = GetPool(prefab.GetInstanceID());
        if (pool == null && createPoolIfNoneExists)
            if (AddPool(prefab))
                pool = GetPool(prefab.GetInstanceID());
        return pool;
    }

    public GameObject GetInstanceOf(int prefabId, Vector3 position) {
        PrefabPool pool = GetPool(prefabId);
        if (pool == null)
            return null;
        return pool.GetInstance(position);
    }
    public GameObject GetInstanceOf(GameObject prefab, Vector3 position, bool createPoolIfNoneExists = false) {
        PrefabPool pool = GetPool(prefab, createPoolIfNoneExists);
        if (pool == null)
            return null;
        return pool.GetInstance(position);
    }

    public PoolablePrefab GetInstanceHandleOf(int prefabId, Vector3 position) {
        PrefabPool pool = GetPool(prefabId);
        if (pool == null)
            return null;
        return pool.GetInstanceHandle(position);
    }
    public PoolablePrefab GetInstanceHandleOf(GameObject prefab, Vector3 position, bool createPoolIfNoneExists = false) {
        PrefabPool pool = GetPool(prefab, createPoolIfNoneExists);
        if (pool == null)
            return null;
        return pool.GetInstanceHandle(position);
    }

    public bool StoreInstanceOf(int prefabId, GameObject instance) {
        PrefabPool pool = GetPool(prefabId);
        if (pool == null)
            return false;
        return pool.StoreInstance(instance);
    }
    public bool StoreInstanceOf(GameObject prefab, GameObject instance) {
        PrefabPool pool = GetPool(prefab);
        if (pool == null)
            return false;
        return pool.StoreInstance(instance);
    }

    public bool StoreInstanceHandleOf(int prefabId, PoolablePrefab instanceHandle) {
        if (!instanceHandle)
            return false;
        PrefabPool pool = GetPool(prefabId);
        if (pool == null)
            return false;
        return pool.StoreInstance(instanceHandle.gameObject);
    }
    public bool StoreInstanceHandleOf(GameObject prefab, PoolablePrefab instanceHandle) {
        if (!instanceHandle)
            return false;
        PrefabPool pool = GetPool(prefab);
        if (pool == null)
            return false;
        return pool.StoreInstance(instanceHandle.gameObject);
    }

    public bool RemoveInstanceOf(int prefabId, GameObject instance) {
        PrefabPool pool = GetPool(prefabId);
        if (pool == null)
            return false;
        return pool.RemoveInstance(instance);
    }
    public bool RemoveInstanceOf(GameObject prefab, GameObject instance) {
        PrefabPool pool = GetPool(prefab);
        if (pool == null)
            return false;
        return pool.RemoveInstance(instance);
    }

    public bool RemoveInstanceHandleOf(int prefabId, PoolablePrefab instanceHandle) {
        if (!instanceHandle)
            return false;
        PrefabPool pool = GetPool(prefabId);
        if (pool == null)
            return false;
        return pool.RemoveInstance(instanceHandle.gameObject);
    }
    public bool RemoveInstanceHandleOf(GameObject prefab, PoolablePrefab instanceHandle) {
        if (!instanceHandle)
            return false;
        PrefabPool pool = GetPool(prefab);
        if (pool == null)
            return false;
        return pool.RemoveInstance(instanceHandle.gameObject);
    }
}