using System;
using System.Collections.Generic;
using System.Diagnostics;
using DataConverter;
using UnityEngine;




/// <summary>
/// The kinect binder creates the necessary setup for you to receive data for the kinect face tracking system directly.
/// 
/// Simply subscribe to the FaceTrackingDataReceived event to receive face tracking data.
/// Check FaceTrackingExample.cs for an example.
/// 
/// VideoFrameDataReceived and DepthFrameDataReceived events will give you the raw rbg/depth data from kinect cameras.
/// Check ImageFeedback.cs for an example.
/// </summary>
public class KinectManager : Singleton<KinectManager>
{
    public delegate void FaceTrackingDataDelegate(FaceData faceData);
    public event FaceTrackingDataDelegate FaceTrackingDataReceived;

    public delegate void VideoFrameDataDelegate(Color32[] pixels);
    public event VideoFrameDataDelegate VideoFrameDataReceived;

    public delegate void DepthFrameDataDelegate(ushort[] pixels);
    public event DepthFrameDataDelegate DepthFrameDataReceived;

    public delegate void InfraRedFrameDataDelegate(ushort[] pixels);
    public event InfraRedFrameDataDelegate InfraRedFrameDataReceived;

    public delegate void SkeletonDataDelegate(BodyData bodyData);
    public event SkeletonDataDelegate SkeletonDataReceived;

    public delegate void InteractionDataDelegate(HandPointer pointer);
    public event InteractionDataDelegate InteractionDataReceived;

    public delegate void TrackedUserChangedDelegate(ulong userId);
    public event TrackedUserChangedDelegate TrackedUserChanged;

    public FpsTracker SkeletonFpsTracker;
    public FpsTracker FaceFpsTracker;
    public FpsTracker InteractionFpsTracker;

    private Process _otherProcess;
    private bool _hasNewVideoContent;
    private bool _hasNewDepthContent;
    private bool _hasNewInfraRedContent;
    private List<string> _faceTrackingData;
    private List<string> _skeletonData;
    private List<string> _interactionData;
    private List<string> _poseActionDate;
    private System.Object _skeletonLock = new System.Object();
    private System.Object _faceLock = new System.Object();
    private System.Object _interactionLock = new System.Object();
    private System.Object _poseActionLock = new System.Object();
    private ushort[] _depthBuffer;
    private ushort[] _infraRedBuffer;
    private Color32[] _colorBuffer;
    private bool _isStarted;
    private ulong _switchToBodyID;

    public ulong TrackedBodyId
    {
        get { return _trackedBodyId; }
    }

    public int TrackedPlayerIndex
    {
        get { return _trackedPlayerIndex; }
    }


    private void SetTrackedBody(BodyData body)
    {
        _trackedBodyId = body.UserId;
        _trackedPlayerIndex = body.PlayerIndex;
        _lastTimeBodyWasTracked = Time.time;
        if (TrackedUserChanged != null)
        {
            TrackedUserChanged(_trackedBodyId);
        }
    }

    private void SetNoTrackedBody()
    {
        _trackedBodyId = NoTrackedBodyId;
        _trackedPlayerIndex = NotTrackedIndex;
        if (TrackedUserChanged != null)
        {
            TrackedUserChanged(_trackedBodyId);
        }
    }

    private ulong _trackedBodyId = NoTrackedBodyId;
    private const ulong NoTrackedBodyId = 0;

    private int _trackedPlayerIndex = NotTrackedIndex; 
    private const int NotTrackedIndex = -1;

    private float _lastTimeBodyWasTracked;
    
    public bool IsEnabled { get; set; }


