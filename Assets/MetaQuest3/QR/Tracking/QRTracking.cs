using System;
using PassthroughCameraSamples;
using UnityEngine;
using ZXing;
using ZXing.Common;
using ZXing.QrCode;

public class QRTracking : MonoBehaviour
{
    public GameObject markerGO;

    [SerializeField] private bool startTrackingOnStart = true;
    [SerializeField] private WebCamTextureManager webCamTextureManager;
    [SerializeField] private PassthroughCameraPermissions cameraPermissions;
    // [SerializeField] private GameObject prefab;
    [SerializeField] private EnvironmentRayCastSampleManager environmentRayCastSampleManager;
    [SerializeField] private UPDirection upDirection = UPDirection.POINTCLOUD;
    [SerializeField] private bool useAxisMarker = true;
    [SerializeField] private GameObject axisMarkerPrefab;

    private WebCamTexture texture;
    private Vector3[] cornerPoints = new Vector3[3];
    private bool tracking;
    private GameObject axisMarkerGO;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (useAxisMarker && axisMarkerPrefab != null)
        {
            axisMarkerGO = Instantiate(axisMarkerPrefab, Vector3.zero, Quaternion.identity);
            axisMarkerGO.SetActive(false);
        }
        else
        {
            useAxisMarker = false;
            axisMarkerGO = null;
        }

        if (startTrackingOnStart)
        {
            StartTracking();
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (!tracking || webCamTextureManager.WebCamTexture == null) return;

        texture = webCamTextureManager.WebCamTexture;
        LuminanceSource luminanceSource = getLuminanceSource(texture);
        Result result = decodeQr(luminanceSource);


        if (result != null)
        {
            Debug.Log($"QR Code detected: {result.Text}");

            Vector3[] positions = new Vector3[3];
            Vector3[] normals = new Vector3[3];
            getPosesOfCorners(result, positions, normals);

            setPoseOfGO(positions, normals);

            if (useAxisMarker && !axisMarkerGO.activeSelf)
            {
                axisMarkerGO.SetActive(true);
            }
        }
        else
        {
            Debug.Log("No QR Code detected");
        }
    }

    private void setPoseOfGO(Vector3[] positions, Vector3[] normals)
    {
        Vector3 forward = (positions[0] - positions[1]).normalized;
        Vector3 right = (positions[2] - positions[1]).normalized;
        Vector3 up = Vector3.up;
        switch (upDirection)
        {
            case UPDirection.POINTCLOUD:
                for (int i = 0; i < normals.Length; i++)
                {
                    up += normals[i];
                }
                up /= normals.Length;
                break;

            case UPDirection.QR:
                up = Vector3.Cross(forward, right).normalized;
                break;
            case UPDirection.FLOOR:
                up = Vector3.up;
                break;
        }



        if (markerGO != null)
        {
            // Quaternion forwardQuaternion = Quaternion.LookRotation(Vector3.forward, up);
            // Vector3 center = (positions[0] + positions[1] + positions[2]) / 3;
            // markerGO.transform.SetPositionAndRotation(center, forwardQuaternion);
            // Quaternion upRot = Quaternion.FromToRotation(markerGO.transform.up, up);
            // markerGO.transform.rotation *= upRot;

            // Experiment
            markerGO.transform.up = up;
            markerGO.transform.LookAt(forward);
        }

        if (useAxisMarker && axisMarkerGO != null)
        {
            Vector3 upperCenter = (positions[1] + positions[2]) / 2;
            axisMarkerGO.transform.SetPositionAndRotation(upperCenter, markerGO.transform.rotation);
        }
    }

