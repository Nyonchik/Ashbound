using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public float smoothing = 8f;

    static float shakeTime, shakeAmount;
    Vector3 basePos;
    bool started;

    public static void Shake(float amount, float duration)
    {
        if (amount >= shakeAmount || shakeTime <= 0f) shakeAmount = amount;
        shakeTime = Mathf.Max(shakeTime, duration);
    }

    void LateUpdate()
    {
        if (target == null) return;
        if (!started) { basePos = transform.position; started = true; }
        basePos = Vector3.Lerp(basePos, target.position, 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime));
        basePos.z = -10f;

        Vector3 offset = Vector3.zero;
        if (shakeTime > 0f && Time.timeScale > 0f)
        {
            shakeTime -= Time.unscaledDeltaTime;
            offset = (Vector3)(Random.insideUnitCircle * shakeAmount);
            if (shakeTime <= 0f) shakeAmount = 0f;
        }
        transform.position = basePos + offset;
    }
}
