using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BigRedButton.Editor
{
    /// <summary>Inspector controls for seeing PaintBucket dimension edits immediately.</summary>
    [CustomEditor(typeof(PaintBucket))]
    [CanEditMultipleObjects]
    public sealed class PaintBucketEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Edit any placement, body, rim, or paint-bar field above, " +
                "then rebuild the Scene view preview. The preview is editor-only and is " +
                "not saved into the level.", MessageType.Info);

            if (!GUILayout.Button("Rebuild Bucket Preview"))
                return;

            foreach (Object item in targets)
            {
                var bucket = (PaintBucket)item;
                bucket.Build();
                EditorUtility.SetDirty(bucket);
                EditorSceneManager.MarkSceneDirty(bucket.gameObject.scene);
            }
        }
    }
}
