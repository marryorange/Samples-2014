﻿using KLibrary.ComponentModel;
using Leap;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media.Media3D;

namespace LeapSmoothWpf
{
    public class AppModel : NotifyBase
    {
        const int ScreenWidth = 1920;
        const int ScreenHeight = 1080;
        const int MappingScale = 3;

        public double FrameRate
        {
            get { return GetValue<double>(); }
            private set { SetValue(value); }
        }

        public Point3D[] TipPositions
        {
            get { return GetValue<Point3D[]>(); }
            private set { SetValue(value); }
        }

        public Point3D[] StabilizedTipPositions
        {
            get { return GetValue<Point3D[]>(); }
            private set { SetValue(value); }
        }

        public AppModel()
        {
            var controller = new Controller();
            var listener = new FrameListener();
            controller.AddListener(listener);

            AppDomain.CurrentDomain.ProcessExit += (o, e) =>
            {
                controller.RemoveListener(listener);
                listener.Dispose();
                controller.Dispose();
            };

            listener.FrameArrived += listener_FrameArrived;
        }

        void listener_FrameArrived(Leap.Frame frame)
        {
            FrameRate = frame.CurrentFramesPerSecond;

            TipPositions = frame.Pointables
                .Where(p => p.IsValid)
                .Where(p => p.TipPosition.IsValid())
                .Select(p => ToScreenPoint(p.TipPosition))
                .ToArray();

            StabilizedTipPositions = frame.Pointables
                .Where(p => p.IsValid)
                .Where(p => p.StabilizedTipPosition.IsValid())
                .Select(p => ToScreenPoint(p.StabilizedTipPosition))
                .ToArray();
        }

        static Point3D ToScreenPoint(Leap.Vector v)
        {
            return new Point3D(ScreenWidth / 2 + MappingScale * v.x, ScreenHeight - MappingScale * v.y, MappingScale * v.z);
        }
    }
}
