using UnityEngine;
using MidiPlayerTK;

namespace DemoMPTK
{

    /// <summary>
    /// Add this script to the GameObject you want to add Music.
    /// A sphere in the demo. Can be of course any kind of GameObject
    /// </summary>
    public class TestMidiSpatialization : MonoBehaviour
    {
        // Material of the GameObject, useful for random color.
        public Material material;
        // Start position
        public Vector3 StartPosition;
        public float Radius;
        public float smoothTime = 0.3F;
        public Vector3 RotateAmount;  // degrees per second to rotate in each axis. Set in inspector.

        private Vector3 velocity = Vector3.zero;
        private Vector3 Target;

        private void Start()
        {
            Random.InitState(System.DateTime.Now.Millisecond);
            SetTarget();
        }

        /// <summary>
        /// From the UI, add a gameobject which holds a MidiFilePlayer
        /// </summary>
        public void AddSphere()
        {
            // Create a GameObject from this GameObject (sphere + MidiFilePlayer)
            GameObject goCreated = Instantiate(gameObject);

            // Random color of the GameObject
            Renderer renderer = goCreated.GetComponent<Renderer>();
            renderer.material = material;
            renderer.material.color = new Color(Random.value, Random.value, Random.value);

            // Get the MidiFilePlayer component from this GameObject
            MidiFilePlayer mfp = goCreated.GetComponentInChildren<MidiFilePlayer>();

            // Random selection of the MIDI ... and play!
            mfp.MPTK_MidiIndex = Random.Range(0, MidiPlayerGlobal.MPTK_ListMidi.Count);
            mfp.MPTK_Play();

        }

        private void Update()
        {
            if ((transform.position - Target).sqrMagnitude < 0.01f)
                // Defined a new random position when GameObject reach the target (distance < 0.01f)
                SetTarget();

            // At each update, move the Game Object to the target ... smoothly
            transform.position = Vector3.SmoothDamp(transform.position, Target, ref velocity, smoothTime);

            // And rotate, why not?
            transform.Rotate(RotateAmount * Time.unscaledDeltaTime);
        }

        /// <summary>
        /// Defined a new random position
        /// </summary>
        private void SetTarget()
        {
            Target = Random.insideUnitSphere * Radius + StartPosition;
        }
    }
}