    public void BootProcess()
    {
        if (_isStarted)
        {
            return;
        }

        const string dataTransmitterFilename = "KinectDataTransmitter.exe";
        string path = Application.dataPath + @"/../Kinect/";

        _otherProcess = new Process();
        _otherProcess.StartInfo.FileName = path + dataTransmitterFilename;
        _otherProcess.StartInfo.UseShellExecute = false;
        _otherProcess.StartInfo.CreateNoWindow = true;
        _otherProcess.StartInfo.RedirectStandardInput = true;
        _otherProcess.StartInfo.RedirectStandardOutput = true;
        _otherProcess.StartInfo.RedirectStandardError = true;
        _otherProcess.OutputDataReceived += (sender, args) => ParseReceivedData(args.Data);
        _otherProcess.ErrorDataReceived += (sender, args) => Debug.LogError(args.Data);

        try
        {
            _otherProcess.Start();
        }
        catch (Exception)
        {
            Debug.LogWarning(
                "Could not find the kinect data transmitter. Please read the readme.txt for the setup instructions.");
            _otherProcess = null;
            _isStarted = false;
            return;
        }

        SkeletonFpsTracker = new FpsTracker();
        FaceFpsTracker = new FpsTracker();
        InteractionFpsTracker = new FpsTracker();
        _faceTrackingData = new List<string>();
        _skeletonData = new List<string>();
        _interactionData = new List<string>();
        _poseActionDate = new List<string>();
        _otherProcess.BeginOutputReadLine();
        _otherProcess.StandardInput.WriteLine("1"); // gets rid of the Byte-order mark in the pipe.
        _isStarted = true;
        Enable();
    }


    void ParseReceivedData(string data)
    {
        if (!IsEnabled)
        {
            return;
        }

        if (Converter.IsFaceTrackingData(data))
        {
            lock (_faceLock)
            {
                _faceTrackingData.Add(data);
            }
        }
        else if (Converter.IsSkeletonData(data))
        {
            lock (_skeletonLock)
            {
                _skeletonData.Add(data);
            }
        }
        else if (Converter.IsVideoFrameData(data))
        {
            _hasNewVideoContent = true;
        }
        else if (Converter.IsDepthFrameData(data))
        {
            _hasNewDepthContent = true;
        }
        else if (Converter.IsInfraRedFrameData(data))
        {
            _hasNewInfraRedContent = true;
        }
        else if (Converter.IsPing(data))
        {
            if (_otherProcess != null && !_otherProcess.HasExited)
            {
                _otherProcess.StandardInput.WriteLine(Converter.EncodePingData());
            }
        }
        else if (Converter.IsError(data))
        {
            Debug.LogError(Converter.GetDataContent(data));
        }
        else if (Converter.IsInformationMessage(data))
        {
            Debug.Log("Kinect (information message): " + Converter.GetDataContent(data));
        }
        else if (Converter.IsNewInteractionUserData(data))
        {

        }
        else if (Converter.IsInteractionUserLeftData(data))
        {

        }
        else if (Converter.IsInteractionData(data))
        {
            lock (_interactionLock)
            {
                _interactionData.Add(data);
            }
        }
        else if (Converter.IsPoseActionData(data))
        {
            lock (_poseActionLock)
            {
                _poseActionDate.Add(data);
            }
        }
        else
        {
            Debug.LogWarning("Received this (unknown) message from kinect: " + data);
        }
    }


