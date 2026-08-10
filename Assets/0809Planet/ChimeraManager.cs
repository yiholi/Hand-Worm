using UnityEngine;
using Chimera; // 必須加上這行，才能讀取到你的怪獸腳本[cite: 1]

public class ChimeraManager : MonoBehaviour
{
    [Header("Chimera 輪流出場系統")]
    public GameObject[] chimeras;

    [Header("隨機屬性範圍設定")]
    public float minScale = 0.5f;
    public float maxScale = 1.5f;
    public float minSpeed = 0.5f;
    public float maxSpeed = 1.5f;

    // 紀錄目前畫面上是哪一隻，方便我們等一下徹底清除它
    private GameObject currentChimera;

    void Start()
    {
        // 遊戲一開始先隱藏所有生物
        foreach (GameObject chimera in chimeras)
        {
            if (chimera != null) chimera.SetActive(false);
        }

        // 立刻抽出第一隻並顯示
        RandomizeAndShowOne();
    }

    public void RandomizeAndShowOne()
    {
        // =========================================================
        // 【關鍵優化】：徹底清除上一隻的記憶體與效能負擔
        // =========================================================
        if (currentChimera != null)
        {
            DataStyleChimeraColony oldColony = currentChimera.GetComponent<DataStyleChimeraColony>();
            if (oldColony != null)
            {
                // 呼叫你的腳本內建功能，徹底銷毀生成的網格與物件，釋放記憶體[cite: 8]
                oldColony.ClearZooids(); 
            }
            // 清除完畢後，把空殼隱藏起來
            currentChimera.SetActive(false);
        }

        // =========================================================
        // 抽取並建立新怪獸
        // =========================================================
        if (chimeras.Length > 0)
        {
            int randomIndex = Random.Range(0, chimeras.Length);
            GameObject selectedChimera = chimeras[randomIndex];

            if (selectedChimera != null)
            {
                float randomY = Random.Range(0f, 360f);
                selectedChimera.transform.localEulerAngles = new Vector3(0f, randomY, 0f);

                ChimeraBodyPlan plan = selectedChimera.GetComponent<ChimeraBodyPlan>();
                if (plan != null)
                {
                    plan.creatureScale = Random.Range(minScale, maxScale);
                    plan.speed = Random.Range(minSpeed, maxSpeed);

                    if (plan is PlanOctopus octopus)
                    {
                        octopus.arms = Random.Range(4, 11); //[cite: 5]
                    }
                    else if (plan is PlanBeast beast)
                    {
                        beast.heads = Random.Range(1, 4); //[cite: 3]
                    }
                    else if (plan is PlanBird bird)
                    {
                        bird.wingPairs = Random.Range(1, 4); //[cite: 4]
                    }
                    else if (plan is PlanSeaHare hare)
                    {
                        hare.cerataClusters = Random.Range(4, 13); //[cite: 6]
                    }
                    else if (plan is PlanArthropod arthropod)
                    {
                        arthropod.gaitRate = Random.Range(1.5f, 4.5f); //[cite: 2]
                    }

                    // 重建骨架[cite: 1]
                    plan.Rebuild();

                    DataStyleChimeraColony colony = selectedChimera.GetComponent<DataStyleChimeraColony>();
                    if (colony != null)
                    {
                        colony.organs.eyes = Random.value > 0.5f;
                        colony.organs.mouths = Random.value > 0.5f;
                        colony.organs.headBuds = Random.value > 0.5f;
                        colony.organs.limbs = Random.value > 0.5f;

                        colony.organs.organAmount = Random.Range(0f, 1f);
                        colony.organs.appendageAmount = Random.Range(0f, 1f);
                        colony.tendrilLength = Random.Range(0f, 1f);

                        // 通知重繪[cite: 8]
                        colony.rebuildNow = true;
                    }
                }

                // 紀錄這次抽中的怪獸，下次轉頭時就會把它徹底清除
                currentChimera = selectedChimera;

                // 華麗登場！
                selectedChimera.SetActive(true);
            }
        }
    }
}