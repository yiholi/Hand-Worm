using UnityEditor;
using UnityEngine;

namespace Chimera
{
    /// DataStyleChimeraColony 的 Inspector。
    /// 把 rebuildNow 那個「勾了會自己彈回」的怪 checkbox 換成真正的按鈕，
    /// 並說明哪些參數在材質球上、哪些在元件上。
    ///
    /// ★ 這個檔案必須放在名為 Editor 的資料夾底下，例如
    ///   Assets/0806Chimera/Editor/DataStyleChimeraColonyEditor.cs
    ///   否則 build 到 Quest 3 時會因為引用 UnityEditor 而失敗。
    [CustomEditor(typeof(DataStyleChimeraColony))]
    [CanEditMultipleObjects]
    public class DataStyleChimeraColonyEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var col = (DataStyleChimeraColony)target;

            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("重建 Rebuild", GUILayout.Height(28)))
                {
                    col.Build();
                    SceneView.RepaintAll();
                }
                if (GUILayout.Button("清除 Clear", GUILayout.Height(28)))
                {
                    col.ClearZooids();
                    SceneView.RepaintAll();
                }
            }

            EditorGUILayout.HelpBox(
                "外觀參數在材質球上，隨時可拖：\n" +
                "Glitch Amount / Glitch Rate / Burst / Drift / Block Count / Tear / " +
                "Chroma Split / Channel Blowout / Posterise / Projection Scale / " +
                "Displacement Amp / Facet / Rim Iridescence / Rim Power / Hue Shift Mix / Darken\n\n" +
                "這個元件只負責「每顆球哪裡不一樣」：_Seed 與 _UvRect，以及逐節點的形態值。\n\n" +
                "★ Glitch Rate / Drift / 呼吸依賴 shader 時間，Scene 視窗要打開 Always Refresh " +
                "才看得到，否則會以為它們沒作用。",
                MessageType.None);

            if (col.freezePerNodeShape)
            {
                EditorGUILayout.HelpBox(
                    "Freeze Per Node Shape：開\n" +
                    "27 顆球共用材質球上的形態值，方便單獨觀察某一組參數。\n" +
                    "_Seed 與 _UvRect 仍然逐節點，所以貼圖窗格還是不一樣。",
                    MessageType.Info);
            }

            if (col.bodyMaterial != null &&
                GUILayout.Button("選取 Body Material（在 Inspector 裡調參數）"))
            {
                Selection.activeObject = col.bodyMaterial;
            }

            EditorGUILayout.Space(6);

            serializedObject.Update();
            var it = serializedObject.GetIterator();
            bool enterChildren = true;
            while (it.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (it.name == "m_Script") continue;
                if (it.name == "rebuildNow") continue;   // 已經有按鈕了
                EditorGUILayout.PropertyField(it, true);
            }

            if (serializedObject.ApplyModifiedProperties())
                SceneView.RepaintAll();
        }
    }
}