using UnityEngine;

namespace Unity.VisualScripting
{
    [AddComponentMenu("")]
    public sealed class UnityOnFixedUpdateListener : MessageListener
    {
        private void FixedUpdate()
        {
            EventBus.Trigger(EventHooks.FixedUpdate, gameObject);
        }
    }
}
