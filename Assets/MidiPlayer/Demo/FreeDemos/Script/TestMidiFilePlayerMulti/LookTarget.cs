using UnityEngine;
namespace DemoMPTK
{
    /// <summary>@brief
    /// Used in TestMidiFilePlayerMulti demo. Useful to draw gizmos to debug camera path (no interest directly for MPTK)
    /// </summary>
    public class LookTarget : MonoBehaviour
    {
        void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(transform.position, .25f);
        }
    }
}
