using DataConverter;
using Microsoft.Kinect;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JointType = Microsoft.Kinect.JointType;

namespace KinectDataTransmitter
{
    /// <summary>
    /// Class that reads skeleton data from the kinect frames and output it for external usage.
    /// </summary>
    public class SkeletonDataTracker : IDisposable
    {
        private bool _disposed;
        private readonly JointData[] _jointData;

        public SkeletonDataTracker()
        {
            const int jointsNumber = (int)DataConverter.JointType.NumberOfJoints;
            _jointData = new JointData[jointsNumber];
            for (int i = 0; i < jointsNumber; i++)
            {
                _jointData[i] = new JointData();
                _jointData[i].JointId = (DataConverter.JointType)i;
            }
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

            // Assume no nearest skeleton and that the nearest skeleton is a long way away.
            Skeleton nearestSkeleton = null;
            var nearestSqrDistance = double.MaxValue;

            // Look through the skeletons.
            foreach (var skeleton in skeletons)
            {
                // Only consider tracked skeletons.
                if (skeleton.TrackingState == SkeletonTrackingState.Tracked)
                {
                    // Find the distance squared.
                    var sqrDistance = (skeleton.Position.X*skeleton.Position.X) +
                                      (skeleton.Position.Y*skeleton.Position.Y) +
                                      (skeleton.Position.Z*skeleton.Position.Z);

                    // Is the new distance squared closer than the nearest so far?
                    if (sqrDistance < nearestSqrDistance)
                    {
                        // Use the new values.
                        nearestSqrDistance = sqrDistance;
                        nearestSkeleton = skeleton;
                    }
                }
               
            }
            
            SendSkeletonData(nearestSkeleton);
        }

        private void SendSkeletonData(Skeleton skeleton)
        {
            if (skeleton == null)
            {
                return;
            }

            for (int i = 0; i < _jointData.Length; i++ )
            {
                _jointData[i].State = TrackingState.NotTracked;
            }

            foreach (Joint joint in skeleton.Joints)
            {
                int type = (int)joint.JointType;
                _jointData[type].State = (TrackingState) joint.TrackingState;
                _jointData[type].PositionX = joint.Position.X;
                _jointData[type].PositionY = joint.Position.Y;
                _jointData[type].PositionZ = joint.Position.Z;
            }

            foreach (BoneOrientation boneOrientations in skeleton.BoneOrientations)
            {
                int type = (int)boneOrientations.EndJoint;
                _jointData[type].QuaternionX = boneOrientations.HierarchicalRotation.Quaternion.X;
                _jointData[type].QuaternionY = boneOrientations.HierarchicalRotation.Quaternion.Y;
                _jointData[type].QuaternionZ = boneOrientations.HierarchicalRotation.Quaternion.Z;
                _jointData[type].QuaternionW = boneOrientations.HierarchicalRotation.Quaternion.W;
            }
            Console.WriteLine(Converter.EncodeSkeletonData(_jointData));
        }
    }
}
