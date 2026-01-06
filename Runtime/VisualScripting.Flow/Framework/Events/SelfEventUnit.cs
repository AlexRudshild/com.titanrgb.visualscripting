using System;
using UnityEngine;

namespace Unity.VisualScripting
{
    public abstract class SelfEventUnit<TArgs> : EventUnit<TArgs>, IGameObjectEventUnit
    {
        protected sealed override bool register => true;

        public abstract Type MessageListenerType { get; }

        public override IGraphElementData CreateData()
        {
            return new Data();
        }

        public override EventHook GetHook(GraphReference reference)
        {
            if (!reference.hasData)
            {
                return hookName;
            }

            return new EventHook(hookName, reference.gameObject);
        }

        protected virtual string hookName => throw new InvalidImplementationException($"Missing event hook for '{this}'.");

        public override void StartListening(GraphStack stack)
        {
            if (UnityThread.allowsAPI)
            {
                if (MessageListenerType == null) // can't be null. SelfEven need a message listener
                    throw new InvalidImplementationException("SelfEventUnit require message listener");
            }

            MessageListener.AddTo(MessageListenerType, stack.gameObject);

            base.StartListening(stack);
        }
    }
}
