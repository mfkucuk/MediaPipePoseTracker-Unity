using Mediapipe.Unity.CoordinateSystem;
using System.Collections;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UI;

using Stopwatch = System.Diagnostics.Stopwatch;

namespace Mediapipe.Unity.Tutorial
{
    public class MyPoseTracker : MonoBehaviour
    {
        [SerializeField] private AnimationController _model;

        [SerializeField] private TextAsset _configAsset;
        [SerializeField] private RawImage _rawImage;
        [SerializeField] private int _width;
        [SerializeField] private int _height;
        [SerializeField] private int _fps;
        [SerializeField] private PoseLandmarkListAnnotationController _poseLandmarkListAnnotationController;

        private ArrayList _unityCoordinates;

        private const string INPUT_STREAM_NAME = "input_video";
        private const string POSE_DETECTION_NAME = "pose_detection";
        private const string POSE_LANDMARK_NAME = "pose_landmarks";
        private const string POSE_WORLD_LANDMARK_NAME = "pose_world_landmarks";
        private const string SEGMENTATION_MASK_NAME = "segmentation_mask";
        private const string ROI_FROM_LANDMARK_NAME = "roi_from_landmarks";

        private OutputStream<DetectionPacket, Detection> _poseDetectionStream;
        private OutputStream<NormalizedLandmarkListPacket, NormalizedLandmarkList> _poseLandmarksStream;
        private OutputStream<LandmarkListPacket, LandmarkList> _poseWorldLandmarksStream;
        private OutputStream<ImageFramePacket, ImageFrame> _segmentationMaskStream;
        private OutputStream<NormalizedRectPacket, NormalizedRect> _roiFromLandmarksStream;

        private CalculatorGraph _graph;
        private ResourceManager _resourceManager;

        private WebCamTexture _webCamTexture;
        private Texture2D _inputTexture;
        private Color32[] _inputPixelData;
        

        private IEnumerator Start()
        {
            if (WebCamTexture.devices.Length == 0)
            {
                throw new System.Exception("Web camera devices are not found.");
            }

            var webCamDevice = WebCamTexture.devices[0];
            _webCamTexture = new WebCamTexture(webCamDevice.name, _width, _height, _fps);
            _webCamTexture.Play();

            yield return new WaitUntil(() => _webCamTexture.width > 16);

            _rawImage.rectTransform.sizeDelta = new Vector2(_width, _height);
            _rawImage.texture = _webCamTexture;

            _inputTexture = new Texture2D(_width, _height, TextureFormat.RGBA32, false);
            _inputPixelData = new Color32[_width * _height];

            _resourceManager = new LocalResourceManager();
            yield return _resourceManager.PrepareAssetAsync("pose_detection.bytes");
            yield return _resourceManager.PrepareAssetAsync("pose_landmark_full.bytes");


            // Initalize side packet for feeding input into side graphs.
            var sidePacket = new SidePacket();
            sidePacket.Emplace("input_rotation", new IntPacket(0));
            sidePacket.Emplace("input_horizontally_flipped", new BoolPacket(false));
            sidePacket.Emplace("input_vertically_flipped", new BoolPacket(true));

            sidePacket.Emplace("output_rotation", new IntPacket(0));
            sidePacket.Emplace("output_horizontally_flipped", new BoolPacket(false));
            sidePacket.Emplace("output_vertically_flipped", new BoolPacket(false));

            _graph = new CalculatorGraph(_configAsset.text);
            var stopwatch = new Stopwatch();

            // Initialize output stream of the graph
            _poseDetectionStream = new OutputStream<DetectionPacket, Detection>(_graph, POSE_DETECTION_NAME);
            _poseLandmarksStream = new OutputStream<NormalizedLandmarkListPacket, NormalizedLandmarkList>(_graph, POSE_LANDMARK_NAME);
            _poseWorldLandmarksStream = new OutputStream<LandmarkListPacket, LandmarkList>(_graph, POSE_WORLD_LANDMARK_NAME);
            _segmentationMaskStream = new OutputStream<ImageFramePacket, ImageFrame>(_graph, SEGMENTATION_MASK_NAME);
            _roiFromLandmarksStream = new OutputStream<NormalizedRectPacket, NormalizedRect>(_graph, ROI_FROM_LANDMARK_NAME);
            _poseDetectionStream.StartPolling().AssertOk();
            _poseLandmarksStream.StartPolling().AssertOk();
            _poseWorldLandmarksStream.StartPolling().AssertOk();
            _segmentationMaskStream.StartPolling().AssertOk();
            _roiFromLandmarksStream.StartPolling().AssertOk();

            _graph.StartRun(sidePacket).AssertOk();
            stopwatch.Start();

            var screenRect = _rawImage.GetComponent<RectTransform>().rect;
            _unityCoordinates = new ArrayList(33);

            while (true)
            {
                _inputTexture.SetPixels32(_webCamTexture.GetPixels32(_inputPixelData));
                var imageFrame = new ImageFrame(ImageFormat.Types.Format.Srgba, _width, _height, _width * 4, _inputTexture.GetRawTextureData<byte>());
                var currentTimestamp = stopwatch.ElapsedTicks / (System.TimeSpan.TicksPerMillisecond / 1000);
                _graph.AddPacketToInputStream(INPUT_STREAM_NAME, new ImageFramePacket(imageFrame, new Timestamp(currentTimestamp))).AssertOk();

                yield return new WaitForEndOfFrame();

                // Retrieve the position of the landmarks
                if (_poseLandmarksStream.TryGetNext(out var poseLandmarks))
                {
                    if (poseLandmarks != null && poseLandmarks.CalculateSize() > 0)
                    {
                        _poseLandmarkListAnnotationController.DrawNow(poseLandmarks);

                        foreach (NormalizedLandmark landmark in poseLandmarks.Landmark)
                        {
                            _unityCoordinates.Add(screenRect.GetPoint(landmark));
                        }

                        StartCoroutine(_model.UpdateBones(_unityCoordinates));

                        _unityCoordinates.Clear();
                    }
                }
                            
            }
        }

        private void OnDestroy()
        {
            if (_webCamTexture != null)
            {
                _webCamTexture.Stop();
            }

            if (_graph != null)
            {
                try
                {
                    _graph.CloseInputStream(INPUT_STREAM_NAME).AssertOk();
                    _graph.WaitUntilDone().AssertOk();
                }
                finally
                {
                    _graph.Dispose();
                }
            }
        }
    }
}

