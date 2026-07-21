using System.Collections;
using System.Linq; // 引入 LINQ 套件，這樣才能使用 FirstOrDefault() 功能
using UnityEngine;
using Meta.XR.MRUtilityKit;

public class MRUKGroundLayerSetter : MonoBehaviour
{
    // 目標圖層的名稱，預設為 "Ground"
    public string targetLayerName = "Ground";
    // 毛毛蟲的預製物（Prefab）
    public GameObject caterpillarPrefab;

    // 遊戲開始時由 Unity 自動執行
    void Start()
    {
        // 啟動協程，用來等待 MRUK 載入空間
        StartCoroutine(WaitAndSetup());
    }

    // 協程：負責等待空間生成並放置毛毛蟲
    IEnumerator WaitAndSetup()
    {
        // 迴圈檢查：如果 MRUK 還沒準備好，或是還沒抓到房間資料，就等待下一幀
        while (MRUK.Instance == null || MRUK.Instance.GetCurrentRoom() == null)
            yield return null;

        // 額外等待 0.5 秒，確保空間物件都完全建立完畢
        yield return new WaitForSeconds(0.5f);

        // 取得目標圖層的 Layer 編號
        int layer = LayerMask.NameToLayer(targetLayerName);
        // 取得當前的 MRUK 房間物件
        MRUKRoom room = MRUK.Instance.GetCurrentRoom();
        // 透過遞迴函式，把房間內的所有物件與子物件都設定為 Ground 圖層
        SetLayerRecursively(room.gameObject, layer);

        // 使用 LINQ 的 FirstOrDefault：
        // 它會自動抓取 FloorAnchors 清單中的第一個地板。如果清單是空的，它會聰明地回傳 null，不會讓程式當掉
        MRUKAnchor floor = room.FloorAnchors.FirstOrDefault();
        
        // 安全防呆：如果真的找不到任何地板資料（floor 為 null）
        if (floor == null)
        {
            // 在主控台印出黃色警告訊息
            Debug.LogWarning("[MRUKGroundLayerSetter] 找不到 floor anchor，跳過生成毛蟲。");
            // 直接跳出協程，不再往下執行生成毛毛蟲的程式碼
            yield break;
        }

        // 抓取地板的位置，並往上提 0.5 公尺作為生成位置
        Vector3 spawnPos = floor.transform.position + Vector3.up * 0.5f;
        // 在剛剛算好的位置生成毛毛蟲，並保持預設的角度
        Instantiate(caterpillarPrefab, spawnPos, Quaternion.identity);
    }

    // 遞迴函式：用來把某個物件以及它底下的所有子物件都換成同一個圖層
    void SetLayerRecursively(GameObject obj, int layer)
    {
        // 設定目前物件的圖層
        obj.layer = layer;
        // 使用 foreach 檢查這物件底下的每一個子物件
        foreach (Transform child in obj.transform)
            // 讓子物件也執行同一個功能（自己呼叫自己，這就是遞迴）
            SetLayerRecursively(child.gameObject, layer);
    }
}