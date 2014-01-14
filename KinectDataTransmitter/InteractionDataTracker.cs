using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DataConverter;
using Microsoft.Kinect;
using Microsoft.Kinect.Toolkit;
using Microsoft.Kinect.Toolkit.Interaction;

namespace KinectDataTransmitter
{
    public class InteractionDataTracker : IInteractionClient
    {
        //// TODO: Rather than hardcoding size of interaction region, use dimensions of the
        //// TODO: region of your UI that allows Kinect interactions.

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

        /// <summary>
        /// Entry point for interaction stream functionality.
        /// </summary>
        private InteractionStream interactionStream;

        /// <summary>
        /// Sensor currently in use.
        /// </summary>
        private KinectSensor kinectSensor;

        /// <summary>
        /// Intermediate storage for the user information received from interaction stream.
        /// </summary>
        private UserInfo[] userInfos;


        #region PressAndGripAdjustment

        /// <summary>
        /// Gets interaction information available for a specified location in UI.
        /// </summary>
        /// <param name="skeletonTrackingId">
        /// The skeleton tracking ID for which interaction information is being retrieved.
        /// </param>
        /// <param name="handType">
        /// The hand type for which interaction information is being retrieved.
        /// </param>
        /// <param name="x">
        /// X-coordinate of UI location for which interaction information is being retrieved.
        /// 0.0 corresponds to left edge of interaction region and 1.0 corresponds to right edge
        /// of interaction region.
        /// </param>
        /// <param name="y">
        /// Y-coordinate of UI location for which interaction information is being retrieved.
        /// 0.0 corresponds to top edge of interaction region and 1.0 corresponds to bottom edge
        /// of interaction region.
        /// </param>
        /// <returns>
        /// An <see cref="InteractionInfo"/> object instance.
        /// </returns>
        public InteractionInfo GetInteractionInfoAtLocation(int skeletonTrackingId, InteractionHandType handType,
                                                            double x, double y)
        {
            var interactionInfo = new InteractionInfo
                {
                    IsPressTarget = false,
                    IsGripTarget = false
                };

            // Map coordinates from [0.0,1.0] coordinates to UI-relative coordinates
            double xUI = x*InteractionRegionWidth;
            double yUI = y*InteractionRegionHeight;

            var uiElement = this.PerformHitTest(xUI, yUI);

            if (uiElement != null)
            {
                interactionInfo.IsPressTarget = true;

                // If UI framework uses strings as button IDs, use string hash code as ID
                interactionInfo.PressTargetControlId = uiElement.Id.GetHashCode();

                // Designate center of button to be the press attraction point
                //// TODO: Create your own logic to assign press attraction points if center
                //// TODO: is not always the desired attraction point.
                interactionInfo.PressAttractionPointX = ((uiElement.Left + uiElement.Right)/2.0)/InteractionRegionWidth;
                interactionInfo.PressAttractionPointY = ((uiElement.Top + uiElement.Bottom)/2.0)/InteractionRegionHeight;
            }

            return interactionInfo;
        }

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

        #region Configuration

        /// <summary>
        /// Prepare to feed data and skeleton frames to a new interaction stream and receive
        /// interaction data from interaction stream.
        /// </summary>
        /// <param name="sensor">
        /// Sensor from which we will stream depth and skeleton data.
        /// </param>
        public void InitializeTracking(KinectSensor sensor)
        {
            // Allocate space to put the skeleton and interaction data we'll receive
            this.userInfos = new UserInfo[InteractionFrame.UserInfoArrayLength];

            this.interactionStream = new InteractionStream(sensor, this);
            this.interactionStream.InteractionFrameReady += this.InteractionFrameReady;
            this.kinectSensor = sensor;
        }

        /// <summary>
        /// Clean up interaction stream and associated data structures.
        /// </summary>
        /// <param name="sensor">
        /// Sensor from which we were streaming depth and skeleton data.
        /// </param>
        public void ResetTracking()
        {
            this.kinectSensor = null;
            this.userInfos = null;

            this.interactionStream.InteractionFrameReady -= this.InteractionFrameReady;
            this.interactionStream.Dispose();
            this.interactionStream = null;
        }


        #endregion Configuration

        #region Processing

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
                UserId = (int)body.TrackingId,
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

        /// <summary>
        /// Event handler for InteractionStream's InteractionFrameReady event
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void InteractionFrameReady(object sender, InteractionFrameReadyEventArgs e)
        {
            // Check for a null userInfos since we may still get posted events
            // from the stream after we have unregistered our event handler and
            // deleted our buffers.
            if (this.userInfos == null)
            {
                return;
            }

            UserInfo[] localUserInfos = null;
            long timestamp = 0;

            using (InteractionFrame interactionFrame = e.OpenInteractionFrame())
            {
                if (interactionFrame != null)
                {
                    // Copy interaction frame data so we can dispose interaction frame
                    // right away, even if data processing/event handling takes a while.
                    interactionFrame.CopyInteractionDataTo(this.userInfos);
                    timestamp = interactionFrame.Timestamp;
                    localUserInfos = this.userInfos;
                }
            }

            if (localUserInfos != null)
            {
                //// TODO: Process user info data, perform hit testing with UI, route UI events, etc.
                //// TODO: See KinectRegion and KinectAdapter in Microsoft.Kinect.Toolkit.Controls assembly
                //// TODO: For a more comprehensive example on how to do this.

                //var currentUserSet = new HashSet<int>();
                //var usersToRemove = new HashSet<int>();

                //// Keep track of current users in scene
                //foreach (var info in localUserInfos)
                //{
                //    if (info.SkeletonTrackingId == InvalidTrackingId)
                //    {
                //        // Only look at user information corresponding to valid users
                //        continue;
                //    }

                //    if (!this.trackedUsers.Contains(info.SkeletonTrackingId))
                //    {
                //        Console.WriteLine(Converter.EncodeNewInteractionUser(info.SkeletonTrackingId));
                //    }

                //    currentUserSet.Add(info.SkeletonTrackingId);
                //    this.trackedUsers.Add(info.SkeletonTrackingId);

                    // Perform hit testing and look for Grip and GripRelease events
                //    foreach (var handPointer in info.HandPointers)
                //    {
                //        Console.WriteLine(Converter.EncodeInteraction(info.SkeletonTrackingId,
                //                                                    (HandEventType)handPointer.HandEventType,
                //                                                    (HandType)handPointer.HandType, (float)handPointer.X, (float)handPointer.Y, (float)handPointer.PressExtent,
                //                                                    handPointer.IsActive, handPointer.IsInteractive, handPointer.IsPressed, handPointer.IsTracked));                            
                //    }
                //}

                //foreach (var id in this.trackedUsers)
                //{
                //    if (!currentUserSet.Contains(id))
                //    {
                //        usersToRemove.Add(id);
                //    }
                //}

                //foreach (var id in usersToRemove)
                //{
                //    this.trackedUsers.Remove(id);
                //    Console.WriteLine(Converter.EncodeInteractionUserLeft(id));
                //}
            }
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
