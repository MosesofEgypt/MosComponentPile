using UnityEngine;


[AddComponentMenu("Camera/Splitscreen Camera")]
public class SplitscreenCamera : MonoBehaviour {
    public enum ScreenNumber {
        first,
        second,
        third,
        fourth,
        invalid,
    }

    public ScreenNumber screenNumber = ScreenNumber.first;

    public bool vertical2PRects = true;

    public bool long3PRect = false;

    public Vector2Int joinedCameraPadding = new Vector2Int(1, 1);

    public static SplitscreenCamera[] joinedCameras = new SplitscreenCamera[4] { null, null, null, null };

    [SerializeField]
    Rect[] cameraRects4Player = new Rect[4] {
        new Rect(0.0f, 0.5f, 0.5f, 0.5f),
        new Rect(0.5f, 0.5f, 0.5f, 0.5f),
        new Rect(0.0f, 0.0f, 0.5f, 0.5f),
        new Rect(0.5f, 0.0f, 0.5f, 0.5f),
        };

    [SerializeField]
    Rect[] cameraRects3Player = new Rect[3] {
        new Rect(0.0f, 0.5f, 1.0f, 0.5f),
        new Rect(0.0f, 0.0f, 0.5f, 0.5f),
        new Rect(0.5f, 0.0f, 0.5f, 0.5f),
        };

    [SerializeField]
    Rect[] cameraRects2Player = new Rect[2] {
        new Rect(0.0f, 0.0f, 0.5f, 1.0f),
        new Rect(0.5f, 0.0f, 0.5f, 1.0f),
        };

    [SerializeField]
    Rect[] cameraRects2PlayerHorizontal = new Rect[2] {
        new Rect(0.0f, 0.5f, 1.0f, 0.5f),
        new Rect(0.0f, 0.0f, 1.0f, 0.5f),
        };

    ScreenNumber prevScreenNumber = ScreenNumber.invalid;

    int prevScreenWidth = 0, prevScreenHeight = 0;

    bool prevVertical2PRects = true, prevLong3PRect = false;

    Camera myCamera;

    void Awake() {
        myCamera = GetComponent<Camera>() as Camera;
    }

    void OnDisable() { 
        RemoveCamera();
        prevScreenNumber = ScreenNumber.invalid;
    }

    void Update() {
        if (!myCamera)
            return;

        // if ANY of these change, the camera needs to be updated
        if (screenNumber != prevScreenNumber ||
            prevScreenWidth != Screen.width || prevScreenHeight != Screen.height ||
            vertical2PRects != prevVertical2PRects || long3PRect != prevLong3PRect)
            SetupCamera();
    }

    protected void SetupCamera() {
        // if this screen number is valid, add the camera to the joined cameras
        // and force the other cameras to recalculate their viewing rectangle.

        if (screenNumber < ScreenNumber.first || ScreenNumber.fourth < screenNumber) {
            Debug.LogError("Attempted to setup a camera with screen number: " + screenNumber);
            return;
        }

        joinedCameras[(int)screenNumber] = this;
        foreach (SplitscreenCamera camera in joinedCameras)
            if (camera) {
                camera.vertical2PRects = vertical2PRects;
                camera.long3PRect = long3PRect;
                camera.joinedCameraPadding = joinedCameraPadding;

                camera.InitializeViewingRectangle();
            }
    }

    protected void RemoveCamera() {
        if (screenNumber < ScreenNumber.first || ScreenNumber.fourth < screenNumber)
            return;

        // null this camera from the array
        joinedCameras[(int)screenNumber] = null;

        // force the other cameras to recalculate their viewing rectangle.
        foreach (SplitscreenCamera otherCamera in joinedCameras)
            if (otherCamera)
                otherCamera.InitializeViewingRectangle();
    }

    protected void InitializeViewingRectangle() {
        prevScreenNumber = screenNumber;
        prevVertical2PRects = vertical2PRects;
        prevLong3PRect = long3PRect;
        prevScreenWidth = Screen.width;
        prevScreenHeight = Screen.height;

        int cameraCt = 0, thisScreenNum = (int)screenNumber;
        bool isActive = false;
        // determine where to place this camera on the screen
        for (int i = 0; i < joinedCameras.Length; i++)
            if (joinedCameras[i]) {
                cameraCt++;
                if (joinedCameras[i] == this)
                    isActive = true;
            } else if (i <= thisScreenNum)
                thisScreenNum--;

        if (!isActive)
            return;

        thisScreenNum = Mathf.Min(thisScreenNum, cameraCt - 1);

        if (!myCamera) {
            Debug.LogError("Could not locate camera component.");
            return;
        }

        if (thisScreenNum < 0 || 4 < thisScreenNum) {
            Debug.LogWarning("Attempted to initialize camera whose screen number is \"" + thisScreenNum + "\"");
            return;
        }

        Rect[] viewRects;
        switch (cameraCt) {
            case 2:
                if (vertical2PRects)
                    viewRects = cameraRects2Player;
                else
                    viewRects = cameraRects2PlayerHorizontal;
                break;
            case 3:
                if (long3PRect)
                    viewRects = cameraRects3Player;
                else
                    viewRects = cameraRects4Player;
                break;
            case 4:
                viewRects = cameraRects4Player;
                break;
            default:
                viewRects = new Rect[1] { new Rect(0, 0, 1, 1) };
                break;
        }

        float x = viewRects[thisScreenNum].x, width  = viewRects[thisScreenNum].width;
        float y = viewRects[thisScreenNum].y, height = viewRects[thisScreenNum].height;

        if (cameraCt > 1) {
            x += joinedCameraPadding.x / (float)Screen.width;
            y += joinedCameraPadding.y / (float)Screen.height;
            width -= 2 * joinedCameraPadding.x / (float)Screen.width;
            height -= 2 * joinedCameraPadding.y / (float)Screen.height;
        }

        myCamera.rect = new Rect(x, y, width, height);
    }
}
