using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace DataConverter
{
    /// <summary>
    /// Class responsible for converting data from the file mapping data stream to managed data that can be used in code.
    /// </summary>
    public static class Converter
    {
        // chars encoding the types of messages that can be transmitted.
        public const char ErrorType = 'E';
        public const char PingType = 'P';
        public const char FaceTrackingFrameType = 'F';
        public const char VideoFrameType = 'V';
        public const char DepthFrameType = 'D';
        public const char DebugType = 'd';
        public const char SkeletonFrameType = 'S';
        public const char InteractionNewUserFrameType = 'U';
        public const char InteractionUserLeftFrameType = 'u';
        public const char InteractionFrameType = 'I';

        /// <summary>
        /// Maximum number of joints in a skeleton.
        /// </summary>
        public static int JointCount = Enum.GetValues(typeof(JointType)).Length;

        /// <summary>
        /// The name of the file used to transfer color data.
        /// </summary>
        private const string ColorFileName = "KinectColorFrame";
        /// <summary>
        /// The name of the file used to transfer depth data.
        /// </summary>
        private const string DepthFileName = "DepthColorFrame";
        /// <summary>
        /// The size of the file used to transfer color data.
        /// </summary>
        private const long ColorFileSize = 640 * 480 * 4;
        /// <summary>
        /// The size of the file used to transfer depth data.
        /// </summary>
        private const long DepthFileSize = 640 * 480 * 2;
        /// <summary>
        /// The code corresponding to "read" access level.
        /// </summary>
        private const int ReadAccess = 4;

        private static StringBuilder _stringBuilder = new StringBuilder();

        /// <summary>
        /// Opens a named file mapping object.
        /// </summary>
        /// <param name="desiredAccess">The access to the file mapping object. This access is checked against any security descriptor on the target file mapping object. 
        /// For a list of values, see <see cref="http://msdn.microsoft.com/en-us/library/windows/desktop/aa366559(v=vs.85).aspx"/> File Mapping Security and Access Rights.</param>
        /// <param name="inheritHandle">If this parameter is TRUE, a process created by the CreateProcess function can inherit the handle; 
        /// otherwise, the handle cannot be inherited.</param>
        /// <param name="name">The name of the file mapping object to be opened. 
        /// If there is an open handle to a file mapping object by this name and the security descriptor on the mapping object does not conflict with the dwDesiredAccess parameter, 
        /// the open operation succeeds.</param>
        /// <returns>If the function succeeds, the return value is an open handle to the specified file mapping object.</returns>
        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern IntPtr OpenFileMapping(int desiredAccess, bool inheritHandle, String name);

        /// <summary>
        /// Maps a view of a file mapping into the address space of a calling Windows Store app.
        /// </summary>
        /// <param name="fileMappingObject">A handle to a file mapping object.</param>
        /// <param name="desiredAccess">The type of access to a file mapping object, which determines the protection of the pages.</param>
        /// <param name="fileOffsetHigh">A high-order DWORD of the file offset where the view begins.</param>
        /// <param name="fileOffsetLow">A low-order DWORD of the file offset where the view is to begin. 
        /// The combination of the high and low offsets must specify an offset within the file mapping. 
        /// They must also match the memory allocation granularity of the system. 
        /// That is, the offset must be a multiple of the allocation granularity. 
        /// To obtain the memory allocation granularity of the system, use the GetSystemInfo function, 
        /// which fills in the members of a SYSTEM_INFO structure.</param>
        /// <param name="numBytesToMap">The number of bytes of a file mapping to map to the view. 
        /// All bytes must be within the maximum size specified by CreateFileMapping.</param>
        /// <returns>If the function succeeds, the return value is the starting address of the mapped view.</returns>
        [DllImport("kernel32", SetLastError = true)]
        public static extern IntPtr MapViewOfFile(IntPtr fileMappingObject, int desiredAccess, int fileOffsetHigh, int fileOffsetLow, IntPtr numBytesToMap);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool UnmapViewOfFile(IntPtr lpBaseAddress);

        private static byte[] _depthBytes = new byte[DepthFileSize]; // Unique allocation to avoid constant (and costly) garbage collections.
        private static byte[] _colorBytes = new byte[ColorFileSize];

        /// <summary>
        /// Reads kinect's video stream data from the file mapping created by the kinect data transmitter.
        /// </summary>
        public static byte[] GetVideoStreamData()
        {
            return ReadFromMappedFile(ColorFileName, _colorBytes);
        }
        /// <summary>
        /// Reads kinect's depth stream data from the file mapping created by the kinect data transmitter.
        /// </summary>
        public static byte[] GetDepthStreamData()
        {
            return ReadFromMappedFile(DepthFileName, _depthBytes);
        }

        private static byte[] ReadFromMappedFile(string filename, byte[] bytesBuffer)
        {
            IntPtr handle = IntPtr.Zero;
            IntPtr pointer = IntPtr.Zero;

            try
            {
                handle = OpenFileMapping(ReadAccess, false, filename);
                pointer = MapViewOfFile(handle, ReadAccess, 0, 0, new IntPtr(bytesBuffer.Length));
                Marshal.Copy(pointer, bytesBuffer, 0, bytesBuffer.Length);
            }
            catch (Exception e)
            {
                bytesBuffer = null;
            }
            finally
            {
                try
                {
                    if (handle != IntPtr.Zero)
                    {
                        UnmapViewOfFile(pointer);
                        CloseHandle(handle);
                    }
                }
                catch (Exception)
                {
                }
            }

            return bytesBuffer;
        }

        /// <summary>
        /// Encodes face tracking data for transmission through the data stream.
        /// </summary>
        /// <param name="au0">Animation unit 0.</param>
        /// <param name="au1">Animation unit 1.</param>
        /// <param name="au2">Animation unit 2.</param>
        /// <param name="au3">Animation unit 3.</param>
        /// <param name="au4">Animation unit 4.</param>
        /// <param name="au5">Animation unit 5.</param>
        /// <param name="posX">Head position (x) in meters.</param>
        /// <param name="posY">Head position (y) in meters.</param>
        /// <param name="posZ">Head position (z) in meters.</param>
        /// <param name="rotX">Head rotation (x - euler angle).</param>
        /// <param name="rotY">Head rotation (y - euler angle).</param>
        /// <param name="rotZ">Head rotation (z - euler angle).</param>
        /// <returns>The string that encodes the facetracking information.</returns>
        public static string EncodeFaceTrackingData(FaceData data)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}|{1} {2} {3} {4} {5} {6} {7} {8} {9} {10} {11} {12}",
                                 FaceTrackingFrameType, data.Au0, data.Au1, data.Au2, data.Au3, data.Au4, data.Au5,
                                 data.PosX, data.PosY, data.PosZ, data.RotX, data.RotY, data.RotZ);
        }

        /// <summary>
        /// Encodes skeleton data to transmission.
        /// </summary>
        public static string EncodeSkeletonData(BodyData bodyData)
        {
            if (bodyData.JointData == null)
            {
                return EncodeError("EncodeSkeletonData: joint data is null.");
            }

            _stringBuilder.Remove(0, _stringBuilder.Length);
            _stringBuilder.AppendFormat(CultureInfo.InvariantCulture,"{0}|{1} {2} ", SkeletonFrameType, bodyData.UserId, (int)bodyData.TrackingState);
            foreach (var jointData in bodyData.JointData)
            {
                if (jointData.State == JointTrackingState.NotTracked)
                {
                    continue;
                }

                // joint_id state x y z qx qy qz qw 
                _stringBuilder.AppendFormat(CultureInfo.InvariantCulture, "{0} {1} {2} {3} {4} {5} {6} {7} {8} ",
                                            (int)jointData.JointId, (int)jointData.State, jointData.PositionX, jointData.PositionY, jointData.PositionZ,
                                            jointData.QuaternionX, jointData.QuaternionY, jointData.QuaternionZ, jointData.QuaternionW);
            }
            return _stringBuilder.ToString();
        }



        public static string EncodeNewInteractionUser(ulong skeletonTrackingId)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}|{1}", InteractionNewUserFrameType, skeletonTrackingId);
        }

        public static string EncodeInteraction(ulong skeletonTrackingId, HandEventType handEventType, HandType handType, float x, float y, float pressExtent, bool isActive, bool isInteractive, bool isPressed, bool isTracked)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}|{1} {2} {3} {4} {5} {6} {7} {8} {9} {10}", InteractionFrameType, skeletonTrackingId, handEventType, handType, x, y, pressExtent, isActive, isInteractive, isPressed, isTracked);
        }

        public static string EncodeInteractionUserLeft(ulong id)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}|{1}", InteractionUserLeftFrameType, id);
        }

        /// <summary>
        /// Retrieves the part of the data that contains content.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static string GetDataContent(string data)
        {
            // We send datablocks of the type: 'FrameType(char)'|Content
            return data.Substring(2);
        }

        /// <summary>
        /// Decodes face tracking data received from the data stream into meaningful values.
        /// </summary>
        /// <param name="data">The data that encodes the facetracking information.</param>
        /// <param name="faceData">FaceTracking data.</param>
        public static void DecodeFaceTrackingData(string data, out FaceData faceData)
        {
            string[] tokens = data.Split(' ');
            faceData = new FaceData();
            faceData.Au0 = float.Parse(tokens[0], CultureInfo.InvariantCulture);
            faceData.Au1 = float.Parse(tokens[1], CultureInfo.InvariantCulture);
            faceData.Au2 = float.Parse(tokens[2], CultureInfo.InvariantCulture);
            faceData.Au3 = float.Parse(tokens[3], CultureInfo.InvariantCulture);
            faceData.Au4 = float.Parse(tokens[4], CultureInfo.InvariantCulture);
            faceData.Au5 = float.Parse(tokens[5], CultureInfo.InvariantCulture);
            faceData.PosX = float.Parse(tokens[6], CultureInfo.InvariantCulture);
            faceData.PosY = float.Parse(tokens[7], CultureInfo.InvariantCulture);
            faceData.PosZ = float.Parse(tokens[8], CultureInfo.InvariantCulture);
            faceData.RotX = float.Parse(tokens[9], CultureInfo.InvariantCulture);
            faceData.RotY = float.Parse(tokens[10], CultureInfo.InvariantCulture);
            faceData.RotZ = float.Parse(tokens[11], CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Decodes the skeleton data received from the data stream into joint positions.
        /// </summary>
        public static void DecodeSkeletonData(string data, out BodyData bodyData)
        {
            const int jointsNumber = (int)JointType.Count;
            bodyData = new BodyData();
            bodyData.JointData = new JointData[jointsNumber];
            var jointsData = bodyData.JointData;

            if (jointsData == null || jointsData.Length != jointsNumber)
            {
                throw new Exception("DecodeSkeletonData is expecting a JointData[] buffer big enough to hold the data.");
            }

            for (int i = 0; i < jointsData.Length; i++)
            {
                jointsData[i].State = JointTrackingState.NotTracked;
            }

            string[] tokens = data.Split(' ');
            bodyData.UserId = int.Parse(tokens[0], CultureInfo.InvariantCulture);
            bodyData.TrackingState = (BodyTrackingState)int.Parse(tokens[1], CultureInfo.InvariantCulture);

            const int jointDataOffset = 2;
            const int elementsNumber = 9;
            for (int i = 0; i + jointDataOffset < (tokens.Length / elementsNumber) + jointDataOffset; i++)
            {
                int jointId = int.Parse(tokens[(i * elementsNumber) + jointDataOffset], CultureInfo.InvariantCulture);
                jointsData[jointId].State = (JointTrackingState)int.Parse(tokens[(i * elementsNumber + 1) + jointDataOffset], CultureInfo.InvariantCulture);
                jointsData[jointId].PositionX = float.Parse(tokens[(i * elementsNumber + 2) + jointDataOffset], CultureInfo.InvariantCulture);
                jointsData[jointId].PositionY = float.Parse(tokens[(i * elementsNumber + 3) + jointDataOffset], CultureInfo.InvariantCulture);
                jointsData[jointId].PositionZ = float.Parse(tokens[(i * elementsNumber + 4) + jointDataOffset], CultureInfo.InvariantCulture);
                jointsData[jointId].QuaternionX = float.Parse(tokens[(i * elementsNumber + 5) + jointDataOffset], CultureInfo.InvariantCulture);
                jointsData[jointId].QuaternionY = float.Parse(tokens[(i * elementsNumber + 6) + jointDataOffset], CultureInfo.InvariantCulture);
                jointsData[jointId].QuaternionZ = float.Parse(tokens[(i * elementsNumber + 7) + jointDataOffset], CultureInfo.InvariantCulture);
                jointsData[jointId].QuaternionW = float.Parse(tokens[(i * elementsNumber + 8) + jointDataOffset], CultureInfo.InvariantCulture);
             }
        }

        public static void DecodeNewInteractionUserData(string data, out int skeletonTrackingId)
        {
            skeletonTrackingId = int.Parse(data, CultureInfo.InvariantCulture);
        }

        public static void DecodeInteractionData(string data, out HandPointer handPointer)
        {
            var tokens = data.Split(' ');

            handPointer = new HandPointer();
            handPointer.UserId = int.Parse(tokens[0], CultureInfo.InvariantCulture);
            handPointer.HandEventType = (HandEventType)Enum.Parse(typeof(HandEventType), tokens[1]);
            handPointer.HandType = (HandType)Enum.Parse(typeof(HandType), tokens[2]);
            handPointer.X = float.Parse(tokens[3], CultureInfo.InvariantCulture);
            handPointer.Y = float.Parse(tokens[4], CultureInfo.InvariantCulture);
            handPointer.PressExtent = float.Parse(tokens[5], CultureInfo.InvariantCulture);
            handPointer.IsActive = bool.Parse(tokens[6]);
            handPointer.IsInteractive = bool.Parse(tokens[7]);
            handPointer.IsPressed = bool.Parse(tokens[8]);
            handPointer.IsTracked = bool.Parse(tokens[9]);

        }

        public static void DecodeInteractionUserLeftData(string data, out int skeletonTrackingId)
        {
            skeletonTrackingId = int.Parse(data, CultureInfo.InvariantCulture);
        }
        
        /// <summary>
        /// Encodes an error message to be sent through the data stream.
        /// </summary>
        public static string EncodeError(string message)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}|{1}", ErrorType, message);
        }

        public static string EncodePingData()
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}|", PingType);
        }

        /// <summary>
        /// Encodes information messages.
        /// </summary>
        public static string EncodeInfo(string message)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}|{1}", DebugType, message);
        }

        /// <summary>
        /// Checks whether the given data is face tracking data.
        /// </summary>
        public static bool IsFaceTrackingData(string data)
        {
            return data[0] == FaceTrackingFrameType;
        }
        /// <summary>
        /// Checks whether the given data is video frame data.
        /// </summary>
        public static bool IsVideoFrameData(string data)
        {
            return data[0] == VideoFrameType;
        }
        /// <summary>
        /// Checks whether the given data is depth frame data.
        /// </summary>
        public static bool IsDepthFrameData(string data)
        {
            return data[0] == DepthFrameType;
        }
        /// <summary>
        /// Checks whether the given data is an information message.
        /// </summary>
        public static bool IsInformationMessage(string data)
        {
            return data[0] == DebugType;
        }
        /// <summary>
        /// Checks whether the given data is an error message.
        /// </summary>
        public static bool IsError(string data)
        {
            return data[0] == ErrorType;
        }
        /// <summary>
        /// Checks whether the given data is skeleton data.
        /// </summary>
        public static bool IsSkeletonData(string data)
        {
            return data[0] == SkeletonFrameType;
        }
        /// <summary>
        /// Checks whether the given data is a ping data.
        /// </summary>
        public static bool IsPing(string data)
        {
            return data[0] == PingType;
        }

        public static bool IsNewInteractionUserData(string data)
        {
            return data[0] == InteractionNewUserFrameType;
        }
        public static bool IsInteractionUserLeftData(string data)
        {
            return data[0] == InteractionUserLeftFrameType;
        }
        public static bool IsInteractionData(string data)
        {
            return data[0] == InteractionFrameType;
        }
    }


    /// <summary>
    /// The names of the joints.
    /// (This enum is a copy of kinect's JointType, to avoid a necessity of referencing Kinect's dll from within Unity)
    /// </summary>
    public enum JointType
    {
        SpineBase = 0,
        SpineMid = 1,
        Neck = 2,
        Head = 3,
        ShoulderLeft = 4,
        ElbowLeft = 5,
        WristLeft = 6,
        HandLeft = 7,
        ShoulderRight = 8,
        ElbowRight = 9,
        WristRight = 10,
        HandRight = 11,
        HipLeft = 12,
        KneeLeft = 13,
        AnkleLeft = 14,
        FootLeft = 15,
        HipRight = 16,
        KneeRight = 17,
        AnkleRight = 18,
        FootRight = 19,
        SpineShoulder = 20,
        HandTipLeft = 21,
        ThumbLeft = 22,
        HandTipRight = 23,
        ThumbRight = 24,
        Count = 25,
    }

    public enum BodyTrackingState
    {
        NotTracked = 0,
        PositionOnly,
        Tracked,
    }

    public enum JointTrackingState
    {
        NotTracked = 0,
        Inferred,
        Tracked,
    }

    public enum HandEventType
    {
        None,
        Grip,
        GripRelease,
    }

    public enum HandType
    {
        None,
        Left,
        Right,
    }

    public struct HandPointer
    {
        public int UserId;
        public HandEventType HandEventType;
        public HandType HandType;
        public float X;
        public float Y;
        public float PressExtent;
        public bool IsActive;
        public bool IsInteractive;
        public bool IsPressed;
        public bool IsTracked;
    }

    public struct BodyData
    {
        public int UserId;
        public BodyTrackingState TrackingState;
        public JointData[] JointData;
    }

    public struct JointData
    {
        public JointType JointId;
        public JointTrackingState State;
        public float PositionX;
        public float PositionY;
        public float PositionZ;
        public float QuaternionX;
        public float QuaternionY;
        public float QuaternionZ;
        public float QuaternionW;
    }

    public struct FaceData
    {
        public int UserId;
        public float Au0;
        public float Au1;
        public float Au2;
        public float Au3;
        public float Au4;
        public float Au5;
        public float PosX;
        public float PosY;
        public float PosZ;
        public float RotX;
        public float RotY;
        public float RotZ;
    }
}
