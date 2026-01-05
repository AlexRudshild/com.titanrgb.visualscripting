using System.Collections;
using UnityEngine;

namespace Unity.VisualScripting
{
    /// <summary>
    /// Delays flow by waiting until the next fixed update.
    /// </summary>
    [UnitTitle("Wait For Next Fixed Update")]
    [UnitOrder(4)]
    public class WaitForFixedUpdateUnit : WaitUnit
    {
        protected override IEnumerator Await(Flow flow)
        {
            yield return new WaitForFixedUpdate();

            yield return exit;
        }
    }
}