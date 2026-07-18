using UnityEngine;

namespace Chimera
{
    /// ★ 這支是「暫時的」佔位，示範移動邏輯該長什麼樣：
    ///   只做一件事 —— 移動自己的 Transform。
    /// 你之後寫的移動（MRUK 空間漫遊、繞著觀眾、追手部…）就寫在這個位置，
    /// 完全不需要知道 zooid、器官、shader 的存在。用完把這支刪掉即可。
    public class SampleWanderDriver : MonoBehaviour
    {
        [Range(0f, 2f)] public float speed = 0.4f;
        public Vector3 extents = new Vector3(2.4f, 1.1f, 2.0f);
        public Vector3 center = new Vector3(0f, 1.6f, 0f);

        void Update()
        {
            float t = Time.time * speed;
            transform.position = center + new Vector3(
                Mathf.Sin(t * 0.21f) * extents.x + Mathf.Sin(t * 0.37f) * extents.x * 0.4f,
                Mathf.Sin(t * 0.17f) * extents.y,
                Mathf.Cos(t * 0.26f) * extents.z);
        }
    }
}
