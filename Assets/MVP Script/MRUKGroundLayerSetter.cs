using System.Collections;
using UnityEngine;
using Meta.XR.MRUtilityKit;

public class MRUKGroundLayerSetter : MonoBehaviour
{
    public string targetLayerName = "Ground";
    public GameObject caterpillarPrefab;

    void Start()
    {
        StartCoroutine(WaitAndSetup());
    }

    IEnumerator WaitAndSetup()
    {
        // 等 MRUK 建好 Room
        while (MRUK.Instance == null || MRUK.Instance.GetCurrentRoom() == null)
            yield return null;

        yield return new WaitForSeconds(0.5f);

        // 把 Room 下面所有東西設成 Ground Layer
        int layer = LayerMask.NameToLayer(targetLayerName);
        MRUKRoom room = MRUK.Instance.GetCurrentRoom();
        SetLayerRecursively(room.gameObject, layer);

        // 生成毛蟲在地板上
        Vector3 spawnPos = room.FloorAnchor.transform.position + Vector3.up * 0.5f;
        Instantiate(caterpillarPrefab, spawnPos, Quaternion.identity);
    }

    void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
            SetLayerRecursively(child.gameObject, layer);
    }
}