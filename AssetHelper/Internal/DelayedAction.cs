using System;
using System.Collections.Generic;

namespace Silksong.AssetHelper.Internal;

/// <summary>
/// Class representing an occurence that may happen in the future or may have already happened.
/// </summary>
internal class DelayedAction
{

    private List<Action> _subscribers = [];

    /// <summary>
    /// Whether or not this instance has been activated.
    /// </summary>
    public bool Activated { get; private set; } = false;

    public void Activate()
    {
        if (!Activated)
        {
            Activated = true;
            foreach (Action a in _subscribers)
            {
                ActionUtil.SafeInvoke(a);
            }
            _subscribers.Clear();
        }
    }

    /// <summary>
    /// If this instance has been activated, invoke the argument now.
    /// Otherwise, invoke it when this instance is activated.
    /// </summary>
    /// <param name="toInvoke"></param>
    public void Subscribe(Action toInvoke)
    {
        if (Activated)
        {
            ActionUtil.SafeInvoke(toInvoke);
        }
        else
        {
            _subscribers.Add(toInvoke);
        }
    }
}
