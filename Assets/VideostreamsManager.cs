using System.Collections.Generic;
using IRXR.Node;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;


public class VideostreamsManager : MonoBehaviour
{
    public Canvas canvas; // Reference to the Canvas where video streams will be displayed
    private Dictionary<string, RawImage> videoStreams;
    private Service<VideoStreamRequest, bool> videoStreamService;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        videoStreams = new Dictionary<string, RawImage>();
        videoStreamService = new Service<VideoStreamRequest, bool>(
            "VideoStream",
            (request) => HandleVideoStreamRequest(request)
        );
    }

    private bool HandleVideoStreamRequest(VideoStreamRequest request)
    {
        switch (request.Operation)
        {
            case VideoStreamOperation.Create:
                return CreateVideoStream(request.VideoStreamId, request.Position, request.Rotation);
            case VideoStreamOperation.Update:
                return UpdateVideoStream(request.VideoStreamId, request.ImageData);
            case VideoStreamOperation.Remove:
                return RemoveVideoStream(request.VideoStreamId);
            default:
                Debug.LogWarning($"Unknown video stream operation: {request.Operation}");
                return false;
        }
    }


    public bool CreateVideoStream(string videoStreamId, Vector3 position, Quaternion rotation)
    {
        if (videoStreams.ContainsKey(videoStreamId))
        {
            Debug.LogWarning($"Video stream with ID {videoStreamId} already exists.");
            return false; // Return false if the video stream already exists
        }

        // Logic to add a video stream
        Debug.Log($"Video stream '{videoStreamId}' added at position {position} with rotation {rotation}.");
        // create a new RawImage and Canvas for the video stream
        GameObject videoStreamObject = new GameObject(videoStreamId);
        RawImage rawImage = videoStreamObject.AddComponent<RawImage>();
        videoStreamObject.transform.SetParent(canvas.transform);
        RectTransform rt = videoStreamObject.GetComponent<RectTransform>();
        rt.SetParent(canvas.transform, false);
        rawImage.SetNativeSize();
        videoStreamObject.transform.SetPositionAndRotation(position, rotation);
        videoStreams[videoStreamId] = rawImage;
        return true; // Return true if successful
    }

    /// <summary>
    /// Updates an existing video stream with new image data.
    /// </summary>
    /// <param name="videoStreamId">The ID of the video stream to update.</param>
    /// <param name="imageData">The new JPG/PNG image data for the video stream.</param>
    /// <returns>True if the video stream was updated successfully; otherwise, false.</returns>
    public bool UpdateVideoStream(string videoStreamId, byte[] imageData)
    {
        // Logic to update a video stream
        Debug.Log($"Video stream '{videoStreamId}' updated with new image data.");
        if (videoStreams.TryGetValue(videoStreamId, out RawImage rawImage))
        {
            // Update the texture of the RawImage with the new image data
            Texture2D texture = new Texture2D(2, 2);
            // https://docs.unity3d.com/530/Documentation/ScriptReference/Texture2D.LoadImage.html
            texture.LoadImage(imageData);

            rawImage.texture = texture;
            return true; // Return true if successful
        }
        return false; // Return false if the video stream was not found
    }

    public bool RemoveVideoStream(string videoStreamId)
    {
        // Logic to remove a video stream
        Debug.Log($"Video stream '{videoStreamId}' removed.");
        if (videoStreams.TryGetValue(videoStreamId, out RawImage rawImage))
        {
            Destroy(rawImage.gameObject);
            return videoStreams.Remove(videoStreamId);
        }
        return false;
    }

    // Enum and class for the unified video stream service
    public enum VideoStreamOperation
    {
        Create,
        Update,
        Remove
    }

    public class VideoStreamRequest
    {
        public VideoStreamOperation Operation { get; set; }
        public string VideoStreamId { get; set; }
        public Vector3 Position { get; set; }
        public Quaternion Rotation { get; set; }
        public byte[] ImageData { get; set; }
    }
}
