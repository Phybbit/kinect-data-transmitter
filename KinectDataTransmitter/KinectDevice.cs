using System;
using DataConverter;
using Microsoft.Kinect;
using Microsoft.Kinect.Toolkit;
using System.IO.MemoryMappedFiles;

namespace KinectDataTransmitter
{
    public class KinectDevice
    {
        public readonly KinectSensorChooser SensorChooser = new KinectSensorChooser();
        private ColorImageFormat _colorImageFormat = ColorImageFormat.Undefined;
        private byte[] _colorImageData;
        private DepthImageFormat _depthImageFormat = DepthImageFormat.Undefined;
        private short[] _depthImageData;
        private Skeleton[] _skeletons;

        private SkeletonDataTracker _skeletonTracker;
        private FaceDataTracker _faceTracker;
        private InteractionDataTracker _interactionTracker;
        private StreamWriter _streamWriter;
        private KinectSensor _currentSensor;
        private long _skeletonFrameTimestamp;

        public bool IsTrackingSkeletons { get; set; }
        public bool IsTrackingFace { get; set; }
        public bool IsTrackingInteraction { get; set; }
        public bool IsWritingColorStream { get; set; }
        public bool IsWritingDepthStream { get; set; }
        public bool IsUsingInfraRedStream { get; set; }

        public void Initialize()
        {
            _faceTracker = new FaceDataTracker();
            _skeletonTracker = new SkeletonDataTracker();
            _streamWriter = new StreamWriter();
            _interactionTracker = new InteractionDataTracker();

            SensorChooser.KinectChanged += OnKinectChanged;

            SensorChooser.PropertyChanged += (sender, args) =>
                { if (args.PropertyName == "Status") HandleSensorChooserStatusChanged(); };
            try
            {
                SensorChooser.Start();
            }
            catch (Exception e)
            {
                Console.WriteLine(Converter.EncodeError(e.Message));
                throw;
            }
        }

        private void HandleSensorChooserStatusChanged()
        {
            string message = string.Format("The kinect sensor is status changed to: {0}.", SensorChooser.Status);
            switch (SensorChooser.Status)
            {
                case ChooserStatus.SensorInitializing:
                case ChooserStatus.SensorStarted:
                    Console.WriteLine(Converter.EncodeInfo(message));
                    break;
                default:
                    Console.WriteLine(Converter.EncodeError(message));
                    break;
            }
        }

        private void OnKinectChanged(object sender, KinectChangedEventArgs kinectChangedEventArgs)
        {
            KinectSensor oldSensor = kinectChangedEventArgs.OldSensor;
            KinectSensor newSensor = kinectChangedEventArgs.NewSensor;

            if (oldSensor != null)
            {
                oldSensor.AllFramesReady -= OnAllFramesReady;
                oldSensor.ColorStream.Disable();
                oldSensor.DepthStream.Disable();
                oldSensor.DepthStream.Range = DepthRange.Default;
                oldSensor.SkeletonStream.Disable();
                oldSensor.SkeletonStream.EnableTrackingInNearRange = false;
                oldSensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Default;
                _currentSensor = null;

                _faceTracker.ResetTracking();
                _skeletonTracker.ResetTracking();
                _interactionTracker.ResetTracking();
            }

            if (newSensor != null)
            {
                try
                {
                    // InteractionStream needs 640x480 depth data stream
                    var colorImageFormat = ColorImageFormat.RgbResolution640x480Fps30;
                    if (IsUsingInfraRedStream)
                    {
                        colorImageFormat = ColorImageFormat.InfraredResolution640x480Fps30;
                    }
                    newSensor.ColorStream.Enable(colorImageFormat);
                    newSensor.DepthStream.Enable(DepthImageFormat.Resolution640x480Fps30);
                    try
                    {
                        // This will throw on non Kinect For Windows devices.
                        // Interactions work better in near range
                        newSensor.DepthStream.Range = DepthRange.Near;
                        newSensor.SkeletonStream.EnableTrackingInNearRange = true;
                    }
                    catch (InvalidOperationException)
                    {
                        newSensor.DepthStream.Range = DepthRange.Default;
                        newSensor.SkeletonStream.EnableTrackingInNearRange = false;
                    }

                    TransformSmoothParameters smoothingParam = new TransformSmoothParameters();
                    {
                        smoothingParam.Smoothing = 0.5f;
                        smoothingParam.Correction = 0.5f;
                        smoothingParam.Prediction = 0.5f;
                        smoothingParam.JitterRadius = 0.05f;
                        smoothingParam.MaxDeviationRadius = 0.04f;
                    }

                    newSensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Default;
                    newSensor.SkeletonStream.Enable(smoothingParam);
                    _currentSensor = newSensor;
                    _interactionTracker.InitializeTracking(_currentSensor);
                    newSensor.AllFramesReady += OnAllFramesReady;
                }
                catch (InvalidOperationException e)
                {
                    // This exception can be thrown when we are trying to
                    // enable streams on a device that has gone away.  This
                    // can occur, say, in app shutdown scenarios when the sensor
                    // goes away between the time it changed status and the
                    // time we get the sensor changed notification.
                    //
                    // Behavior here is to just eat the exception and assume
                    // another notification will come along if a sensor
                    // comes back.
                    Console.WriteLine(Converter.EncodeError("KinectDevice.OnKinectChanged: threw an exception. It might be safe to ignore this if the app was shuting down. Message: " + e.Message));
                }
            }
        }


