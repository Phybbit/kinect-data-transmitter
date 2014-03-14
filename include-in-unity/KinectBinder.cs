using UnityEngine;


public class KinectBinder : MonoBehaviour
{
    void Start()
    {
        KinectManager.Instance.BootProcess();
    }

    void Update()
    {
        KinectManager.Instance.Update();
    }

#if false
    void OnGUI()
    {
        if (Event.current.type != EventType.Repaint)
        {
            return;
        }

        var skeletonFpsTracker = KinectManager.Instance.SkeletonFpsTracker;
        var faceFpsTracker = KinectManager.Instance.FaceFpsTracker;
        var interactionFpsTracker = KinectManager.Instance.InteractionFpsTracker;

        GUI.color = Color.white;
        GUI.Label(new Rect(5, 55, 250, 30), string.Format("Kinect FPS [S: {0} | F: {1} | I: {2}]", skeletonFpsTracker.Fps, faceFpsTracker.Fps, interactionFpsTracker.Fps));
        if ((skeletonFpsTracker.Fps == 0) && (faceFpsTracker.Fps == 0) && (interactionFpsTracker.Fps == 0))
        {
            GUI.Label(new Rect(5, 75, 400, 30), "(Kinect is not tracking... please get in range.)");
        }
    }
#endif

    void OnApplicationQuit()
    {
        KinectManager.Instance.ShutdownKinect();
    }

}

