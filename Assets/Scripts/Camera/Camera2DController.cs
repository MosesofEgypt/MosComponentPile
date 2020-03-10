using UnityEngine;


[AddComponentMenu("Camera/Camera 2D Controller")]
public class Camera2DController : MonoBehaviour {
    /* NOTE: Make sure this script is set to execute after anything that would change the
    ** translation or rotation of the target GameObject, otherwise this camera will lag behind
    */

    public enum RotationTarget {
        world,
        target,
    }

    [Tooltip("Camera will follow this GameObject.")]
    public GameObject target = null;

    [Tooltip("Camera will ignore target and follow this instead(for temporary camera hijacking).")]
    public GameObject targetOther = null;

    [Tooltip("The rotation vector the camera will use for \"world\" orientation.")]
    public Quaternion defaultRotation = new Quaternion(0f, 0f, 0f, 1f);

    [Tooltip("Camera will snap to keep target within this box.")]
    public Vector2 cameraDeadzone = new Vector2(0.5f, 0.5f);

    [Tooltip("Center offset of the deadzone.")]
    public Vector2 cameraOrigin = new Vector2(0.0f, -0.22f);

    public Color cameraDeadzoneColor = Color.red;

    public float cameraHeight = -10f;

    [Tooltip("Time in seconds to tween between local and world camera orientations.")]
    public float tweenTime = 0.3f;

    [Tooltip("Pixels per world unit on the sprite being followed. Used to make sure camera is pixel perfect.")]
    public float pixelsPerUnit = 64f;

    [Range(0.001f, 20f)]
    public float pixelScale = 1f;

    [Tooltip("Uses the above pixelsPerUnit to autoset the cameras orthographicSize so it is pixel perfect.")]
    public bool autosetOrthographicSize = true;

    public bool pixelPerfectPosition = false;

    public bool rotateWithTarget = true;

    [Tooltip("Overrides deadzone so it covers ~100% of the screen.")]
    public bool fullscreenDeadzone = false;

    [Tooltip("Camera will keep its center on the target at all times(zero deadzone).")]
    public bool stayOnTarget = false;

    RotationTarget rotationTarget = RotationTarget.world;
    Quaternion tweenOrigin = new Quaternion(0f, 0f, 0f, 1f);
    Quaternion tweenTarget = new Quaternion(0f, 0f, 0f, 1f);
    float tweenTimer;

    Camera myCamera;

    public float PixelsPerUnit { get { return Mathf.Max(0.000001f, pixelsPerUnit * pixelScale); } }

    void Awake() {
        myCamera = GetComponent<Camera>() as Camera;
    }

    void Start() {
        if (!target)
            Debug.LogError("No player GameObject to attach camera to.");
    }

    void Update() {
        if (!target)
            return;

        PositionUpdate();
        RotationUpdate(Time.deltaTime);

        if (myCamera && autosetOrthographicSize)
            // auto-calculates the orthographicSize to allow for pixel perfect cameras
            myCamera.orthographicSize = (Screen.height * myCamera.rect.height) / (2 * PixelsPerUnit);
    }

    protected void PositionUpdate() {
        GameObject currTarget = GetCameraTarget();

        if (!currTarget) return;
        Vector3 tempVector  = new Vector3(0f, 0f, 0f);
        Vector3 newPosition = new Vector3(0f, 0f, 0f);

        // get the players bounds within the frustum
        Vector3[] corners = GetCameraDeadzoneCorners(false);
        if (corners == null)
            return;

        newPosition = currTarget.transform.position;
        if (corners.Length >= 4) {
            // get the distance the player is from the cameras center and transform it
            // to local space so we can measure how far in/out of bounds the player is
            tempVector = (transform.InverseTransformPoint(newPosition) -
                          transform.InverseTransformPoint(transform.position));

            // clamp the players x and y position to within the camera bounds
            if (corners[0].x < corners[2].x)
                tempVector.x = Mathf.Clamp(tempVector.x, corners[0].x, corners[2].x);
            else
                tempVector.x = Mathf.Clamp(tempVector.x, corners[2].x, corners[0].x);

            if (corners[0].y < corners[2].y)
                tempVector.y = Mathf.Clamp(tempVector.y, corners[0].y, corners[2].y);
            else
                tempVector.y = Mathf.Clamp(tempVector.y, corners[2].y, corners[0].y);

            newPosition -= transform.TransformDirection(tempVector);
        }
        newPosition.z = cameraHeight;

        if (pixelPerfectPosition ) {
            // snap the camera position to a virtual pixel grid based on PixelsPerUnit
            double pixelStride = 1d / PixelsPerUnit;
            for (int i = 0; i < 2; i++)
                newPosition[i] = (float)(pixelStride * System.Math.Round(newPosition[i] / pixelStride));
        }

        transform.position = newPosition;
    }

