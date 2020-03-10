using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("Pooling/Poolable Prefab")]
public class PoolablePrefab : MonoBehaviour {
    [System.Serializable]
    class InitialGOState {
        public Transform parentTransform;
        public Transform transform;

        public bool active;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
    }

    [SerializeField]
    private GameObject prefab;

    public System.UInt64 pooledInstanceId = 0;

    List<InitialGOState> initialGOStates;

    void Awake() {
        CacheActiveStates();
    }

    public bool CacheActiveStates() {
        if (initialGOStates != null)
            return false;

        initialGOStates = new List<InitialGOState>();

        Stack<Transform> transforms = new Stack<Transform>();
        transforms.Push(transform);
        while (transforms.Count > 0) {
            Transform currTrans = transforms.Pop();
            InitialGOState state = new InitialGOState {
                parentTransform = currTrans.parent, transform = currTrans,
                active = currTrans.gameObject.activeSelf,
                position = currTrans.transform.position,
                rotation = currTrans.transform.rotation,
                scale = currTrans.transform.localScale,
            };

            initialGOStates.Add(state);
            foreach (Transform child in currTrans)
                transforms.Push(child);
        }

        return true;
    }

    public void ResetActiveStates() {
        foreach (var state in initialGOStates) {
            state.transform.gameObject.SetActive(state.active);
            state.transform.parent = state.parentTransform;
            state.transform.SetPositionAndRotation(state.position, state.rotation);
            state.transform.localScale = state.scale;
        }
    }

    public GameObject Prefab {
        get { return prefab; }
        set { if (!prefab || prefab == gameObject) prefab = value; }
    }

    void OnDestroy() {
        if (!PrefabPoolManager.CanQuit)
            RemoveFromPool();
    }

    public bool ReturnToPool() {
        if (!prefab)
            Debug.Log("Prefab is not set. Cannot return to PrefabPoolManager.");
        else if (!PrefabPoolManager.Manager)
            Debug.Log("No PrefabPoolManager instance to return to.");
        else if (PrefabPoolManager.Manager.StoreInstanceOf(prefab, gameObject)) {
            pooledInstanceId = 0;
            ResetActiveStates();
            return true;
        }

        return false;
    }

    public bool RemoveFromPool() {
        if (!prefab)
            Debug.Log("Prefab is not set. Cannot remove from PrefabPoolManager.");
        else if (!PrefabPoolManager.Manager)
            Debug.Log("No PrefabPoolManager instance to remove from.");
        else if (PrefabPoolManager.Manager.RemoveInstanceOf(prefab, gameObject)) {
            pooledInstanceId = 0;
            return true;
        }

        return false;
    }
}
