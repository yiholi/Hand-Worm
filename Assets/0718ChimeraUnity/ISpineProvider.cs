using UnityEngine;

namespace Chimera
{
    /// ★ 這是「移動」與「生物」的分界線。
    /// ChimeraColony 只跟這個介面說話：它問「第 i 節在哪、朝哪」，然後把 zooid 擺過去。
    /// 你自己寫的移動邏輯只要實作這個介面（或驅動 VerletSpine 的 head），
    /// 就完全不需要碰形態、材質、器官那一整套。
    public interface ISpineProvider
    {
        /// 脊索節點數。ChimeraColony 會依此決定生成幾個 zooid。
        int Count { get; }

        /// 每幀更新一次（由 ChimeraColony 呼叫，你不用自己 Update）
        void Tick(float deltaTime);

        /// 第 i 節的世界座標
        Vector3 GetPoint(int i);

        /// 第 i 節的朝向（指向前一節，也就是「頭的方向」）
        Vector3 GetForward(int i);
    }
}
