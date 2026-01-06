using System;

namespace Unity.VisualScripting
{
    /// <summary>
    /// Called every frame.
    /// </summary>
    [UnitCategory("Events/Lifecycle")]
    [UnitOrder(3)]
    [UnitTitle("On Update")]
    public sealed class Update : SelfEventUnit<EmptyEventArgs>
    {
        public override Type MessageListenerType => typeof(UnityOnUpdateListener);

        protected override string hookName => EventHooks.Update;
    }
}