    public void Update()
    {
        if (!_isStarted)
        {
            return;
        }
        if (!IsEnabled)
        {
            return;
        }

        if (_otherProcess == null || _otherProcess.HasExited)
        {
            Debug.LogWarning("KinectDataTransmitter has exited. Trying to reboot the process...");
            _isStarted = false;
            BootProcess();
        }


        if (_hasNewVideoContent)
        {
            if (VideoFrameDataReceived != null)
            {
                ProcessVideoFrame(Converter.GetVideoStreamData());
            }
            _hasNewVideoContent = false;
        }

        if (_hasNewDepthContent)
        {
            if (DepthFrameDataReceived != null)
            {
                ProcessDepthFrame(Converter.GetDepthStreamData());
            }
            _hasNewDepthContent = false;
        }

        if (_hasNewInfraRedContent)
        {
            if (InfraRedFrameDataReceived != null)
            {
                ProcessInfraRedFrame(Converter.GetInfraRedStreamData());
            }
            _hasNewDepthContent = false;
        }

        if (_faceTrackingData != null)
        {
            var dataList = _faceTrackingData;
            lock (_faceLock)
            {
                _faceTrackingData = new List<string>();
            }
            foreach (var data in dataList)
            {
                ProcessFaceTrackingData(Converter.GetDataContent(data));
            }
        }

        if (_skeletonData != null)
        {
            var dataList = _skeletonData;
            lock (_skeletonLock)
            {
                _skeletonData = new List<string>();
            }
            foreach (var data in dataList)
            {
                ProcessSkeletonData(Converter.GetDataContent(data));
            }
        }

        if (_interactionData != null)
        {
            var dataList = _interactionData;
            lock (_interactionLock)
            {
                _interactionData = new List<string>();
            }
            foreach (var data in dataList)
            {
                ProcessInteractionData(Converter.GetDataContent(data));
            }
        }

        if (_poseActionDate != null)
        {
            var dataList = _poseActionDate;
            lock (_poseActionLock)
            {
                _poseActionDate = new List<string>();
            }
            foreach (var data in dataList)
            {
                ProcessPoseActionData(Converter.GetDataContent(data));
            }
        }

        const float timeToTimeoutTrackedUser = 0.5f;
        if (HasTrackedBody() && _lastTimeBodyWasTracked + timeToTimeoutTrackedUser < Time.time)
        {
            SetNoTrackedBody();
        }

        UpdateFpsTrackers();
    }
    private void ProcessDepthFrame(byte[] bytes)
    {
        if (DepthFrameDataReceived == null)
        {
            return;
        }
        Process16bitsFrame(bytes, ref _depthBuffer);
        DepthFrameDataReceived(_depthBuffer);
    }
    private void ProcessInfraRedFrame(byte[] bytes)
    {
        if (InfraRedFrameDataReceived == null)
        {
            return;
        }
        Process16bitsFrame(bytes, ref _infraRedBuffer);
        InfraRedFrameDataReceived(_infraRedBuffer);
    }
    
    private void Process16bitsFrame(byte[] rawBytes, ref ushort[] buffer)
    {
        if (rawBytes == null)
        {
            return;
        }

        if (buffer == null || buffer.Length != rawBytes.Length / 2)
        {
            buffer = new ushort[rawBytes.Length / 2];
        }
        for (int i = 0; i < buffer.Length; i++)
        {
            int byteIndex = i * 2;
            buffer[i] = BitConverter.ToUInt16(rawBytes, byteIndex);
        }
    }



    private void ProcessVideoFrame(byte[] bytes)
    {
        if (bytes == null || VideoFrameDataReceived == null)
        {
            return;
        }

        if (_colorBuffer == null || _colorBuffer.Length != bytes.Length / 4)
        {
            _colorBuffer = new Color32[bytes.Length / 4];
        }

        for (int i = 0; i < _colorBuffer.Length; i++)
        {
            int byteIndex = i * 4;
            _colorBuffer[i] = new Color32(bytes[byteIndex + 2], bytes[byteIndex + 1], bytes[byteIndex], byte.MaxValue);
        }

        VideoFrameDataReceived(_colorBuffer);
    }


    private void ProcessFaceTrackingData(string data)
    {
        FaceFpsTracker.AddFrame();

        if (FaceTrackingDataReceived == null)
        {
            return;
        }

        FaceData faceData;
        Converter.DecodeFaceTrackingData(data, out faceData);
        FaceTrackingDataReceived(faceData);
    }


    private void ProcessSkeletonData(string data)
    {
        SkeletonFpsTracker.AddFrame();

        if (SkeletonDataReceived == null)
        {
            return;
        }

        BodyData bodyData;
        Converter.DecodeSkeletonData(data, out bodyData);
        UpdateTrackedUserWith(bodyData);
        SkeletonDataReceived(bodyData);
    }

