using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DataConverter;
using Microsoft.Kinect;

namespace KinectDataTransmitter
{
    public class InteractionDataTracker : IDisposable
    {
        private bool _disposed;

        /// <summary>
        /// Width of interaction region in UI coordinates.
        /// </summary>
        private const double InteractionRegionWidth = 1024.0;

        /// <summary>
        /// Height of interaction region in UI coordinates.
        /// </summary>
        private const double InteractionRegionHeight = 768.0;

        /// <summary>
        /// Invalid skeleton tracking id.
        /// </summary>
        private const int InvalidTrackingId = 0;

        /// <summary>
        /// Keeps track of set of interacting users.
        /// </summary>
        private readonly HashSet<ulong> trackedUsers = new HashSet<ulong>();

        //// TODO: Rather than hardcoding UI element information, use the facilities
        //// TODO: provided by your UI framework of choice.

        /// <summary>
        /// Information about a button control laid out in application UI.
        /// </summary>
        private readonly UIElementInfo buttonControl = new UIElementInfo
            {
                Left = 100.0,
                Top = 100.0,
                Right = 300.0,
                Bottom = 300.0,
                Id = "button1"
            };

        #region PressAndGripAdjustment


        /// <summary>
        /// Returns information about the UI element located at the specified coordinates.
        /// This simulates a hit testing operation that would normally be performed by
        /// some UI framework.
        /// </summary>
        /// <param name="x">
        /// Horizontal position, in UI coordinates.
        /// </param>
        /// <param name="y">
        /// Vertical position, in UI coordinates.
        /// </param>
        /// <returns>
        /// Information about topmost UI control located at the specified UI position.
        /// </returns>
        private UIElementInfo PerformHitTest(double x, double y)
        {
            //// TODO: Rather than manually checking against bounds of each control, use
            //// TODO: UI framework hit testing functionality, if available
            if ((this.buttonControl.Left <= x) && (x <= this.buttonControl.Right) &&
                (this.buttonControl.Top <= y) && (y <= this.buttonControl.Bottom))
            {
                return this.buttonControl;
            }

            return null;
        }

        #endregion PressAndGripAdjustment
        
        #region Processing

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

        /// <summary>
        /// Handler for the Kinect sensor's SkeletonFrameReady event
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="skeletonFrameReadyEventArgs">event arguments</param>
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
                SendInteractionData(body);
            }
        }

        private void SendInteractionData(Body body, HandType handType)
        {
            HandPointer handPointer = convertHandPointerFromBodyAndHandType(body, handType);

            Console.WriteLine(Converter.EncodeInteraction(body.TrackingId,
                                                        (HandEventType)handPointer.HandEventType,
                                                        (HandType)handPointer.HandType, (float)handPointer.X, (float)handPointer.Y, (float)handPointer.PressExtent,
                                                        handPointer.IsActive, handPointer.IsInteractive, handPointer.IsPressed, handPointer.IsTracked));

        }

        private void SendInteractionData(Body body)
        {
            if (body == null)
            {
                return;
            }
            
            var currentUserSet = new HashSet<ulong>();
            var usersToRemove = new HashSet<ulong>();

            if (!this.trackedUsers.Contains(body.TrackingId))
            {
                Console.WriteLine(Converter.EncodeNewInteractionUser(body.TrackingId));
            }
            currentUserSet.Add(body.TrackingId);
            this.trackedUsers.Add(body.TrackingId);
            SendInteractionData(body, HandType.Right);
            SendInteractionData(body, HandType.Left);

            foreach (var id in this.trackedUsers)
            {
                if (!currentUserSet.Contains(id))
                {
                    usersToRemove.Add(id);
                }
            }

            foreach (var id in usersToRemove)
            {
                this.trackedUsers.Remove(id);
                Console.WriteLine(Converter.EncodeInteractionUserLeft(id));
            }
        }

        private HandPointer convertHandPointerFromBodyAndHandType(Body body, HandType handType){
                HandState handState = HandState.NotTracked;
                TrackingConfidence trackingConfidence = TrackingConfidence.Low;
                Vector4 spineToHand = new Vector4();
                if (handType == HandType.Left){
                    handState = body.HandLeftState;
                    trackingConfidence = body.HandLeftConfidence;
                    spineToHand = VectorBetweenJoints(body, Microsoft.Kinect.JointType.SpineMid, Microsoft.Kinect.JointType.HandLeft);
                } else if (handType == HandType.Right){
                    handState = body.HandRightState;
                    trackingConfidence = body.HandRightConfidence;
                    spineToHand = VectorBetweenJoints(body, Microsoft.Kinect.JointType.SpineMid, Microsoft.Kinect.JointType.HandRight);
                }
                HandEventType handEventType = HandEventType.None;
                if (handState == HandState.Closed)
                {
                    handEventType = HandEventType.Grip;
                }
                else if (handState == HandState.Open)
                {
                    handEventType = HandEventType.GripRelease;
                }


            HandPointer handPointer = new HandPointer{
                UserId = body.TrackingId,
                HandEventType = handEventType,
                HandType = handType,
                X = spineToHand.X,
                Y = spineToHand.Y,
                PressExtent = spineToHand.Z,
                 IsActive = handState != HandState.NotTracked && trackingConfidence == TrackingConfidence.High,
                 IsInteractive = false,
                 IsPressed = false,
                 IsTracked = handState != HandState.NotTracked
            };

            return handPointer;
        }

        static Vector4 VectorBetweenJoints(Body body, Microsoft.Kinect.JointType start, Microsoft.Kinect.JointType end)
        {
            CameraSpacePoint pointStart = body.Joints[start].Position;
            CameraSpacePoint pointEnd = body.Joints[end].Position;
            return new Vector4
            {
                X = pointEnd.X - pointStart.X,
                Y = pointEnd.Y - pointStart.Y,
                Z = pointEnd.Z - pointStart.Z
            };
        }

        #endregion Processing

        /// <summary>
        /// This class is meant to be analogous to a structure used by your UI framework to
        /// represent hit-testable components such as button controls, sliders, etc.
        /// </summary>
        /// <remarks>
        /// Rather than defining your own class, you should leverage hit testing functionality
        /// provided by your UI framework.
        /// </remarks>
        private class UIElementInfo
        {
            /// <summary>
            /// Position of the left edge of UI element.
            /// </summary>
            public double Left { get; set; }

            /// <summary>
            /// Position of the right edge of UI element.
            /// </summary>
            public double Right { get; set; }

            /// <summary>
            /// Position of the top edge of UI element.
            /// </summary>
            public double Top { get; set; }

            /// <summary>
            /// Position of the bottom edge of UI element.
            /// </summary>
            public double Bottom { get; set; }

            /// <summary>
            /// Identifier of UI element.
            /// </summary>
            public string Id { get; set; }
        }
    }
}
