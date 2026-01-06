using System;

namespace Unity.VisualScripting
{
    /// <summary>
    /// Called every fixed framerate frame.
    /// </summary>
    [UnitCategory("Events/Lifecycle")]
    [UnitOrder(4)]
    [UnitTitle("On Fixed Update")]
    public sealed class FixedUpdate : SelfEventUnit<EmptyEventArgs>
    {
        public override Type MessageListenerType => typeof(UnityOnFixedUpdateListener);

        protected override string hookName => EventHooks.FixedUpdate;
    }
}
