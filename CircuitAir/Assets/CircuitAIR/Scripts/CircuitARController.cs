using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>
/// Tracks two images from one XR Reference Image Library:
/// - CircuitQR: anchors the virtual circuit.
/// - LupaMarker: tracks the physical magnifying-glass tip.
///
/// If circuitPrefab/lupaMarkerPrefab are not assigned, simple runtime objects are generated.
/// </summary>
[RequireComponent(typeof(ARTrackedImageManager))]
public class CircuitARController : MonoBehaviour
{
    [Header("AR Foundation")]
    [SerializeField] private ARTrackedImageManager trackedImageManager;

    [Header("Reference image names (case-sensitive)")]
    [SerializeField] private string circuitImageName = "CircuitQR";
    [SerializeField] private string lupaImageName = "LupaMarker";

    [Header("Optional prefabs")]
    [Tooltip("Optional. If empty, the example circuit is generated at runtime.")]
    [SerializeField] private GameObject circuitPrefab;
    [Tooltip("Optional. If empty, a sphere trigger with LupaTip is generated at runtime.")]
    [SerializeField] private GameObject lupaMarkerPrefab;

    [Header("Local alignment on the tracked image")]
    [SerializeField] private Vector3 circuitLocalPosition = new Vector3(0f, 0f, 0.004f);
    [SerializeField] private Vector3 circuitLocalEulerOffset = Vector3.zero;
    [SerializeField] private Vector3 lupaRootLocalPosition = Vector3.zero;
    [Tooltip("Only used when the lupa marker prefab is empty. Offset of the trigger sphere from the center of the tracked LupaMarker image.")]
    [SerializeField] private Vector3 generatedLupaTipLocalPosition = Vector3.zero;

    [Header("Optional status text")]
    [SerializeField] private TMP_Text statusText;

    private readonly Dictionary<TrackableId, GameObject> spawnedContent = new Dictionary<TrackableId, GameObject>();
    private bool circuitImageTracked;
    private bool lupaImageTracked;

    private void Awake()
    {
        if (trackedImageManager == null)
            trackedImageManager = GetComponent<ARTrackedImageManager>();
    }

    private void OnEnable()
    {
        if (trackedImageManager == null)
            trackedImageManager = GetComponent<ARTrackedImageManager>();

        if (trackedImageManager != null)
            trackedImageManager.trackedImagesChanged += OnTrackedImagesChanged;
        else
            Debug.LogError("CircuitARController precisa de um ARTrackedImageManager.", this);
    }

    private void OnDisable()
    {
        if (trackedImageManager != null)
            trackedImageManager.trackedImagesChanged -= OnTrackedImagesChanged;
    }

    private void OnTrackedImagesChanged(ARTrackedImagesChangedEventArgs eventArgs)
    {
        foreach (ARTrackedImage image in eventArgs.added)
            UpdateTrackedImage(image);

        foreach (ARTrackedImage image in eventArgs.updated)
            UpdateTrackedImage(image);

        foreach (ARTrackedImage image in eventArgs.removed)
            RemoveTrackedImage(image);

        RefreshStatusText();
    }

    private void UpdateTrackedImage(ARTrackedImage trackedImage)
    {
        string imageName = trackedImage.referenceImage.name;
        if (imageName != circuitImageName && imageName != lupaImageName)
            return;

        bool isTracked = trackedImage.trackingState == TrackingState.Tracking;
        if (imageName == circuitImageName)
            circuitImageTracked = isTracked;
        else
            lupaImageTracked = isTracked;

        if (!spawnedContent.TryGetValue(trackedImage.trackableId, out GameObject content) || content == null)
        {
            content = CreateContentForImage(trackedImage, imageName);
            if (content == null)
                return;

            spawnedContent[trackedImage.trackableId] = content;
        }

        // Content is parented to ARTrackedImage, so it follows the tracked pose automatically.
        content.SetActive(isTracked);
        RefreshStatusText();
    }

    private GameObject CreateContentForImage(ARTrackedImage trackedImage, string imageName)
    {
        GameObject content;

        if (imageName == circuitImageName)
        {
            if (circuitPrefab != null)
            {
                content = Instantiate(circuitPrefab);
            }
            else
            {
                content = new GameObject("CircuitAIR_Circuit_Runtime");
                content.AddComponent<CircuitLayoutBuilder>();
            }

            content.name = "CircuitAIR_Circuit_Tracked";
            content.transform.SetParent(trackedImage.transform, false);
            content.transform.localPosition = circuitLocalPosition;
            content.transform.localRotation = Quaternion.Euler(circuitLocalEulerOffset);
            content.transform.localScale = Vector3.one;
            return content;
        }

        if (lupaMarkerPrefab != null)
        {
            content = Instantiate(lupaMarkerPrefab);
        }
        else
        {
            content = new GameObject("CircuitAIR_LupaMarker_Runtime");
            GameObject tipObject = new GameObject("LupaTip");
            tipObject.transform.SetParent(content.transform, false);
            tipObject.transform.localPosition = generatedLupaTipLocalPosition;
            tipObject.AddComponent<LupaTip>();
        }

        content.name = "CircuitAIR_Lupa_Tracked";
        content.transform.SetParent(trackedImage.transform, false);
        content.transform.localPosition = lupaRootLocalPosition;
        content.transform.localRotation = Quaternion.identity;
        content.transform.localScale = Vector3.one;

        if (lupaMarkerPrefab != null && content.GetComponentInChildren<LupaTip>() == null)
            Debug.LogWarning("O prefab da lupa precisa ter um objeto filho com o componente LupaTip.", content);

        return content;
    }

    private void RemoveTrackedImage(ARTrackedImage trackedImage)
    {
        string imageName = trackedImage.referenceImage.name;
        if (imageName == circuitImageName)
            circuitImageTracked = false;
        else if (imageName == lupaImageName)
            lupaImageTracked = false;

        if (spawnedContent.TryGetValue(trackedImage.trackableId, out GameObject content))
        {
            if (content != null)
                Destroy(content);
            spawnedContent.Remove(trackedImage.trackableId);
        }
    }

    private void RefreshStatusText()
    {
        if (statusText == null)
            return;

        if (circuitImageTracked && lupaImageTracked)
            statusText.text = "Circuito e lupa rastreados";
        else if (circuitImageTracked)
            statusText.text = "QR Code detectado. Mostre o marcador preso à lupa.";
        else if (lupaImageTracked)
            statusText.text = "Marcador da lupa detectado. Aponte para o QR Code.";
        else
            statusText.text = "Aponte a câmera para o QR Code.";
    }

    private void OnDestroy()
    {
        foreach (KeyValuePair<TrackableId, GameObject> entry in spawnedContent)
        {
            if (entry.Value != null)
                Destroy(entry.Value);
        }
        spawnedContent.Clear();
    }
}
