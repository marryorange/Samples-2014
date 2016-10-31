﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Input;

namespace MouseRx2Wpf
{
    public class EventsExtension<TElement> where TElement : UIElement
    {
        public TElement Target { get; }
        public IObservable<IObservable<Vector>> MouseDrag { get; }

        public EventsExtension(TElement target)
        {
            Target = target;

            // Replaces events with IObservable objects.
            var mouseDown = Observable.FromEventPattern<MouseEventArgs>(Target, nameof(UIElement.MouseDown)).Select(e => e.EventArgs);
            var mouseMove = Observable.FromEventPattern<MouseEventArgs>(Target, nameof(UIElement.MouseMove)).Select(e => e.EventArgs);
            var mouseUp = Observable.FromEventPattern<MouseEventArgs>(Target, nameof(UIElement.MouseUp)).Select(e => e.EventArgs);
            var mouseLeave = Observable.FromEventPattern<MouseEventArgs>(Target, nameof(UIElement.MouseLeave)).Select(e => e.EventArgs);
            var mouseDownEnd = mouseUp.Merge(mouseLeave);

            MouseDrag = mouseDown
                .Select(e => e.GetPosition(Target))
                .Select(p0 => mouseMove
                    .TakeUntil(mouseDownEnd)
                    .Select(e => e.GetPosition(Target) - p0));
        }
    }
}
