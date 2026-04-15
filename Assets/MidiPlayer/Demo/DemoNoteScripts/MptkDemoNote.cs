using UnityEngine;

namespace DemoMPTK
{
    /// <summary>
    /// Role of the note content. This impacts how it is displayed in the inspector.
    /// </summary>
    public enum NoteRole
    {
        Info,
        Warning,
        Tip
    }

    /// <summary>
    /// Simple component used to attach documentation notes to a GameObject.
    /// The content is mainly intended for use in the Unity Editor (Inspector).
    /// </summary>
    //[DisallowMultipleComponent]
    //[AddComponentMenu("Maestro/Note")]
    public class MptkDemoNote : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Optional short title for this note.")]
        private string title;

        [SerializeField]
        [TextArea(3, 10)]
        [Tooltip("Main note text. RichText tags are supported in the custom editor preview.")]
        private string message;

        [SerializeField]
        [Tooltip("Semantic role of this note: Info, Warning, Tip, etc.")]
        private NoteRole role = NoteRole.Info;

        /// <summary>
        /// Gets the title of the note.
        /// </summary>
        public string Title => title;

        /// <summary>
        /// Gets the main text content of the note.
        /// </summary>
        public string Message => message;

        /// <summary>
        /// Gets the semantic role of this note.
        /// </summary>
        public NoteRole Role => role;
    }
}
