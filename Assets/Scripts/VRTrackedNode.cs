using UnityEngine;
using UnityEngine.XR;

// Отслеживает 6DoF через OpenXR без внешних моделей/сэмплов контроллеров.
public class VRTrackedNode : MonoBehaviour
{
    [SerializeField] private XRNode node;
    public void Configure(XRNode trackedNode) { node = trackedNode; }

    private void LateUpdate()
    {
        InputDevice device = InputDevices.GetDeviceAtXRNode(node);
        if (!device.isValid) return;
        if (device.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 position))
            transform.localPosition = position;
        if (device.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rotation))
            transform.localRotation = rotation;
    }
}
