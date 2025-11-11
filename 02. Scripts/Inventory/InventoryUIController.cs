using UnityEngine;
using UnityEngine.UI;

// ItemUseRegistry: IItemUse[] 등록해서 키별로 Use 라우팅
public class InventoryUIController : MonoBehaviour
{
    public InventoryStore store;
    public ItemUseRegistry registry;    // DB 대신 사용
    public Button[] itemSlot;
    public GameObject player;

    void Awake()
    {
        // 1) 인스펙터 주입
        if (!registry) registry = ItemUseRegistry.Instance;

        // 2) 그래도 없으면 씬에서 탐색 (비활성 포함)
        if (!registry) registry = FindObjectOfType<ItemUseRegistry>(true);
    }
    
     void Start()
    {
        // 찐 Last 체크
        if (!registry)
        {
            Debug.LogError("[UI] ItemUseRegistry 못 찾음");
            return;
        }
    }
    void OnEnable()
    {
        // 3) 인스펙터 주입
        if (!registry) registry = ItemUseRegistry.Instance;

        Debug.Log(registry.name);
        if (store != null) store.OnInventoryChanged += HandleInventoryChanged;
        Refresh();
    }

    void OnDisable()
    {
        if (store != null) store.OnInventoryChanged -= HandleInventoryChanged;
    }

    void HandleInventoryChanged(string itemId, int newCount) => Refresh();

    public void Refresh()
    {
        if (store == null || itemSlot == null) return;

        // 1) 초기화
        foreach (var btn in itemSlot)
        {
            btn.onClick.RemoveAllListeners();
            btn.image.sprite = null;
            btn.image.enabled = false;
            btn.interactable = false;
        }

        // 2) 스냅샷 -> 슬롯 바인딩
        int i = 0;
        var snap = store.Snapshot(); // IReadOnlyDictionary<string,int>
        foreach (var kv in snap)
        {
            if (i >= itemSlot.Length) break;

            string id = kv.Key;
            var btn = itemSlot[i];

            // 아이콘: Resources/Icons/{key}.png (없으면 이미지 비활성)
            var icon = Resources.Load<Sprite>($"Icons/{id}");
            if (icon != null)
            {
                btn.image.sprite = icon;
                btn.image.enabled = true;
            }

            btn.onClick.AddListener(() => OnClickUse(id));
            btn.interactable = true;

            i++;
        }
    }

    void OnClickUse(string itemId)
    {
        if (registry == null)
        {
            Debug.LogWarning("[UI] registry 미할당");
            return;
        }

        var ctx = new ItemEffectContext { user = player, store = store, runner = this, itemPrefabs = null };

        if (!registry.TryUse(itemId, ctx, out bool consumable))
        {
            Debug.LogWarning($"[UI] 핸들러 없음: {itemId}");
            return;
        }

        if (consumable)
            store.Remove(itemId, 1);

        Refresh();
    }
}
