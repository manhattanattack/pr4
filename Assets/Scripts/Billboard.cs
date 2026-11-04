using UnityEngine;

public class Billboard : MonoBehaviour
{
    private Transform mainCameraTransform;

    private void Start()
    {
        FindCamera();
    }

    private void LateUpdate()
    {
        if (mainCameraTransform == null)
        {
            FindCamera();
            return;
        }

        // Плашка всегда строго параллельна плоскости экрана камеры
        transform.rotation = mainCameraTransform.rotation;
    }

    private void FindCamera()
    {
        if (Camera.main != null)
        {
            mainCameraTransform = Camera.main.transform;
        }
    }
}