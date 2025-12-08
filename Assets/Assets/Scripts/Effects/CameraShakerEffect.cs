using UnityEngine;

namespace POTCO.Effects
{
    public class CameraShakerEffect : POTCOEffect
    {
        [Header("Shake Settings")]
        public float shakeSpeed = 0.1f;
        public float shakePower = 5.0f;
        public int numShakes = 1;
        
        private Transform cameraTransform;
        private Quaternion originalRotation;

        protected override void Start()
        {
            if (Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
                originalRotation = cameraTransform.localRotation;
            }
            
            // Calculate total duration based on shakes
            // Sequence: Rock1 (speed) + numShakes * (Rock2 + Rock3) (2*speed) + Rock4 (speed)
            // Total = speed + numShakes * 2 * speed + speed
            duration = shakeSpeed * (2 + numShakes * 2) + 0.1f;
            
            base.Start();
        }

        public override void StartEffect()
        {
            base.StartEffect();
            if (cameraTransform != null)
            {
                StartCoroutine(ShakeRoutine());
            }
        }

        private System.Collections.IEnumerator ShakeRoutine()
        {
            float power = shakePower;
            float speed = shakeSpeed;

            // Rock 1: Center -> (P, P, 0)
            yield return RotateTo(new Vector3(power, power, 0), speed);

            for (int i = 0; i < numShakes; i++)
            {
                // Rock 2: (P, P, 0) -> (-P, -P, 0)
                yield return RotateTo(new Vector3(-power, -power, 0), speed);
                
                // Rock 3: (-P, -P, 0) -> (P, P, 0)
                yield return RotateTo(new Vector3(power, power, 0), speed);
            }

            // Rock 4: (P, P, 0) -> Center
            yield return RotateTo(Vector3.zero, speed);
            
            // Reset
            cameraTransform.localRotation = originalRotation;
        }

        private System.Collections.IEnumerator RotateTo(Vector3 targetEuler, float time)
        {
            Quaternion startRot = cameraTransform.localRotation;
            Quaternion endRot = originalRotation * Quaternion.Euler(targetEuler); // Additive to original?
            // Python says hprInterval on base.cam. If base.cam is parented, it's local.
            // hpr is (Heading, Pitch, Roll) -> (Y, X, Z) in Unity?
            // Point3(power, power, 0) -> H=power, P=power, R=0.
            // Let's map H->Y, P->X, R->Z.
            endRot = originalRotation * Quaternion.Euler(targetEuler.y, targetEuler.x, targetEuler.z);

            float t = 0;
            while (t < time)
            {
                t += Time.deltaTime;
                // EaseInOut
                float k = t / time;
                k = k * k * (3f - 2f * k); // SmoothStep
                cameraTransform.localRotation = Quaternion.Slerp(startRot, endRot, k);
                yield return null;
            }
            cameraTransform.localRotation = endRot;
        }
    }
}
