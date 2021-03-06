﻿using System;
using DataConverter;
using Microsoft.Kinect;
using System.IO.MemoryMappedFiles;

namespace KinectDataTransmitter
{
    public class KinectDevice
    {
        private readonly int bytesPerColorPixel = (int)ColorImageFormat.Bgra;

        public KinectSensor Sensor = null;
        private byte[] _colorImageData;
        private ushort[] _depthImageData;
        private ushort[] _infraredImageData;
        private Body[] bodies = null;

        private CoordinateMapper coordinateMapper = null;

        private MultiSourceFrameReader multiSourceFrameReader = null;

        private SkeletonDataTracker _skeletonTracker;
        private FaceDataTracker _faceTracker;
        private InteractionDataTracker _interactionTracker;
        private StreamWriter _streamWriter;
        //private KinectSensor _currentSensor;
        //private long _skeletonFrameTimestamp;

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
            try
            {
                this.Sensor = KinectSensor.Default;

                if (this.Sensor != null)
                {
                    // get the coordinate mapper
                    this.coordinateMapper = this.Sensor.CoordinateMapper;

                    // open the sensor
                    this.Sensor.Open();

                    this.bodies = new Body[this.Sensor.BodyFrameSource.BodyCount];
                    
                    // open the reader for the frames


                    FrameSourceTypes frameSourceTypes = FrameSourceTypes.Infrared;
                    if (IsTrackingSkeletons || true)
                    {
                        frameSourceTypes = frameSourceTypes | FrameSourceTypes.Body;
                    }

                    if (IsWritingColorStream)
                    {
                        frameSourceTypes = frameSourceTypes | FrameSourceTypes.Color;
                    }

                        if (IsWritingDepthStream || true)
                    {
                        frameSourceTypes = frameSourceTypes | FrameSourceTypes.Depth;
                    }

                    this.multiSourceFrameReader = this.Sensor.OpenMultiSourceFrameReader(frameSourceTypes);

                    FrameDescription colorFrameDescription = this.Sensor.ColorFrameSource.FrameDescription;
                    FrameDescription infraredFrameDescription = this.Sensor.InfraredFrameSource.FrameDescription;
                    FrameDescription depthFrameDescription = this.Sensor.DepthFrameSource.FrameDescription;

                    this._colorImageData = new byte[colorFrameDescription.Width * colorFrameDescription.Height * this.bytesPerColorPixel];
                    this._infraredImageData = new ushort[infraredFrameDescription.Width * infraredFrameDescription.Height];
                    this._depthImageData = new ushort[depthFrameDescription.Width * depthFrameDescription.Height];
                    //Console.WriteLine("exist sensor");
                }
                else
                {
                    //Console.WriteLine("no sensor");
                }

                this.setReaders();
            }
            catch (Exception e)
            {
                Console.WriteLine(Converter.EncodeError(e.Message));
                throw;
            }

            this.Sensor.PropertyChanged += (sender, args) =>
                { if (args.PropertyName == "Status") HandleSensorChooserStatusChanged(); };

        }

        private void setReaders()
        {
            if (this.multiSourceFrameReader != null)
            {
                this.multiSourceFrameReader.MultiSourceFrameArrived += this.Reader_MultiSourceFrameArrived;
            }
        }

        private void Reader_MultiSourceFrameArrived(object sender, MultiSourceFrameArrivedEventArgs e)
        {
            MultiSourceFrameReference frameReference = e.FrameReference;
            MultiSourceFrame multiSourceFrame = frameReference.AcquireFrame();
            DepthFrame depthFrame = null;
            ColorFrame colorFrame = null;
            InfraredFrame infraredFrame = null;
            BodyFrame bodyFrame = null;

            try
            {
                multiSourceFrame = frameReference.AcquireFrame();

                if (multiSourceFrame != null)
                {
                    // MultiSourceFrame is IDisposable
                    using (multiSourceFrame)
                    {
                        DepthFrameReference depthFrameReference = multiSourceFrame.DepthFrameReference;
                        InfraredFrameReference infraredFrameReference = multiSourceFrame.InfraredFrameReference;
                        ColorFrameReference colorFrameReference = multiSourceFrame.ColorFrameReference;
                        BodyFrameReference bodyFrameReference = multiSourceFrame.BodyFrameReference;

                        depthFrame = depthFrameReference.AcquireFrame();
                        infraredFrame = infraredFrameReference.AcquireFrame();
                        colorFrame = colorFrameReference.AcquireFrame();
                        bodyFrame  = bodyFrameReference.AcquireFrame();

                        long frameNumber = -1;
                        if (depthFrame != null)
                        {
                            ProcessFrame(depthFrame);

                            frameNumber = depthFrame.RelativeTime;
                        }
                        if (infraredFrame != null)
                        {
                            ProcessFrame(infraredFrame);

                            frameNumber = infraredFrame.RelativeTime;
                        }
                        if (colorFrame != null)
                        {
                            ProcessFrame(colorFrame);

                            frameNumber = colorFrame.RelativeTime;
                        }
                        if (bodyFrame != null)
                        {
                            ProcessFrame(bodyFrame);

                            frameNumber = bodyFrame.RelativeTime;
                        }

                        if ((depthFrame != null) || (colorFrame != null) || (bodyFrame != null))
                        {
                            ProcessData(frameNumber);
                        }

                    }
                }
            }
            catch (Exception)
            {
                // ignore if the frame is no longer available
            }
            finally
            {
                // MultiSourceFrame, DepthFrame, ColorFrame, BodyIndexFrame are IDispoable
                if (depthFrame != null)
                {
                    depthFrame.Dispose();
                    depthFrame = null;
                }

                if (colorFrame != null)
                {
                    colorFrame.Dispose();
                    colorFrame = null;
                }

                if (infraredFrame != null)
                {
                    infraredFrame.Dispose();
                    colorFrame = null;
                }

                if (bodyFrame != null)
                {
                    bodyFrame.Dispose();
                    bodyFrame = null;
                }

                if (multiSourceFrame != null)
                {
                    multiSourceFrame.Dispose();
                    multiSourceFrame = null;
                }
            }
        }
        /// <summary>
        /// Handles the body frame data arriving from the sensor
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void Reader_FrameArrived(object sender, BodyFrameArrivedEventArgs e)
        {
            BodyFrameReference frameReference = e.FrameReference;

            try
            {
                BodyFrame frame = frameReference.AcquireFrame();

                if (frame != null)
                {
                    // BodyFrame is IDisposable
                    using (frame)
                    {
                        ProcessFrame(frame);

                        long skeletonFrameNumber = (frame != null ? frame.RelativeTime : -1);
                            ProcessData(skeletonFrameNumber);
                        }
                    
                }
            }
            catch (Exception)
            {
                // ignore if the frame is no longer available
            }
        }