    protected void RotationUpdate(float deltaTime) {
        GameObject currTarget = GetCameraTarget();
        if (!currTarget) return;

        float tweenRatio;
        Vector3 tempVector  = new Vector3(0f, 0f, 0f);

        RotationTarget currRotationTarget = GetCameraTargetType();

        if (currRotationTarget == rotationTarget) {
            // count how long we've been tweening to this target
            tweenTimer = Mathf.Min(tweenTime, tweenTimer + deltaTime);
        } else {
            tweenTimer = 0f;
            rotationTarget = currRotationTarget;
            if (rotationTarget == RotationTarget.world) {
                // we only need to be concerned with the players yaw
                tempVector.z = currTarget.transform.eulerAngles.z;
                tweenOrigin = Quaternion.Euler(tempVector);
            } else {
                // need to snap to camera. tween the rotation between the camera's current
                // rotation and the target rotation so the transition isn't disorienting.
                tweenOrigin = transform.rotation;
            }
        }

        // get our tween target
        if (rotationTarget == RotationTarget.target) {
            // we only need to be concerned with the players yaw
            tempVector.z = currTarget.transform.eulerAngles.z;
            tweenTarget = Quaternion.Euler(tempVector);
        } else
            tweenTarget = defaultRotation;

        // get how far into the tween we are(avoiding division by zero)
        tweenRatio = Mathf.Abs(tweenTimer / Mathf.Max(tweenTime, 0.000001f));
        if (tweenRatio >= 0.99999f)
            tweenRatio = 1f;

        if (tweenRatio == 0f)
            transform.rotation = tweenOrigin;
        else if (tweenRatio == 1f)
            transform.rotation = tweenTarget;
        else
            transform.rotation = Quaternion.Slerp(tweenOrigin, tweenTarget, tweenRatio);
    }

    public GameObject GetCameraTarget() {
        return (!targetOther) ? target : targetOther;
    }

    public RotationTarget GetCameraTargetType() {
        if (GetCameraTarget() != target)
            // target explicitely set
            return RotationTarget.target;
        else if (rotateWithTarget)
            return RotationTarget.target;

        return RotationTarget.world;
    }

    public Vector3[] GetCameraDeadzoneCorners(bool worldSpace = true) {
        if (!myCamera)
            return null;

        Vector3[] corners = new Vector3[5];
        Vector2 cameraOriginShift = new Vector2(0f, 0f);
        Vector2 currCameraDeadzone = new Vector2(0.9f, 0.85f);
        Vector2 currCameraOrigin   = cameraOrigin;

        if (myCamera) {
            myCamera.CalculateFrustumCorners(
                new Rect(0, 0, 1, 1),
                myCamera.farClipPlane,
                Camera.MonoOrStereoscopicEye.Mono,
                corners);

            if (fullscreenDeadzone)
                currCameraOrigin *= 0;
            else
                currCameraDeadzone = cameraDeadzone;

            if (stayOnTarget)
                currCameraDeadzone *= 0;

            cameraOriginShift.x = corners[2].x * 2 * currCameraOrigin.x;
            cameraOriginShift.y = corners[2].y * 2 * currCameraOrigin.y;

            // transform the corner coordinates to world space and apply the deadzone to them
            for (int i = 0; i < 4; i++) {
                corners[i].x = corners[i].x * currCameraDeadzone.x + cameraOriginShift.x;
                corners[i].y = corners[i].y * currCameraDeadzone.y + cameraOriginShift.y;

                if (worldSpace)
                    corners[i] = transform.TransformPoint(corners[i]);
            }
        }
        return corners;
    }

    [ExecuteInEditMode]
    void OnDrawGizmos() {
        Update();

        // Draws the camera movement deadzone on the screen
        Vector3[] corners = GetCameraDeadzoneCorners();
        if (corners == null)
            return;
        else if (corners.Length < 4)
            return;

        // copy the last point
        corners[4] = corners[0];

        // draw the edges
        for (int i = 0; i < 4; i++)
            Debug.DrawLine(corners[i], corners[i + 1], cameraDeadzoneColor, 0.0f, false);
    }
}
