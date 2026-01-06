using UnityEngine;

namespace Unity.VisualScripting
{
    [AddComponentMenu("")]
    public sealed class UnityOnLateUpdateListener : MessageListener
    {
        private void LateUpdate()
        {
            EventBus.Trigger(EventHooks.LateUpdate, gameObject);
        }
    }
}