        private void Reader_FrameArrived(object sender, DepthFrameArrivedEventArgs e)
        {
            DepthFrameReference frameReference = e.FrameReference;


            try
            {
                DepthFrame frame = frameReference.AcquireFrame();

                if (frame != null)
                {
                    // DepthFrame is IDisposable
                    using (frame)
                    {
                        ProcessFrame(frame);
                        long skeletonFrameNumber = (frame != null ? frame.RelativeTime : -1);
                        ProcessData(skeletonFrameNumber);
                    }
                }
            }
            catch (Exception)
            {
                // ignore if the frame is no longer available
            }
        }

        private void Reader_FrameArrived(object sender, ColorFrameArrivedEventArgs e)
        {
            ColorFrameReference frameReference = e.FrameReference;

            try
            {
                ColorFrame frame = frameReference.AcquireFrame();

                if (frame != null)
                {
                    // ColorFrame is IDisposable
                    using (frame)
                    {
                        ProcessFrame(frame);
                        long skeletonFrameNumber = (frame != null ? frame.RelativeTime : -1);
                        ProcessData(skeletonFrameNumber);
                    }
                }
            }
            catch (Exception)
            {
                // ignore if the frame is no longer available
            }
        }
    


        private void HandleSensorChooserStatusChanged()
        {
            string message = string.Format("The kinect sensor is status changed to: {0}.", this.Sensor.Status);
            switch (this.Sensor.Status)
            {
                case KinectStatus.Initializing:
                case KinectStatus.Connected:
                    Console.WriteLine(Converter.EncodeInfo(message));
                    break;
                default:
                    Console.WriteLine(Converter.EncodeError(message));
                    break;
            }
        }

        private void ProcessFrame(ColorFrame colorFrame)
        {
            if (colorFrame == null)
            {
                return;
            }

            FrameDescription frameDescription = colorFrame.FrameDescription;
            if (colorFrame.RawColorImageFormat == ColorImageFormat.Bgra)
            {
                colorFrame.CopyRawFrameDataToArray(this._colorImageData);
            }
            else
            {
                colorFrame.CopyConvertedFrameDataToArray(this._colorImageData, ColorImageFormat.Bgra);
            }
        }

        private void ProcessFrame(InfraredFrame infraredFrame)
        {
            if (infraredFrame == null)
            {
                return;
            }

            infraredFrame.CopyFrameDataToArray(this._infraredImageData);
            _streamWriter.ProcessInfraRedData(_infraredImageData);
        }

        private void ProcessFrame(DepthFrame depthFrame)
        {
            if (depthFrame == null)
            {
                return;
            }

            depthFrame.CopyFrameDataToArray(this._depthImageData);
            //_interactionTracker.SensorDepthFrameReady(depthImageFrame);
        }

        private void ProcessFrame(BodyFrame bodyFrame)
        {
            if (bodyFrame == null)
            {
                return;
            }

            // The first time GetAndRefreshBodyData is called, Kinect will allocate each Body in the array.
            // As long as those body objects are not disposed and not set to null in the array,
            // those body objects will be re-used.
            bodyFrame.GetAndRefreshBodyData(this.bodies);
        }

        private void ProcessData(long skeletonFrameNumber)
        {
            if (IsTrackingFace)
            {
                _interactionTracker.ProcessData(this.bodies);
            }


            if (IsTrackingSkeletons)
            {
                _skeletonTracker.ProcessData(this.bodies);
            }

            //if (IsWritingColorStream)
            //{
            //    _streamWriter.ProcessColorData(_colorImageData);
            //}

            //if (IsWritingDepthStream)
            //{
            //    _streamWriter.ProcessDepthData(_depthImageData);
            //}

            //if (IsUsingInfraRedStream)
            //{
            //    _streamWriter.ProcessInfraRedData(_colorImageData);
            //}
        }

        public void OverrideHandTracking(ulong trackingId)
        {
            this.Sensor.BodyFrameSource.OverrideHandTracking(trackingId);
        }

        public void SetMode(KinectDeviceMode mode)
        {
            IsTrackingSkeletons = ((mode & KinectDeviceMode.Body) != KinectDeviceMode.None);
        }
    }
}
