using UnityEngine;

public class DreamSlopeRotator : MonoBehaviour
{
    [Tooltip("度/秒。正值 = Y 軸正向(俯視逆時針)")]
    public float rotationSpeed = 3f;

    float currentAngle;

    void Start()
    {
        currentAngle = transform.localEulerAngles.y;
    }

    void Update()
    {
        currentAngle = Mathf.Repeat(currentAngle + rotationSpeed * Time.deltaTime, 360f);
        transform.localRotation = Quaternion.Euler(0f, currentAngle, 0f);
    }
}