        private void OnAllFramesReady(object sender, AllFramesReadyEventArgs allFramesReadyEventArgs)
        {
            ColorImageFrame colorImageFrame = null;
            DepthImageFrame depthImageFrame = null;
            SkeletonFrame skeletonFrame = null;

            bool openColorFrame = (IsTrackingFace || IsWritingColorStream || IsUsingInfraRedStream);
            bool openDepthFrame = (IsTrackingFace || IsWritingDepthStream);
            bool openSkeletonFrame = (IsTrackingFace || IsTrackingSkeletons);

            try
            {
                if (openColorFrame)
                {
                    colorImageFrame = allFramesReadyEventArgs.OpenColorImageFrame();
                }
                if (openDepthFrame)
                {
                    depthImageFrame = allFramesReadyEventArgs.OpenDepthImageFrame();
                }
                if (openSkeletonFrame)
                {
                    skeletonFrame = allFramesReadyEventArgs.OpenSkeletonFrame();
                }

                ProcessFrame(colorImageFrame);
                ProcessFrame(depthImageFrame);
                ProcessFrame(skeletonFrame);

                int skeletonFrameNumber = (skeletonFrame != null ? skeletonFrame.FrameNumber : -1);
                ProcessData(skeletonFrameNumber);
            }
            finally
            {
                if (colorImageFrame != null)
                {
                    colorImageFrame.Dispose();
                }

                if (depthImageFrame != null)
                {
                    depthImageFrame.Dispose();
                }

                if (skeletonFrame != null)
                {
                    skeletonFrame.Dispose();
                }
            }
        }

        private void ProcessFrame(ColorImageFrame colorImageFrame)
        {
            if (colorImageFrame == null)
            {
                return;
            }

            // Make a copy of the color frame for displaying.
            var hasNewFormat = _colorImageFormat != colorImageFrame.Format;
            if (hasNewFormat)
            {
                _colorImageFormat = colorImageFrame.Format;
                _colorImageData = new byte[colorImageFrame.PixelDataLength];
            }

            colorImageFrame.CopyPixelDataTo(_colorImageData);
        }

        private void ProcessFrame(DepthImageFrame depthImageFrame)
        {
            if (depthImageFrame == null)
            {
                return;
            }

            // Make a copy of the color frame for displaying.
            var hasNewFormat = _depthImageFormat != depthImageFrame.Format;
            if (hasNewFormat)
            {
                _depthImageFormat = depthImageFrame.Format;
                _depthImageData = new short[depthImageFrame.PixelDataLength];
            }

            if (IsTrackingInteraction)
            {
                _interactionTracker.SensorDepthFrameReady(depthImageFrame);
            }
            depthImageFrame.CopyPixelDataTo(_depthImageData);
        }

        private void ProcessFrame(SkeletonFrame skeletonFrame)
        {
            if (skeletonFrame == null)
            {
                return;
            }

            if (_skeletons == null || _skeletons.Length != skeletonFrame.SkeletonArrayLength)
            {
                _skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
            }
            skeletonFrame.CopySkeletonDataTo(_skeletons);
            _skeletonFrameTimestamp = skeletonFrame.Timestamp;
        }

        private void ProcessData(int skeletonFrameNumber)
        {
            if (IsTrackingInteraction)
            {
                _interactionTracker.ProcessData(_skeletons, _skeletonFrameTimestamp);
            }
            if (IsTrackingFace)
            {
                _faceTracker.ProcessData(_currentSensor, _colorImageFormat, _colorImageData,
                                         _depthImageFormat, _depthImageData, _skeletons,
                                         skeletonFrameNumber);
            }

            if (IsTrackingSkeletons)
            {
                _skeletonTracker.ProcessData(_skeletons);
            }

            if (IsWritingColorStream)
            {
                _streamWriter.ProcessColorData(_colorImageData);
            }

            if (IsWritingDepthStream)
            {
                _streamWriter.ProcessDepthData(_depthImageData);
            }

            if (IsUsingInfraRedStream)
            {
                _streamWriter.ProcessInfraRedData(_colorImageData);
            }
        }
    }
}
