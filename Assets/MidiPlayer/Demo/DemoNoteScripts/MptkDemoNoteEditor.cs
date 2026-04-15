#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace DemoMPTK
{
    /// <summary>
    /// Custom inspector for the Note component.
    /// Global mode is controlled by the EDIT_MODE constant:
    /// - true  = Edit mode (plain text editing)
    /// - false = Preview mode (RichText rendering only)
    /// </summary>
    [CustomEditor(typeof(MptkDemoNote))]
    public class MptkDemoNoteEditor : Editor
    {
        /// <summary>
        /// Global mode flag.
        /// Set this to true for edit mode, or false for preview mode.
        /// Changing this value affects all Note inspectors.
        /// </summary>
        private const bool EDIT_MODE = false;

        // Serialized properties for the Note fields
        private SerializedProperty _titleProp;
        private SerializedProperty _messageProp;
        private SerializedProperty _roleProp;

        // GUI style used for the RichText preview
        private GUIStyle _richStyle;

        // Cached icons for different note roles
        private GUIContent _infoIcon;
        private GUIContent _warningIcon;
        private GUIContent _tipIcon;

        private void OnEnable()
        {
            // Cache the serialized properties
            _titleProp = serializedObject.FindProperty("title");
            _messageProp = serializedObject.FindProperty("message");
            _roleProp = serializedObject.FindProperty("role");

            // Load some built-in Unity icons (fallbacks if not found are handled by Unity)
            _infoIcon = EditorGUIUtility.IconContent("console.infoicon");
            _warningIcon = EditorGUIUtility.IconContent("console.warnicon");

            // For Tip, we can reuse the info icon (or change to another built-in if desired)
            _tipIcon = _infoIcon;
        }

        public override void OnInspectorGUI()
        {
            // Initialize a RichText-enabled style for preview mode
            if (_richStyle == null)
                _richStyle = new GUIStyle(EditorStyles.label)
                {
                    wordWrap = true,
                    richText = true,
                    fontSize = 12
                };

            // Sync serialized object with the target object
            serializedObject.Update();

#pragma warning disable CS0162 // Unreachable code detected
            if (EDIT_MODE)
            {
                DrawEditMode();
            }
            else
            {
                DrawPreviewMode();
            }
#pragma warning restore CS0162 // Unreachable code detected

            // Apply any modified properties back to the object
            serializedObject.ApplyModifiedProperties();
        }
         
        /// <summary>
        /// Draws the global "Edit" mode UI:
        /// title field + message text area + role selection.
        /// </summary>
        private void DrawEditMode()
        {
            EditorGUILayout.LabelField("Note Editing (Global Edit Mode)", EditorStyles.boldLabel);

            // Editable title field
            EditorGUILayout.PropertyField(_titleProp, new GUIContent("Title"));

            // Message field (plain text editing)
            EditorGUILayout.LabelField("Message (plain text, RichText tags allowed):");
            _messageProp.stringValue = EditorGUILayout.TextArea(_messageProp.stringValue, GUILayout.MinHeight(80));

            //EditorGUILayout.Space(4);
            //EditorGUILayout.LabelField("RichText examples: <b>bold</b>, <i>italic</i>, <color=yellow>color</color>, <size=18>big</size>", EditorStyles.miniLabel);

            //EditorGUILayout.Space(8);

            // Role selection (enum popup)
            EditorGUILayout.PropertyField(_roleProp, new GUIContent("Role")); 
        }

        /// <summary>
        /// Draws the global "Preview" mode UI:
        /// read-only RichText rendering with an icon and colored title.
        ///</summary>
        private void DrawPreviewMode()
        {
            //EditorGUILayout.LabelField("Note Preview (Global Preview Mode)", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                NoteRole role = (NoteRole)_roleProp.enumValueIndex;
                GUIContent icon = GetIconForRole(role);
                string titleColor = GetColorForRole(role);

                // Header: icon + colored title (if any)
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (icon != null)
                    {
                        // Draw icon with a fixed size
                        GUILayout.Label(icon, GUILayout.Width(20), GUILayout.Height(20));
                    }

                    if (!string.IsNullOrEmpty(_titleProp.stringValue))
                    {
                        string richTitle = $"<b><color={titleColor}>{_titleProp.stringValue}</color></b>";
                        EditorGUILayout.LabelField(richTitle, _richStyle);
                    }
                    else
                    {
                        // If no title, still show the role text
                        string roleText = $"<i><color={titleColor}>{role}</color></i>";
                        EditorGUILayout.LabelField(roleText, _richStyle);
                    }
                }

                EditorGUILayout.Space(4);

                // Body: message rendered as RichText
                string richContent = _messageProp.stringValue;
                if (string.IsNullOrEmpty(richContent))
                {
                    EditorGUILayout.LabelField("<i>No message defined for this note.</i>", _richStyle);
                }
                else
                {
                    EditorGUILayout.LabelField(richContent, _richStyle);
                }
            }

            //EditorGUILayout.Space(4);
            //EditorGUILayout.LabelField("RichText and icons are rendered here. Switch EDIT_MODE in NoteEditor.cs to go back to edit mode.", EditorStyles.miniLabel);
        }

        /// <summary>
        /// Returns an icon GUIContent based on the note role.
        /// </summary>
        private GUIContent GetIconForRole(NoteRole role)
        {
            switch (role)
            {
                case NoteRole.Info:
                    return _infoIcon;
                case NoteRole.Warning:
                    return _warningIcon;
                case NoteRole.Tip:
                    return _tipIcon;
                default:
                    return null;
            }
        }

        /// <summary>
        /// Returns a color name or hex code used for the title text depending on the role.
        /// </summary>
        private string GetColorForRole(NoteRole role)
        {
            switch (role)
            {
                case NoteRole.Info:
                    return "#3A7EBF";  // Deep desaturated blue
                case NoteRole.Warning:
                    return "#D08A2F";  // Warm dark orange
                case NoteRole.Tip:
                    return "#4AA24A";  // Natural green
                default:
                    return "white";
            }
        }

    }
}
#endif
