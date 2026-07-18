# Chimera — Unity / URP 移植

網頁版 v6 的完整移植：形態雜湊、分區文法、器官（眼／口／小頭／肢）、虹光低面材質、觸手擺動。
**移動邏輯刻意留白**——你自己寫。

---

## 檔案結構

把整個 `ChimeraUnity` 資料夾丟進 `Assets/` 底下即可。

```
Assets/ChimeraUnity/
├── Runtime/
│   ├── ChimeraParams.cs        雜湊 → 參數、分區規則
│   ├── IcoSphere.cs            icosphere 生成（Unity 內建球不能用）
│   ├── ChimeraMeshBuilder.cs   器官與附肢的程序化 mesh
│   ├── ISpineProvider.cs       ★ 移動與生物的分界介面
│   ├── VerletSpine.cs          預設脊索（可替換）
│   ├── ChimeraColony.cs        群體：生成、擺位、推參數
│   └── SampleWanderDriver.cs   佔位移動，用完刪掉
└── Shaders/
    ├── ChimeraCommon.hlsl      simplex noise + flat normal + 虹光著色
    ├── ChimeraBody.shader      本體（頂點位移）
    └── ChimeraOrgan.shader     器官（擺動 + 頂點型別分流）
```

---

## Step 1 — 建立兩個材質

1. Project 視窗右鍵 → Create → Material，命名 `M_ChimeraBody`
2. Inspector 最上方 Shader 下拉 → **Chimera / Body**
3. 同樣建一個 `M_ChimeraOrgan`，shader 選 **Chimera / Organ**

材質上的數值不用調——執行時會被 `MaterialPropertyBlock` 覆蓋。材質只是「用哪支 shader」的容器。

> 如果 shader 下拉找不到 Chimera，看 Console 有沒有編譯錯誤。最常見是 `ChimeraCommon.hlsl` 不在 `.shader` 的同一層資料夾——`#include "ChimeraCommon.hlsl"` 是相對路徑。

---

## Step 2 — 場景組裝

1. Hierarchy 建一個空物件，命名 `Chimera`
2. 加上元件 **VerletSpine**
3. 加上元件 **ChimeraColony**
4. 再建一個空物件 `HeadTarget`，**放在 Chimera 外面**（不要當子物件）
5. 把 `HeadTarget` 拖進 VerletSpine 的 `Head Target` 欄位
6. 在 ChimeraColony 上：
   - `Body Material` ← `M_ChimeraBody`
   - `Organ Material` ← `M_ChimeraOrgan`
   - `Spine Provider Behaviour` ← 把 `Chimera` 物件自己拖進去（會自動抓 VerletSpine）

按 Play。應該會看到一條群體從 HeadTarget 垂下來。

---

## Step 3 — 讓它動（這步是你的）

暫時先掛 `SampleWanderDriver` 到 **HeadTarget** 上，確認整條鏈會跟著游。

確認之後刪掉它，換成你自己的腳本。你的腳本只需要做一件事：

```csharp
public class MyChimeraLocomotion : MonoBehaviour
{
    void Update()
    {
        // 想怎麼走就怎麼走 —— MRUK 房間漫遊、繞著觀眾、追手部、跟隨視線…
        transform.position = ...;
    }
}
```

掛在 HeadTarget 上就好。**你不需要碰任何 zooid、器官、shader 的程式碼。**

### 如果連 verlet 都想自己寫

實作 `ISpineProvider` 就好：

```csharp
public class MySpine : MonoBehaviour, ISpineProvider
{
    public int Count => 18;
    public void Tick(float dt) { /* 你的物理 */ }
    public Vector3 GetPoint(int i) { ... }
    public Vector3 GetForward(int i) { ... }
}
```

然後把它拖進 ChimeraColony 的 `Spine Provider Behaviour`，把 VerletSpine 移除。

---

## Step 4 — 控制項對照

網頁版的滑桿都在 Inspector 上，位置如下：

| 網頁滑桿 | Unity 位置 |
|---|---|
| 節數 zooids | VerletSpine → Node Count |
| 鬆弛 slack | VerletSpine → Slack |
| 泳速 speed | 你自己的移動腳本（不再是本體的事） |
| 附肢量 append | ChimeraColony → Organs → Appendage Amount |
| 器官量 organs | ChimeraColony → Organs → Organ Amount |
| 眼／口／小頭／肢 | ChimeraColony → Organs → 四個 checkbox |
| 觸手長 tendril | ChimeraColony → Tendril Length |
| 面 facet | ChimeraColony → Facet |
| 虹光 iridescence | ChimeraColony → Iridescence |
| 體型 scale | ChimeraColony → Zooid Scale |
| 虹光／玻璃／不透明 | ChimeraColony → Glass（1 = 玻璃，0 = 不透明） |
| 泳動 | ChimeraColony → Swim Pulse |
| 標籤文字 | ChimeraColony → Label |

**即時生效**：Facet、Iridescence、Tendril Length、Glass、Zooid Scale、Swim Pulse
**需要重建**：Label、器官開關、Organ Amount、Appendage Amount、Node Count
→ 改完勾一下 `Rebuild Now`（改 Inspector 任何欄位時 `OnValidate` 會自動勾）

---

## Step 5 — 接上你的終端機輸入

參與者打的字直接餵進 `label`：

```csharp
colony.label = participantInput;
colony.Build();
```

同一個字串永遠長出同一隻群體——FNV-1a 是 deterministic 的，跟你既有的 `SegmentParams` 用同一套雜湊。

---

## Quest 3 上必須處理的四件事

現在這版是「在編輯器裡看起來對」的版本，**還不是可以上頭顯的版本**。

**1. 半透明的深度排序。**
兩支 shader 都是 `ZWrite Off`，節點之間沒有正確前後遮擋。桌面單眼看很美，立體視覺下深度線索會打架，戴久會不舒服。
→ 上機前把 `Glass` 設成 0（不透明），或改成 alpha-clip。虹光靠 fresnel 加色，不是靠透明度，所以不透明版一樣好看。

**2. Draw call。**
現在每節 2 個 renderer，20 節 = 40+ draw call，加上半透明排序成本。
→ 器官 mesh 按分區只做 3 套共用 mesh，改用 `Graphics.DrawMeshInstanced`；或把整條群體合併成一個 mesh，用頂點色帶 per-node 參數。

**3. Passthrough 的黑背景不存在。**
布拉施卡那張照片一半的美感來自純黑。你的展場是有地板有雜物的真實空間。
→ 這件事沒有程式解，只能實測。建議先做這一步，再決定要不要調整整個美術方向。

**4. `ddx/ddy` 在 mobile 上的成本。**
Quest 的 GPU 對導數指令沒有桌面那麼寬容，但這個用法很輕（每像素兩次），實測應該可以接受。真的卡的話，改成 CPU 端把 mesh 拆成 non-indexed 並烘 flat normal。

---

## 已知落差

- 網頁版的目錄／單件解剖介面沒有移植（那是探索工具，不是作品）
- 網頁版脊線（LineRenderer）沒有移植——你原本 `ChimeraCrawler.cs` 就有 LineRenderer，接回去即可
- 這批程式碼**沒有經過 Unity 編譯器驗證**，我這邊沒有 Unity 環境。第一次匯入時 Console 可能會有小錯（namespace、API 版本差異），把第一行錯誤貼給我就好。
