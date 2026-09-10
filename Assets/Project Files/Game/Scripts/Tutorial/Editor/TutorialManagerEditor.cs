#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace BlockShooter
{
    [UnityEditor.CustomEditor(typeof(TutorialManager))]
    public class TutorialManagerEditor : UnityEditor.Editor
    {
        private UnityEditor.SerializedProperty _canvasControllerProp;
        private UnityEditor.SerializedProperty _autoStartOnLevelLoadProp;
        private UnityEditor.SerializedProperty _tutorialsProp;

        private void OnEnable()
        {
            _canvasControllerProp = serializedObject.FindProperty("_canvasController");
            _autoStartOnLevelLoadProp = serializedObject.FindProperty("_autoStartOnLevelLoad");
            _tutorialsProp = serializedObject.FindProperty("_tutorials");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Draw presentation & behavior settings
            UnityEditor.EditorGUILayout.LabelField("General Settings", UnityEditor.EditorStyles.boldLabel);
            UnityEditor.EditorGUILayout.PropertyField(_canvasControllerProp);
            UnityEditor.EditorGUILayout.PropertyField(_autoStartOnLevelLoadProp);
            
            UnityEditor.EditorGUILayout.Space(10);
            
            // Draw Tutorials header with utility buttons
            UnityEditor.EditorGUILayout.BeginHorizontal();
            UnityEditor.EditorGUILayout.LabelField("Tutorials List", UnityEditor.EditorStyles.boldLabel);
            if (UnityEngine.GUILayout.Button("Collapse All", UnityEditor.EditorStyles.miniButton, UnityEngine.GUILayout.Width(80)))
            {
                SetAllExpanded(false);
            }
            if (UnityEngine.GUILayout.Button("Expand All", UnityEditor.EditorStyles.miniButton, UnityEngine.GUILayout.Width(80)))
            {
                SetAllExpanded(true);
            }
            if (UnityEngine.GUILayout.Button("+ Add Tutorial", UnityEditor.EditorStyles.miniButton, UnityEngine.GUILayout.Width(100)))
            {
                _tutorialsProp.arraySize++;
            }
            UnityEditor.EditorGUILayout.EndHorizontal();

            UnityEditor.EditorGUILayout.Space(5);

            // Draw list of tutorials
            for (int i = 0; i < _tutorialsProp.arraySize; i++)
            {
                UnityEditor.SerializedProperty tutorialProp = _tutorialsProp.GetArrayElementAtIndex(i);
                DrawTutorialElement(tutorialProp, i);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawTutorialElement(UnityEditor.SerializedProperty tutorialProp, int index)
        {
            var levelProp = tutorialProp.FindPropertyRelative("_level");
            var showOnceProp = tutorialProp.FindPropertyRelative("_showOnce");
            var tutorialRootProp = tutorialProp.FindPropertyRelative("_tutorialRoot");
            var useStepsProp = tutorialProp.FindPropertyRelative("_useSteps");
            var targetIdProp = tutorialProp.FindPropertyRelative("_targetId");
            var handOffsetProp = tutorialProp.FindPropertyRelative("_handOffset");
            var stepsProp = tutorialProp.FindPropertyRelative("_steps");

            // Header Style
            UnityEngine.GUIStyle headerStyle = new UnityEngine.GUIStyle(UnityEditor.EditorStyles.helpBox);
            headerStyle.margin = new UnityEngine.RectOffset(0, 0, 4, 4);

            UnityEditor.EditorGUILayout.BeginVertical(headerStyle);
            
            UnityEditor.EditorGUILayout.BeginHorizontal();
            
            // Get label showing Level and whether it uses steps
            string label = $"Tutorial [Level {levelProp.intValue}]" + (useStepsProp.boolValue ? $" ({stepsProp.arraySize} Steps)" : " (Single Target)");
            tutorialProp.isExpanded = UnityEditor.EditorGUILayout.Foldout(tutorialProp.isExpanded, label, true);
            
            UnityEngine.GUILayout.FlexibleSpace();
            
            // Delete button
            if (UnityEngine.GUILayout.Button("✕", UnityEditor.EditorStyles.miniButton, UnityEngine.GUILayout.Width(20)))
            {
                int oldSize = _tutorialsProp.arraySize;
                _tutorialsProp.DeleteArrayElementAtIndex(index);
                if (_tutorialsProp.arraySize == oldSize)
                {
                    _tutorialsProp.DeleteArrayElementAtIndex(index);
                }
                UnityEditor.EditorGUILayout.EndHorizontal();
                UnityEditor.EditorGUILayout.EndVertical();
                return;
            }
            UnityEditor.EditorGUILayout.EndHorizontal();

            if (tutorialProp.isExpanded)
            {
                UnityEditor.EditorGUI.indentLevel++;
                UnityEditor.EditorGUILayout.Space(4);
                
                UnityEditor.EditorGUILayout.PropertyField(levelProp);
                UnityEditor.EditorGUILayout.PropertyField(showOnceProp);
                UnityEditor.EditorGUILayout.PropertyField(tutorialRootProp);
                
                UnityEditor.EditorGUILayout.Space(4);
                UnityEditor.EditorGUILayout.PropertyField(useStepsProp);

                if (useStepsProp.boolValue)
                {
                    UnityEditor.EditorGUILayout.Space(6);
                    UnityEditor.EditorGUILayout.BeginHorizontal();
                    UnityEditor.EditorGUILayout.LabelField("Steps", UnityEditor.EditorStyles.boldLabel);
                    if (UnityEngine.GUILayout.Button("+ Add Step", UnityEditor.EditorStyles.miniButton, UnityEngine.GUILayout.Width(80)))
                    {
                        stepsProp.arraySize++;
                    }
                    UnityEditor.EditorGUILayout.EndHorizontal();

                    UnityEditor.EditorGUI.indentLevel++;
                    for (int s = 0; s < stepsProp.arraySize; s++)
                    {
                        UnityEditor.SerializedProperty stepProp = stepsProp.GetArrayElementAtIndex(s);
                        DrawStepElement(stepsProp, stepProp, s);
                    }
                    UnityEditor.EditorGUI.indentLevel--;
                }
                else
                {
                    UnityEditor.EditorGUILayout.PropertyField(targetIdProp);
                    UnityEditor.EditorGUILayout.PropertyField(handOffsetProp);
                }
                
                UnityEditor.EditorGUILayout.Space(4);
                UnityEditor.EditorGUI.indentLevel--;
            }

            UnityEditor.EditorGUILayout.EndVertical();
        }

        private void DrawStepElement(UnityEditor.SerializedProperty stepsProp, UnityEditor.SerializedProperty stepProp, int index)
        {
            var stepRootProp = stepProp.FindPropertyRelative("_stepRoot");
            var targetIdProp = stepProp.FindPropertyRelative("_targetId");
            var handOffsetProp = stepProp.FindPropertyRelative("_handOffset");

            UnityEditor.EditorGUILayout.BeginVertical(UnityEditor.EditorStyles.helpBox);
            UnityEditor.EditorGUILayout.BeginHorizontal();
            
            string stepLabel = $"Step {index + 1}" + (stepRootProp.objectReferenceValue != null ? $" ({stepRootProp.objectReferenceValue.name})" : "");
            stepProp.isExpanded = UnityEditor.EditorGUILayout.Foldout(stepProp.isExpanded, stepLabel, true);
            
            UnityEngine.GUILayout.FlexibleSpace();
            
            if (UnityEngine.GUILayout.Button("✕", UnityEditor.EditorStyles.miniButton, UnityEngine.GUILayout.Width(20)))
            {
                int oldSize = stepsProp.arraySize;
                stepsProp.DeleteArrayElementAtIndex(index);
                if (stepsProp.arraySize == oldSize)
                {
                    stepsProp.DeleteArrayElementAtIndex(index);
                }
                UnityEditor.EditorGUILayout.EndHorizontal();
                UnityEditor.EditorGUILayout.EndVertical();
                return;
            }
            UnityEditor.EditorGUILayout.EndHorizontal();

            if (stepProp.isExpanded)
            {
                UnityEditor.EditorGUI.indentLevel++;
                UnityEditor.EditorGUILayout.PropertyField(stepRootProp);
                UnityEditor.EditorGUILayout.PropertyField(targetIdProp);
                UnityEditor.EditorGUILayout.PropertyField(handOffsetProp);
                UnityEditor.EditorGUI.indentLevel--;
            }

            UnityEditor.EditorGUILayout.EndVertical();
        }

        private void SetAllExpanded(bool expanded)
        {
            for (int i = 0; i < _tutorialsProp.arraySize; i++)
            {
                UnityEditor.SerializedProperty tutorialProp = _tutorialsProp.GetArrayElementAtIndex(i);
                tutorialProp.isExpanded = expanded;
                
                var stepsProp = tutorialProp.FindPropertyRelative("_steps");
                if (stepsProp != null)
                {
                    for (int s = 0; s < stepsProp.arraySize; s++)
                    {
                        stepsProp.GetArrayElementAtIndex(s).isExpanded = expanded;
                    }
                }
            }
        }
    }
}
#endif
