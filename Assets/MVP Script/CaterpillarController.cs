using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CaterpillarController : MonoBehaviour
{
    // 這裡要拖入你在 Project 視窗做好的毛毛蟲身體 Prefab
    public GameObject segmentPrefab;  
    // 遊戲開始時，毛毛蟲初始的身體節數
    public int startSegments = 6;
    // 每個身體節點之間的間距
    public float segmentSpacing = 0.35f;
    // 波浪起伏的頻率（速度）
    public float waveFrequency = 3f;
    // 波浪起伏的幅度（高度）
    public float waveAmplitude = 0.08f;
    // 每節身體的相位差（造成前後起伏的流水效果）
    public float phaseOffset = 0.8f;

    // 用來儲存所有身體節點的列表（List）
    private List<GameObject> segments = new List<GameObject>();

    // 遊戲啟動時執行一次
    void Start()
    {
        // 遊戲一開始，先從頭部依序往後生成初始的 6 節身體
        for (int i = 0; i < startSegments; i++)
        {
            // 計算初始每一節的位置（主要是往左邊延伸）
            Vector3 pos = transform.position + Vector3.left * i * segmentSpacing;
            // 生成球體
            GameObject seg = Instantiate(segmentPrefab, pos, Quaternion.identity);
            // 設為目前物件的子物件，方便管理
            seg.transform.parent = this.transform;
            // 依序加到列表的「尾巴」
            segments.Add(seg);
        }
    }

    // 每幀固定執行（處理原地 Sine 波起動畫與鍵盤偵測）
    void Update()
    {
        // 核心邏輯：用迴圈不斷更新列表中「所有圓圈」的位置
        // 因為按下空白鍵時，列表的順序（Index i）會改變，所以這裡會自動把舊圓圈往後推
        for (int i = 0; i < segments.Count; i++)
        {
            if (segments[i] != null)
            {
                // 使用數學的正弦函數（Sin）依據「時間」與「目前的順序 i」計算出波動的 Y 軸高度
                float wave = Mathf.Sin(Time.time * waveFrequency + i * phaseOffset);
                
                // 重新排列位置：
                // X 軸：依照當前的 index 決定距離（-i * 間距），達成往後推的效果
                // Y 軸：套用 Sine 波起伏，達成原地抖動效果
                // Z 軸：固定為 0，絕對不動
                segments[i].transform.localPosition = new Vector3(
                    -i * segmentSpacing,
                    wave * waveAmplitude,
                    0
                );
            }
        }

        // 偵測按下空白鍵
        if (Input.GetKeyDown(KeyCode.Space))
        {
            AddNewHeadSegment();
        }
    }

    // 按下空白鍵時執行的關鍵方法：從頭部插入新節點
    void AddNewHeadSegment()
    {
        // 1. 在頭部核心的原點位置 (0, 0, 0) 生成一個全新的圓圈
        GameObject newHead = Instantiate(segmentPrefab, transform.position, Quaternion.identity);
        // 2. 將新圓圈設為子物件
        newHead.transform.parent = this.transform;

        // 3. 【最核心步驟】使用 Insert(0, ...)，強行把新圓圈塞到列表的最前面（Index 0）
        // 這樣一來，原本在 Index 0 的舊頭部會自動變成 Index 1，所有人自動往後退一格！
        segments.Insert(0, newHead);
    }
}