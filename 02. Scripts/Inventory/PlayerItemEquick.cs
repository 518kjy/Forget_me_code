using UnityEngine;

public class PlayerItemEquick : MonoBehaviour
{
    public PhotonView pv;
    public Transform rightHand;

    public GameObject equippedInstance;   // 씬 인스턴스
    public GameObject equippedPrefab;     // 장착 프리팹(에셋 레퍼런스)

    /// <summary>
    /// 같은 프리팹이면 토글(해제), 다르면 교체
    /// </summary>
    public GameObject Equip(GameObject prefab)
    {
        if (!rightHand || !prefab) return null;

        // 1) 로컬 처리
        if (equippedInstance == null)
        {
            var go = EquipNew(prefab, prefab.name);
            // 2) 원격 동기화(본인 제외)
            RequestEquip(prefab.name);
            return go;
        }
        else if (equippedPrefab == prefab) // 프리팹 에셋으로 비교
        {
            Unequip();
            RequestUnequip(); // 원격도 해제
            return null;
        }
        else
        {
            Unequip();
            var go = EquipNew(prefab, prefab.name);
            RequestEquip(prefab.name);
            return go;
        }
    }

    GameObject EquipNew(GameObject prefab, string key)
    {
        var go = Instantiate(prefab, rightHand);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        equippedInstance = go;
        equippedPrefab  = prefab;
        return go;
    }

    public void Unequip()
    {
        if (equippedInstance) Destroy(equippedInstance);
        equippedInstance = null;
        equippedPrefab   = null;
    }

    // === 네트워크 브로드캐스트(소유자만) ===
    void RequestEquip(string key)
    {
        Debug.Log(key);
        
        if (!pv || !pv.isMine) return;
        pv.RPC(nameof(RpcEquipByKey), PhotonTargets.OthersBuffered, key); // OthersBuffered
    }

    void RequestUnequip()
    {
        if (!pv || !pv.isMine) return;
        pv.RPC(nameof(RpcUnequip), PhotonTargets.OthersBuffered);
    }

    // === 수신측: 로컬 적용만 ===
    [PunRPC]
    void RpcEquipByKey(string key, PhotonMessageInfo info)
    {
        var prefab = Resources.Load<GameObject>($"ItemPrefabs/{key}"); // 규칙: Resources/<key>.prefab
        if (!rightHand || !prefab)
        {
            Debug.LogWarning($"[RpcEquipByKey] 키 못 찾음 : {key}");
            return;
        }

        if (equippedPrefab == prefab) // 에셋 비교
        {
            // 이미 같은 거면 무시(혹은 토글을 원하면 아래처럼 해제)
            // Unequip();
            return;
        }

        Unequip();
        EquipNew(prefab, key);
    }

    [PunRPC]
    void RpcUnequip(PhotonMessageInfo info)
    {
        Unequip();
    }
}
