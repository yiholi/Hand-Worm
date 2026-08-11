using UnityEngine;

public class SkySphereRotator : MonoBehaviour
{
    [Tooltip("度/秒。這裡刻意取反向,與 DreamSlope 相反旋轉")]
    public float rotationSpeed = 3f;

    float currentAngle;

    void Start()
    {
        currentAngle = transform.localEulerAngles.y;
    }

    void Update()
    {
        currentAngle = Mathf.Repeat(currentAngle - rotationSpeed * Time.deltaTime, 360f);
        transform.localRotation = Quaternion.Euler(0f, currentAngle, 0f);
    }
}