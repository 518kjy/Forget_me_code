using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "ItemEffects/Equip")]
public class EquipEffectAsset : ItemEffectAsset
{
    [Header("장착할 프리팹(손에 붙일 것)")]
    public GameObject equipPrefab;

    public override bool Execute(ItemEffectContext ctx)
    {
        var equip = ctx.user ? ctx.user.GetComponent<PlayerItemEquick>() : null;
        if (!equip) { Debug.LogWarning("[EquipEffect] PlayerItemEquick 없음"); return false; }
        var prefab = equipPrefab ? equipPrefab : ctx.itemPrefabs; // 폴백 허용
        if (!prefab) { Debug.LogWarning("[EquipEffect] prefab 없음"); return false; }

        equip.Equip(prefab);
        return true;
    }
}

[CreateAssetMenu(menuName = "ItemEffects/Charge")]
public class ChargeEffectAsset : ItemEffectAsset
{
    [Min(1)] public int amount = 10;

    public override bool Execute(ItemEffectContext ctx)
    {
        return true;
    }
}

// [CreateAssetMenu(menuName = "ItemEffects/Spawn")]
// public class SpawnEffectAsset : ItemEffectAsset
// {
//     public GameObject prefab;
//     public Vector3 localOffset = new Vector3(0, 0, 1);

//     public override bool Execute(ItemEffectContext ctx)
//     {
//         if (!prefab || !ctx.user) return false;
//         var p = ctx.user.transform.TransformPoint(localOffset);
//         Object.Instantiate(prefab, p, Quaternion.identity);
//         return true;
//     }
// }
