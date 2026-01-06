using UnityEngine;

namespace Unity.VisualScripting
{
    [AddComponentMenu("")]
    public sealed class UnityOnUpdateListener : MessageListener
    {
        private void Update()
        {
            EventBus.Trigger(EventHooks.Update, gameObject);
        }
    }
}
