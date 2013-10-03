kinect-data-transmitter
=======================

KinectDataTransmitter is a .net4 project that reads the data from the kinect device and sends it over IPC to your Unity3D process.
It helps to bind both the Microsoft Kinect SDK and Unity without going through wrapping native code.

Currently supported features:
- Kienct hardware setup (single device)
- Kinect face tracking (single user)
- Skeletal tracking (single user)
- Access Video and depth maps
- Access to parts of the interaction library