    private void UpdateTrackedUserWith(BodyData bodyData)
    {
        if (!HasTrackedBody())
        {
            if (bodyData.IsTracked)
            {
                SetTrackedBody(bodyData);
            }
        }
        else if (IsOverrideBody(bodyData))
        {
            //Logger.Info(System.String.Format("change TrackedBodyId {0}", TrackedBodyId));
            _switchToBodyID = NoTrackedBodyId;
            if (bodyData.IsTracked)
            {
                SetTrackedBody(bodyData);
            }
        }
        else if (TrackedBodyId == bodyData.UserId)
        {
            //Logger.Info(System.String.Format("TrackedBodyId {0}", TrackedBodyId));
            _trackedPlayerIndex = bodyData.PlayerIndex;
            if (bodyData.IsTracked)
            {
                _lastTimeBodyWasTracked = Time.time;
            }
        }
        else
        {
            Logger.Info(String.Format("TrackedBodyId unTracked {0}", bodyData.UserId));
        }
    }

    public bool HasTrackedBody()
    {
        return TrackedBodyId != NoTrackedBodyId;
    }

    private void ProcessInteractionData(string data)
    {
        InteractionFpsTracker.AddFrame();

        if (InteractionDataReceived == null)
        {
            return;
        }

        HandPointer pointer;
        Converter.DecodeInteractionData(data, out pointer);
        InteractionDataReceived(pointer);
    }

    private void ProcessPoseActionData(string data)
    {
        ulong bodyid;
        PoseType poseType;
        Converter.DecodePoseActionData(data, out bodyid, out poseType);
        switch (poseType)
        {
            case PoseType.Swith:
                //Debug.LogInfo(System.String.Format("Swith"));
                _switchToBodyID = bodyid;
                OverrideTrackingId(bodyid);
                break;
            case PoseType.LassoLeft:
                //Debug.LogInfo(System.String.Format("LassoLeft"));
                ChangeKinectDataTransmitterMode(KinectDeviceMode.Body | KinectDeviceMode.Interaction |
                                                KinectDeviceMode.Depth);
                break;
            case PoseType.LassoReight:
                //Log.Info(System.String.Format("LassoReight"));
                ChangeKinectDataTransmitterMode(KinectDeviceMode.Interaction |
                                                KinectDeviceMode.Depth);
                break;
        }
    }

    private void UpdateFpsTrackers()
    {
        SkeletonFpsTracker.Update();
        FaceFpsTracker.Update();
        InteractionFpsTracker.Update();
    }

    private void OverrideTrackingId(ulong trackingId)
    {
        _otherProcess.StandardInput.WriteLine(Converter.EncodeChangeHandTrackingBody(trackingId));
    }

    private void ChangeKinectDataTransmitterMode(KinectDeviceMode mode)
    {
        _otherProcess.StandardInput.WriteLine(Converter.EncodeKinectDeviceMode(mode));
    }

    public void Enable()
    {
        if (IsEnabled)
        {
            return;
        }
        Debug.LogInfo("Kinect enabled.");
        IsEnabled = true;
    }
    
    public void Disable()
    {
        if (!IsEnabled)
        {
            return;
        }
        Debug.LogInfo("Kinect disabled.");
        IsEnabled = false;
        SetNoTrackedBody();
        ClearData();
    }
    private void ClearData()
    {
        _faceTrackingData = new List<string>();
        _skeletonData = new List<string>();
        _interactionData = new List<string>();
        _poseActionDate = new List<string>();
        _hasNewDepthContent = false;
        _hasNewVideoContent = false;
    }

    public void ShutdownKinect()
    {
        _isStarted = false;
        if (_otherProcess == null)
        {
            return;
        }

        try
        {
            Process.GetProcessById(_otherProcess.Id);
        }
        catch (ArgumentException)
        {
            // The other app might have been shut down externally already.
            _otherProcess = null;
            return;
        }

        try
        {
            _otherProcess.CloseMainWindow();
            _otherProcess.Close();
        }
        catch (InvalidOperationException)
        {
            // The other app might have been shut down externally already.
        }
        finally
        {
            _otherProcess = null;
        }
    }

    private bool IsOverrideBody(BodyData body)
    {
        return (TrackedBodyId != _switchToBodyID && _switchToBodyID == body.UserId);
    }
}

