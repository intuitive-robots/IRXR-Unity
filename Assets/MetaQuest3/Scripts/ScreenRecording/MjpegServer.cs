using UnityEngine;
using System.Net;
using System.IO;
using System.Threading;
using IRXR.Node;
using Anaglyph.DisplayCapture;
using System;

public class MjpegServer : MonoBehaviour
{
    // public Camera streamCamera;
    public int serverPort = 8080;

    private HttpListener httpListener;
    private Texture2D frameTexture;
    private bool isRunning = false;
    // private Texture2D _frameTexture;
    private RenderTexture renderTex;


    void Start()
    {
        // Set up the RenderTexture and Texture2D
        // renderTex = new RenderTexture(640, 360, 24);
        // streamCamera.targetTexture = renderTex;
        // _frameTexture = new Texture2D(renderTex.width, renderTex.height, TextureFormat.RGB24, false);

        // Start HTTP server
        httpListener = new HttpListener();
        httpListener.Prefixes.Add($"http://*: {serverPort}/");
        httpListener.Start();
        isRunning = true;
        ThreadPool.QueueUserWorkItem(ListenerLoop);

        frameTexture = DisplayCaptureManager.Instance.ScreenCaptureTexture;
        IRXRNetManager.Instance.serviceCallbacks.Add("ActivateScreenStreaming", ActivateScreenStreaming);
    }

    void OnDestroy()
    {
        isRunning = false;
        httpListener.Stop();
    }

    private void ListenerLoop(object state)
    {
        while (isRunning)
        {
            HttpListenerContext context = httpListener.GetContext();
            HttpListenerResponse response = context.Response;

            response.ContentType = "multipart/x-mixed-replace; boundary=frame";
            response.StatusCode = (int)HttpStatusCode.OK;

            using (Stream outputStream = response.OutputStream)
            {
                while (isRunning && outputStream.CanWrite)
                {
                    // // Read pixels from the camera
                    // RenderTexture.active = renderTex;
                    // _frameTexture.ReadPixels(new Rect(0,0, renderTex.width, renderTex.height), 0,0);
                    // _frameTexture.Apply();

                    byte[] jpg = frameTexture.EncodeToJPG(); // _frameTexture.EncodeToJPG();

                    // Write the boundary
                    string header = "\r\n--frame\r\n" +
                                    "Content-Type: image/jpeg\r\n" +
                                    "Content-Length: " + jpg.Length + "\r\n\r\n";
                    byte[] headerBytes = System.Text.Encoding.UTF8.GetBytes(header);

                    try
                    {
                        outputStream.Write(headerBytes, 0, headerBytes.Length);
                        outputStream.Write(jpg, 0, jpg.Length);
                        outputStream.Flush();
                    }
                    catch
                    {
                        // Client disconnected
                        break;
                    }

                    Thread.Sleep(50); // ~20 FPS
                }
            }
        }
    }

    byte[] ActivateScreenStreaming(byte[] message)
    {
        isRunning = true;
        HttpListenerContext context = httpListener.GetContext();
        HttpListenerRequest request = context.Request;
        Uri url = request.Url;
        return System.Text.Encoding.UTF8.GetBytes(url.AbsolutePath);
    }
}
