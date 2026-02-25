using UnityEditor;
using UnityEngine;

using Script.Environment;

namespace Script.Environment.Editor
{
    [CustomEditor(typeof(DayCycleManager))]
    public class DayCycleManagerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            DayCycleManager script = (DayCycleManager)target;

            if (GUILayout.Button("End Day"))
            {
                script.EndDay();
            }
        }
    }
}
