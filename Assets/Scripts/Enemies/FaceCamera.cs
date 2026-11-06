using UnityEngine;

namespace HyperInfinite
{
    /// Turns a flat sprite to face the camera every frame, so it never looks flat
    /// from the side. It only turns left and right, so it stays upright.
    public class FaceCamera : MonoBehaviour
    {
        Transform cameraTransform;

        void LateUpdate()
        {
            // Find the camera once and remember it
            if (!cameraTransform)
            {
                Camera cam = Camera.main ? Camera.main : FindFirstObjectByType<Camera>();
                if (!cam) return;
                cameraTransform = cam.transform;
            }

            // Face the same way the camera faces, ignoring tilt up and down
            Vector3 forward = cameraTransform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) return;
            transform.rotation = Quaternion.LookRotation(forward);
        }
    }
}