    private void getPosesOfCorners(Result result, Vector3[] positions, Vector3[] normals)
    {
        // the order is: bottom-left, top-left, top-right
        for (int i = 0; i < 3; i++)
        {
            var point = result.ResultPoints[i];
            var ray = PassthroughCameraUtils.ScreenPointToRayInWorld(webCamTextureManager.Eye, new Vector2Int((int)point.X, (int)point.Y));
            (Vector3? position, Vector3? normal) = environmentRayCastSampleManager.PlaceGameObjectByScreenPosAndRot(ray);
            UnityEngine.Assertions.Assert.IsTrue(position != null, $"Position is null for point {i}");
            UnityEngine.Assertions.Assert.IsTrue(normal != null, $"Normal is null for point {i}");
            positions[i] = position.Value;
            normals[i] = normal.Value;
        }
    }

    private static Result decodeQr(LuminanceSource luminanceSource)
    {
        // way 1 ZXing barcode reader
        // var barcodeReader = new BarcodeReaderGeneric { AutoRotate = false, Options = new ZXing.Common.DecodingOptions { TryHarder = false } };
        // var result = barcodeReader.Decode(luminanceSource);

        // way 2 Zxing QRCodeReader
        var qrReader = new QRCodeReader();
        var binarizer = new HybridBinarizer(luminanceSource);
        var binaryBitmap = new BinaryBitmap(binarizer);
        var result = qrReader.decode(binaryBitmap);

        return result;
    }

    private LuminanceSource getLuminanceSource(WebCamTexture texture)
    {
        var pixels = texture.GetPixels32();
        // way 1 custom luminance source
        // return new Color32LuminanceSource(pixels, texture.width, texture.height);

        // way 2 ZXing luminance source
        byte[] rawBytes = new byte[pixels.Length * 3];   // for RGB24
        for (int i = 0; i < pixels.Length; i++)
        {
            rawBytes[i*3 + 0] = pixels[i].r;
            rawBytes[i*3 + 1] = pixels[i].g;
            rawBytes[i*3 + 2] = pixels[i].b;
        }
        return new RGBLuminanceSource(rawBytes, texture.width, texture.height);
    }

    public void StartTracking()
    {
        if(PassthroughCameraPermissions.HasCameraPermission != true) cameraPermissions.AskCameraPermissions();
        // markerGO = Instantiate(prefab, Vector3.zero, Quaternion.identity);
        markerGO.SetActive(true);
        tracking = true;
    }

    public void StopTracking()
    {
        tracking = false;
        markerGO.SetActive(true);
    }

    private int getIndexWithMinDistance(Vector3[] points, Vector3 point)
    {
        int index = 0;
        float minDistance = float.MaxValue;
        for (int i = 0; i < points.Length; i++)
        {
            float distance = Vector3.Distance(points[i], point);
            if (distance < minDistance)
            {
                minDistance = distance;
                index = i;
            }
        }
        return index;
    }   
}

public enum UPDirection
{
    POINTCLOUD,
    QR,
    FLOOR
}

public class Color32LuminanceSource : BaseLuminanceSource

{
    /// <summary>
    /// Initializes a new instance of the <see cref="Color32LuminanceSource"/> class.
    /// </summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    public Color32LuminanceSource(int width, int height)
       : base(width, height)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Color32LuminanceSource"/> class.
    /// </summary>
    /// <param name="color32s">The color32s.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    public Color32LuminanceSource(Color32[] color32s, int width, int height)
       : base(width, height)
    {
        SetPixels(color32s);
    }

    /// <summary>
    /// Sets the pixels.
    /// </summary>
    /// <param name="color32s">The color32s.</param>
    public void SetPixels(Color32[] color32s)
    {
        var z = 0;

        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var color32 = color32s[y * Width + x];
                // Calculate luminance cheaply, favoring green.
                luminances[z++] = (byte)((
                   color32.r +
                   color32.g + color32.g +
                   color32.b) >> 2);
            }
        }
    }

    /// <summary>
    /// Should create a new luminance source with the right class type.
    /// The method is used in methods crop and rotate.
    /// </summary>
    /// <param name="newLuminances">The new luminances.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <returns></returns>
    protected override LuminanceSource CreateLuminanceSource(byte[] newLuminances, int width, int height)
    {
        return new Color32LuminanceSource(width, height) { luminances = newLuminances };
    }
}
