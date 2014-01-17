using System;
using System.Threading;
using DataConverter;
using Microsoft.Kinect;

namespace KinectDataTransmitter
{
  
    class Program
    {
        private static int _nonAcknoledgedPings;
        private static KinectDevice _kinectDevice;
        static void Main(string[] args)
        {
            if (KinectSensor.KinectSensors.Count == 0)
            {
                Console.WriteLine(Converter.EncodeError("No kinect device was found."));
                return;
            }

            try
            {
                _kinectDevice = new KinectDevice();
                _kinectDevice.IsTrackingSkeletons = true;
                _kinectDevice.IsTrackingFace = true;
                _kinectDevice.IsWritingDepthStream = true;
                _kinectDevice.IsTrackingInteraction = true;

                var pingThread = new Thread(SendPings);
                pingThread.Start();

                string inputStr = null;
                while ((inputStr = Console.ReadLine()) != null)
                {
                    const string byteOrderMark = "ï»¿";
                    if (inputStr.Length < 3|| inputStr.Length > 3 && inputStr[0] == 0xEF && inputStr[1] == 0xBB && inputStr[2] == 0xBF)
                    {
                        // ignore the bom.
                        continue;
                    }

                    ParseReceivedData(inputStr);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(Converter.EncodeError(e.Message));
            }
        }

        private static void SendPings()
        {
            while (true)
            {
                Console.WriteLine(Converter.EncodePingData());
                _nonAcknoledgedPings++;
                Thread.Sleep(10000);

                if (_nonAcknoledgedPings >= 2)
                {
                //    Environment.Exit(-1);
                }
            }
        }

        private static void ParseReceivedData(string data)
        {
            try
            {
                if (Converter.IsPing(data))
                {
                    _nonAcknoledgedPings--;
                }
                else if (Converter.IsChangeHandTrackingBody(data))
                {
                    ulong trackingId;
                    Converter.DecodeChangeHandTrackingBody(Converter.GetDataContent(data), out trackingId);
                    ChangeHandTrackingBody(trackingId);
                }
                else if (Converter.IsKinectDeviceModeData(data))
                {
                    KinectDeviceMode mode;
                    Converter.DecodeKinectDeviceMode(Converter.GetDataContent(data), out mode);
                    ChangeKinectDeviceMode(mode);
                }
                else
                {
                    //int i = inputStr.IndexOf('1');
                    //Console.WriteLine(inputStr + " " + i + " " + (inputStr[0] == 0xEF) + " " + (inputStr[1] == 0xBB) + " " + (inputStr[2] == 0xBF));
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(Converter.EncodeError(e.Message));
            }
        }

        private static void ChangeHandTrackingBody(ulong trackingId)
        {
            _kinectDevice.OverrideHandTracking(trackingId);
        }

        private static void ChangeKinectDeviceMode(KinectDeviceMode mode)
        {
            _kinectDevice.IsTrackingSkeletons   = (KinectDeviceMode.Body & mode)>0;
            _kinectDevice.IsTrackingFace = (KinectDeviceMode.Face & mode) > 0;
            _kinectDevice.IsTrackingInteraction = (KinectDeviceMode.Interaction & mode) > 0;
            _kinectDevice.IsWritingColorStream = (KinectDeviceMode.Color & mode) > 0;
            _kinectDevice.IsWritingDepthStream = (KinectDeviceMode.Depth & mode) > 0;
        }
    }
}
