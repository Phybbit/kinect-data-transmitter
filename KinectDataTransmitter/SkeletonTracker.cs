﻿using System.Diagnostics.Eventing.Reader;
using DataConverter;
using Microsoft.Kinect;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JointTrackingState = DataConverter.JointTrackingState;
using JointType = Microsoft.Kinect.JointType;

namespace KinectDataTransmitter
{
    /// <summary>
    /// Class that reads skeleton data from the kinect frames and output it for external usage.
    /// </summary>
    public class SkeletonDataTracker : IDisposable
    {
        private bool _disposed;
        private Dictionary<int, BodyData> _bodyData = new Dictionary<int, BodyData>();

        public SkeletonDataTracker()
        {
        }

        ~SkeletonDataTracker()
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

        public void ResetTracking()
        {
            // not sure if there is anything to dispose.
        }

        public void ProcessData(Body[] bodies)
        {
            if (bodies == null)
            {
                return;
            }

            // Look through the skeletons.
            foreach (Body body in bodies)
            {
                if (body.TrackingId == 0 && body.LeanTrackingState == TrackingState.NotTracked)
                {
                    continue;
                }
                SendSkeletonData(body);
            }
        }

        private void SendSkeletonData(Body body)
        {
            if (body == null)
            {
                return;
            }

            var bodyData = RetrieveOrCreateBodyDataFor((int)body.TrackingId);
            bodyData.TrackingState = (BodyTrackingState)body.LeanTrackingState;

            var jointData = bodyData.JointData;
            for (int i = 0; i < jointData.Length; i++ )
            {
                jointData[i].State = JointTrackingState.NotTracked;
            }
            foreach (Joint joint in body.Joints.Values)
            {
                int type = (int)joint.JointType;
                jointData[type].State = (JointTrackingState) joint.TrackingState;
                jointData[type].PositionX = joint.Position.X;
                jointData[type].PositionY = joint.Position.Y;
                jointData[type].PositionZ = joint.Position.Z;
            }

            foreach (JointOrientation jointOrientation in body.JointOrientations.Values)
            {
                int type = (int)jointOrientation.JointType;
                jointData[type].QuaternionX = jointOrientation.Orientation.X;
                jointData[type].QuaternionY = jointOrientation.Orientation.Y;
                jointData[type].QuaternionZ = jointOrientation.Orientation.Z;
                jointData[type].QuaternionW = jointOrientation.Orientation.W;
            }
            Console.WriteLine(Converter.EncodeSkeletonData(bodyData));
        }

        private BodyData RetrieveOrCreateBodyDataFor(int skeletonId)
        {
            if (_bodyData.ContainsKey(skeletonId))
            {
                return _bodyData[skeletonId];
            }
            var bodyData = SetupBodyData(skeletonId);
            _bodyData[skeletonId] = bodyData;
            return bodyData;
        }


        private BodyData SetupBodyData(int userId)
        {
            var bodyData = new BodyData();
            bodyData.UserId = userId;
            const int jointsNumber = (int)DataConverter.JointType.Count;
            var jointData = new JointData[jointsNumber];
            for (int i = 0; i < jointsNumber; i++)
            {
                jointData[i] = new JointData();
                jointData[i].JointId = (DataConverter.JointType)i;
            }
            bodyData.JointData = jointData;
            return bodyData;
        }
    }
}
