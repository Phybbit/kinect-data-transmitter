using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using DataConverter;
using Microsoft.Kinect;


namespace KinectDataTransmitter
{
    /// <summary>
    /// Class that uses the Face Tracking SDK to display a face mask for
    /// tracked skeletons
    /// </summary>
    public class FaceDataTracker : IDisposable
    {
        private const uint MaxMissedFrames = 100;
        private readonly Dictionary<int, SkeletonFaceTracker> _trackedSkeletons = new Dictionary<int, SkeletonFaceTracker>();
        private bool _disposed;


        ~FaceDataTracker()
        {
            Dispose(false);
        }


        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                ResetTracking();
                _disposed = true;
            }
        }

        //public void ProcessData(KinectSensor kinectSensor, ColorImageFormat colorImageFormat,
        //                         byte[] colorImage, DepthImageFormat depthImageFormat, short[] depthImage,
        //                         Skeleton[] skeletons, int skeletonFrameNumber)
        //{
        //    if (skeletons == null)
        //    {
        //        return;
        //    }

        //    // Update the list of trackers and the trackers with the current frame information
        //    foreach (Skeleton skeleton in skeletons)
        //    {
        //        if (skeleton.TrackingState == SkeletonTrackingState.Tracked ||
        //            skeleton.TrackingState == SkeletonTrackingState.PositionOnly)
        //        {
        //            // We want keep a record of any skeleton, tracked or untracked.
        //            if (!this._trackedSkeletons.ContainsKey(skeleton.TrackingId))
        //            {
        //                this._trackedSkeletons.Add(skeleton.TrackingId, new SkeletonFaceTracker());
        //            }

        //            // Give each tracker the upated frame.
        //            SkeletonFaceTracker skeletonFaceTracker;
        //            if (this._trackedSkeletons.TryGetValue(skeleton.TrackingId, out skeletonFaceTracker))
        //            {
        //                skeletonFaceTracker.OnFrameReady(kinectSensor, colorImageFormat, colorImage, depthImageFormat,
        //                                                 depthImage, skeleton);
        //                skeletonFaceTracker.LastTrackedFrame = skeletonFrameNumber;
        //            }
        //        }
        //    }

        //    RemoveOldTrackers(skeletonFrameNumber);
        //}

        /// <summary>
        /// Clear out any trackers for skeletons we haven't heard from for a while
        /// </summary>
        private void RemoveOldTrackers(int currentFrameNumber)
        {
            var trackersToRemove = new List<int>();

            foreach (var tracker in this._trackedSkeletons)
            {
                uint missedFrames = (uint)currentFrameNumber - (uint)tracker.Value.LastTrackedFrame;
                if (missedFrames > MaxMissedFrames)
                {
                    // There have been too many frames since we last saw this skeleton
                    trackersToRemove.Add(tracker.Key);
                }
            }

            foreach (int trackingId in trackersToRemove)
            {
                this.RemoveTracker(trackingId);
            }
        }

        private void RemoveTracker(int trackingId)
        {
            this._trackedSkeletons[trackingId].Dispose();
            this._trackedSkeletons.Remove(trackingId);
        }

        public void ResetTracking()
        {
            foreach (int trackingId in new List<int>(this._trackedSkeletons.Keys))
            {
                RemoveTracker(trackingId);
            }
        }

        private class SkeletonFaceTracker : IDisposable
        {
            //private FaceTracker _faceTracker;
            private bool _lastFaceTrackSucceeded;
            private BodyTrackingState _bodyTrackingState;

            

            public int LastTrackedFrame { get; set; }

            public void Dispose()
            {
                //if (this._faceTracker != null)
                //{
                //    this._faceTracker.Dispose();
                //    this._faceTracker = null;
                //}
            }


            /// <summary>
            /// Updates the face tracking information for this skeleton
            /// </summary>
    //        internal void OnFrameReady(KinectSensor kinectSensor, ColorImageFormat colorImageFormat, byte[] colorImage, DepthImageFormat depthImageFormat, short[] depthImage, Body bodyOfInterest)
    //        {
    //            this._bodyTrackingState = (BodyTrackingState)bodyOfInterest.LeanTrackingState;

    //            if (this._bodyTrackingState != BodyTrackingState.Tracked)
    //            {
    //                // nothing to do with an untracked skeleton.
    //                return;
    //            }

    //            if (this._faceTracker == null)
    //            {
    //                try
    //                {
    //                    this._faceTracker = new FaceTracker(kinectSensor);
    //                }
    //                catch (InvalidOperationException e)
    //                {
    //                    // During some shutdown scenarios the FaceTracker
    //                    // is unable to be instantiated.  Catch that exception
    //                    // and don't track a face.
    //                    Console.WriteLine(Converter.EncodeError("FaceTracker.OnFrameReady - creating a new FaceTracker threw an InvalidOperationException: " + e.Message));
    //                    this._faceTracker = null;
    //                }
    //            }

    //            if (this._faceTracker != null)
    //            {
    //                FaceTrackFrame frame = this._faceTracker.Track(
    //                    colorImageFormat, colorImage, depthImageFormat, depthImage, bodyOfInterest);

    //                this._lastFaceTrackSucceeded = frame.TrackSuccessful;
    //                if (this._lastFaceTrackSucceeded)
    //                {
    //                    var animUnits = frame.GetAnimationUnitCoefficients();
    //                    var pos = frame.Translation;
    //                    var rot = frame.Rotation;
    //                    var faceData = new FaceData();
    //                    faceData.Au0 = animUnits[0];
    //                    faceData.Au1 = animUnits[1];
    //                    faceData.Au2 = animUnits[2];
    //                    faceData.Au3 = animUnits[3];
    //                    faceData.Au4 = animUnits[4];
    //                    faceData.Au5 = animUnits[5];
    //                    faceData.PosX = pos.X;
    //                    faceData.PosY = pos.Y;
    //                    faceData.PosZ = pos.Z;
    //                    faceData.RotX = rot.X;
    //                    faceData.RotY = rot.Y;
    //                    faceData.RotZ = rot.Z;
    //                    var data = Converter.EncodeFaceTrackingData(faceData);
    //                    Console.WriteLine(data);
    //                }
    //            }
    //        }
        }
    }
}
