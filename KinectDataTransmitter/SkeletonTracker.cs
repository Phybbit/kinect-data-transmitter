using System.Diagnostics.Eventing.Reader;
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
        
        public void ProcessData(Skeleton[] skeletons)
        {
            if (skeletons == null)
            {
                return;
            }

            // Look through the skeletons.
            int playerIndex = -1;
            foreach (var skeleton in skeletons)
            {
                playerIndex++;
                if (skeleton.TrackingId == 0 && skeleton.TrackingState == SkeletonTrackingState.NotTracked)
                {
                    continue;
                }
                SendSkeletonData(skeleton, playerIndex);
            }
        }

        private void SendSkeletonData(Skeleton skeleton, int playerIndex)
        {
            if (skeleton == null)
            {
                return;
            }

            if (skeleton.TrackingId > 5)
            {
            }

            var bodyData = RetrieveOrCreateBodyDataFor(skeleton.TrackingId);
            bodyData.TrackingState = (BodyTrackingState)skeleton.TrackingState;
            bodyData.PlayerIndex = playerIndex;

            var jointData = bodyData.JointData;
            for (int i = 0; i < jointData.Length; i++ )
            {
                jointData[i].State = JointTrackingState.NotTracked;
            }

            foreach (Joint joint in skeleton.Joints)
            {
                int type = (int)joint.JointType;
                jointData[type].State = (JointTrackingState) joint.TrackingState;
                jointData[type].PositionX = joint.Position.X;
                jointData[type].PositionY = joint.Position.Y;
                jointData[type].PositionZ = joint.Position.Z;
            }

            foreach (BoneOrientation boneOrientations in skeleton.BoneOrientations)
            {
                int type = (int)boneOrientations.EndJoint;
                jointData[type].QuaternionX = boneOrientations.HierarchicalRotation.Quaternion.X;
                jointData[type].QuaternionY = boneOrientations.HierarchicalRotation.Quaternion.Y;
                jointData[type].QuaternionZ = boneOrientations.HierarchicalRotation.Quaternion.Z;
                jointData[type].QuaternionW = boneOrientations.HierarchicalRotation.Quaternion.W;
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
            const int jointsNumber = (int)DataConverter.JointType.NumberOfJoints;
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
