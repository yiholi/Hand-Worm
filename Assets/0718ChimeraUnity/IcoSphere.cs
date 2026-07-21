using System.Collections.Generic;
using UnityEngine;

namespace Chimera
{
    /// Unity 內建 Sphere 是 UV 球（兩極擠成一團），做 low-poly 位移會爛掉。
    /// 這裡自己生 icosphere：面積均勻、三角形形狀一致。
    public static class IcoSphere
    {
        public static Mesh Create(int subdivisions)
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var verts = new List<Vector3>
            {
                new Vector3(-1,  t, 0), new Vector3( 1,  t, 0), new Vector3(-1, -t, 0), new Vector3( 1, -t, 0),
                new Vector3( 0, -1,  t), new Vector3( 0,  1,  t), new Vector3( 0, -1, -t), new Vector3( 0,  1, -t),
                new Vector3( t,  0, -1), new Vector3( t,  0,  1), new Vector3(-t,  0, -1), new Vector3(-t,  0,  1)
            };
            for (int i = 0; i < verts.Count; i++) verts[i] = verts[i].normalized;

            var tris = new List<int>
            {
                0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11,
                1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9,
                4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1
            };

            var cache = new Dictionary<long, int>();
            for (int s = 0; s < subdivisions; s++)
            {
                var next = new List<int>(tris.Count * 4);
                for (int i = 0; i < tris.Count; i += 3)
                {
                    int a = Mid(verts, cache, tris[i], tris[i + 1]);
                    int b = Mid(verts, cache, tris[i + 1], tris[i + 2]);
                    int c = Mid(verts, cache, tris[i + 2], tris[i]);
                    next.AddRange(new[] { tris[i], a, c, tris[i + 1], b, a, tris[i + 2], c, b, a, b, c });
                }
                tris = next;
            }

            var mesh = new Mesh { name = "IcoSphere" + subdivisions };
            if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static int Mid(List<Vector3> verts, Dictionary<long, int> cache, int i1, int i2)
        {
            long key = i1 < i2 ? ((long)i1 << 32) + i2 : ((long)i2 << 32) + i1;
            if (cache.TryGetValue(key, out int v)) return v;
            Vector3 m = ((verts[i1] + verts[i2]) * 0.5f).normalized;
            verts.Add(m);
            int idx = verts.Count - 1;
            cache[key] = idx;
            return idx;
        }
    }
}
