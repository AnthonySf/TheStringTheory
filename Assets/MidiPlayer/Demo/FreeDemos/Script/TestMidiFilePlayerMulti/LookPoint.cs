using UnityEngine;
namespace DemoMPTK
{

    /// <summary>@brief
    /// Used in TestMidiFilePlayerMulti demo. Useful to draw gizmos to debug camera path (no interest directly for MPTK)
    /// </summary>
    public class LookPoint : MonoBehaviour
    {
        void OnDrawGizmos()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, .25f);
        }
    }
}
