﻿//------------------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace Microsoft.Samples.Kinect.BackgroundRemovalBasics
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using Microsoft.Kinect;
    using Microsoft.Kinect.Toolkit;
    using Microsoft.Kinect.Toolkit.BackgroundRemoval;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, IDisposable
    {
        /// <summary>
        /// Format we will use for the depth stream
        /// </summary>
        private const DepthImageFormat DepthFormat = DepthImageFormat.Resolution320x240Fps30;

        /// <summary>
        /// Format we will use for the color stream
        /// </summary>
        private const ColorImageFormat ColorFormat = ColorImageFormat.RgbResolution640x480Fps30;

        /// <summary>
        /// Bitmap that will hold color information
        /// </summary>
        private WriteableBitmap foregroundBitmap;

        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensorChooser sensorChooser;

        /// <summary>
        /// Our core library which does background 
        /// </summary>
        private BackgroundRemovedColorStream backgroundRemovedColorStream;

        /// <summary>
        /// Intermediate storage for the skeleton data received from the sensor
        /// </summary>
        private Skeleton[] skeletons;

        /// <summary>
        /// the skeleton that is currently tracked by the app
        /// </summary>
        private int currentlyTrackedSkeletonId;

        /// <summary>
        /// Track whether Dispose has been called
        /// </summary>
        private bool disposed;

        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        public MainWindow()
        {
            this.InitializeComponent();

            // initialize the sensor chooser and UI
            this.sensorChooser = new KinectSensorChooser();
            this.sensorChooser.KinectChanged += this.SensorChooserOnKinectChanged;
            this.sensorChooser.Start();

            var isMouseDown = false;
            var point = default(Point);
            MouseLeftButtonDown += (o, e) =>
            {
                isMouseDown = true;
                point = ScreenManager.GetCursorPosition();
            };
            MouseMove += (o, e) =>
            {
                if (!isMouseDown) return;

                var point_old = point;
                point = ScreenManager.GetCursorPosition();

                var v = point - point_old;
                Left += v.X;
                Top += v.Y;
            };
            MouseLeftButtonUp += (o, e) =>
            {
                isMouseDown = false;
                point = default(Point);
            };
        }

        /// <summary>
        /// Finalizes an instance of the MainWindow class.
        /// This destructor will run only if the Dispose method does not get called.
        /// </summary>
        ~MainWindow()
        {
            this.Dispose(false);
        }

        /// <summary>
        /// Dispose the allocated frame buffers and reconstruction.
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);

            // This object will be cleaned up by the Dispose method.
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Frees all memory associated with the FusionImageFrame.
        /// </summary>
        /// <param name="disposing">Whether the function was called from Dispose.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (null != this.backgroundRemovedColorStream)
                {
                    this.backgroundRemovedColorStream.Dispose();
                    this.backgroundRemovedColorStream = null;
                }

                this.disposed = true;
            }
        }

        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            this.sensorChooser.Stop();
            this.sensorChooser = null;
        }

        /// <summary>
        /// Event handler for Kinect sensor's DepthFrameReady event
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void SensorAllFramesReady(object sender, AllFramesReadyEventArgs e)
        {
            // in the middle of shutting down, or lingering events from previous sensor, do nothing here.
            if (null == this.sensorChooser || null == this.sensorChooser.Kinect || this.sensorChooser.Kinect != sender)
            {
                return;
            }

            try
            {
                using (var depthFrame = e.OpenDepthImageFrame())
                {
                    if (null != depthFrame)
                    {
                        this.backgroundRemovedColorStream.ProcessDepth(depthFrame.GetRawPixelData(), depthFrame.Timestamp);
                    }
                }

                using (var colorFrame = e.OpenColorImageFrame())
                {
                    if (null != colorFrame)
                    {
                        this.backgroundRemovedColorStream.ProcessColor(colorFrame.GetRawPixelData(), colorFrame.Timestamp);
                    }
                }

                using (var skeletonFrame = e.OpenSkeletonFrame())
                {
                    if (null != skeletonFrame)
                    {
                        skeletonFrame.CopySkeletonDataTo(this.skeletons);
                        this.backgroundRemovedColorStream.ProcessSkeleton(this.skeletons, skeletonFrame.Timestamp);
                    }
                }

                this.ChooseSkeleton();
            }
            catch (InvalidOperationException)
            {
                // Ignore the exception. 
            }
        }

        /// <summary>
        /// Handle the background removed color frame ready event. The frame obtained from the background removed
        /// color stream is in RGBA format.
        /// </summary>
        /// <param name="sender">object that sends the event</param>
        /// <param name="e">argument of the event</param>
        private void BackgroundRemovedFrameReadyHandler(object sender, BackgroundRemovedColorFrameReadyEventArgs e)
        {
            using (var backgroundRemovedFrame = e.OpenBackgroundRemovedColorFrame())
            {
                if (backgroundRemovedFrame != null)
                {
                    if (null == this.foregroundBitmap || this.foregroundBitmap.PixelWidth != backgroundRemovedFrame.Width
                        || this.foregroundBitmap.PixelHeight != backgroundRemovedFrame.Height)
                    {
                        this.foregroundBitmap = new WriteableBitmap(backgroundRemovedFrame.Width, backgroundRemovedFrame.Height, 96.0, 96.0, PixelFormats.Bgra32, null);

                        // Set the image we display to point to the bitmap where we'll put the image data
                        this.MaskedColor.Source = this.foregroundBitmap;
                    }

                    // Write the pixel data into our bitmap
                    this.foregroundBitmap.WritePixels(
                        new Int32Rect(0, 0, this.foregroundBitmap.PixelWidth, this.foregroundBitmap.PixelHeight),
                        backgroundRemovedFrame.GetRawPixelData(),
                        this.foregroundBitmap.PixelWidth * sizeof(int),
                        0);
                }
            }
        }

        /// <summary>
        /// Use the sticky skeleton logic to choose a player that we want to set as foreground. This means if the app
        /// is tracking a player already, we keep tracking the player until it leaves the sight of the camera, 
        /// and then pick the closest player to be tracked as foreground.
        /// </summary>
        private void ChooseSkeleton()
        {
            var isTrackedSkeltonVisible = false;
            var nearestDistance = float.MaxValue;
            var nearestSkeleton = 0;

            foreach (var skel in this.skeletons)
            {
                if (null == skel)
                {
                    continue;
                }

                if (skel.TrackingState != SkeletonTrackingState.Tracked)
                {
                    continue;
                }

                if (skel.TrackingId == this.currentlyTrackedSkeletonId)
                {
                    isTrackedSkeltonVisible = true;
                    break;
                }

                if (skel.Position.Z < nearestDistance)
                {
                    nearestDistance = skel.Position.Z;
                    nearestSkeleton = skel.TrackingId;
                }
            }

            if (!isTrackedSkeltonVisible && nearestSkeleton != 0)
            {
                this.backgroundRemovedColorStream.SetTrackedPlayer(nearestSkeleton);
                this.currentlyTrackedSkeletonId = nearestSkeleton;
            }
        }

        /// <summary>
        /// Called when the KinectSensorChooser gets a new sensor
        /// </summary>
        /// <param name="sender">sender of the event</param>
        /// <param name="args">event arguments</param>
        private void SensorChooserOnKinectChanged(object sender, KinectChangedEventArgs args)
        {
            if (args.OldSensor != null)
            {
                try
                {
                    args.OldSensor.AllFramesReady -= this.SensorAllFramesReady;
                    args.OldSensor.DepthStream.Disable();
                    args.OldSensor.ColorStream.Disable();
                    args.OldSensor.SkeletonStream.Disable();

                    // Create the background removal stream to process the data and remove background, and initialize it.
                    if (null != this.backgroundRemovedColorStream)
                    {
                        this.backgroundRemovedColorStream.BackgroundRemovedFrameReady -= this.BackgroundRemovedFrameReadyHandler;
                        this.backgroundRemovedColorStream.Dispose();
                        this.backgroundRemovedColorStream = null;
                    }
                }
                catch (InvalidOperationException)
                {
                    // KinectSensor might enter an invalid state while enabling/disabling streams or stream features.
                    // E.g.: sensor might be abruptly unplugged.
                }
            }

            if (args.NewSensor != null)
            {
                try
                {
                    args.NewSensor.DepthStream.Enable(DepthFormat);
                    args.NewSensor.ColorStream.Enable(ColorFormat);
                    args.NewSensor.SkeletonStream.Enable();

                    this.backgroundRemovedColorStream = new BackgroundRemovedColorStream(args.NewSensor);
                    this.backgroundRemovedColorStream.Enable(ColorFormat, DepthFormat);

                    // Allocate space to put the depth, color, and skeleton data we'll receive
                    if (null == this.skeletons)
                    {
                        this.skeletons = new Skeleton[args.NewSensor.SkeletonStream.FrameSkeletonArrayLength];
                    }

                    // Add an event handler to be called when the background removed color frame is ready, so that we can
                    // composite the image and output to the app
                    this.backgroundRemovedColorStream.BackgroundRemovedFrameReady += this.BackgroundRemovedFrameReadyHandler;

                    // Add an event handler to be called whenever there is new depth frame data
                    args.NewSensor.AllFramesReady += this.SensorAllFramesReady;

                    try
                    {
                        args.NewSensor.DepthStream.Range = DepthRange.Near;
                        args.NewSensor.SkeletonStream.EnableTrackingInNearRange = true;
                    }
                    catch (InvalidOperationException)
                    {
                        // Non Kinect for Windows devices do not support Near mode, so reset back to default mode.
                        args.NewSensor.DepthStream.Range = DepthRange.Default;
                        args.NewSensor.SkeletonStream.EnableTrackingInNearRange = false;
                    }

                    args.NewSensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Seated;
                }
                catch (InvalidOperationException)
                {
                    // KinectSensor might enter an invalid state while enabling/disabling streams or stream features.
                    // E.g.: sensor might be abruptly unplugged.
                }
            }
        }
    }

    public static class ScreenManager
    {
        [DllImport("user32.dll")]
        static extern bool GetCursorPos(ref POINT p);

        [DllImport("user32.dll")]
        static extern bool SetCursorPos(int x, int y);

        public static Point GetCursorPosition()
        {
            var p = new POINT();
            GetCursorPos(ref p);
            return new Point(p.x, p.y);
        }

        public static void SetCursorPosition(Point p)
        {
            SetCursorPos((int)p.X, (int)p.Y);
        }

        [DebuggerDisplay(@"\{{x}, {y}\}")]
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int x;
            public int y;
        }
    }
}