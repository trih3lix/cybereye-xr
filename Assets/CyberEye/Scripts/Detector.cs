using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.InferenceEngine;   // package displayName is "Sentis"; namespace is Unity.InferenceEngine (v2.6.1)

// M4: permissive COCO object detection (person / bird(=duck) / cat / dog) on the Eye feed.
// YOLOv8n with decode + ArgMax + NMS baked into a FunctionalGraph, so only the K survivors read back.
// Async readback (Awaitable) => never blocks the render thread. Falls back to a bundled test image when
// there's no live feed (e.g. 2D adb launch) so the pipeline is verifiable headlessly via logcat.
public class Detector : MonoBehaviour
{
    [Header("Model")]
    public ModelAsset modelAsset;                 // Assets/CyberEye/Models/yolov8n.onnx

    [Header("Source")]
    public EyeCameraFeed eyeFeed;                 // live camera source (PreviewTex)
    public Texture testTexture;                   // fallback (bundled photo) for headless verification
    [Tooltip("Run on the bundled test photo (headless parse/class validation) instead of the live feed.")]
    public bool preferTestImage = false;  // live Eye feed (set true only to headless-validate on the test photo)

    [Header("Params")]
    [Range(0f, 1f)] public float confidence = 0.25f;
    [Range(0f, 1f)] public float iou = 0.45f;
    [Tooltip("Run inference every N rendered frames (6 @60fps ~= 10Hz).")]
    public int inferenceInterval = 6;

    const int INPUT = 640;

    static readonly string[] Labels = {
        "person","bicycle","car","motorcycle","airplane","bus","train","truck","boat","traffic light",
        "fire hydrant","stop sign","parking meter","bench","bird","cat","dog","horse","sheep","cow",
        "elephant","bear","zebra","giraffe","backpack","umbrella","handbag","tie","suitcase","frisbee",
        "skis","snowboard","sports ball","kite","baseball bat","baseball glove","skateboard","surfboard","tennis racket","bottle",
        "wine glass","cup","fork","knife","spoon","bowl","banana","apple","sandwich","orange",
        "broccoli","carrot","hot dog","pizza","donut","cake","chair","couch","potted plant","bed",
        "dining table","toilet","tv","laptop","mouse","remote","keyboard","cell phone","microwave","oven",
        "toaster","sink","refrigerator","book","clock","vase","scissors","teddy bear","hair drier","toothbrush"
    };
    static bool IsWanted(int id) => id == 0 || id == 14 || id == 15 || id == 16; // person, bird(duck), cat, dog

    public struct Detection { public int classId; public string label; public float confidence; public float x, y, w, h; }
    readonly List<Detection> m_Results = new();
    public IReadOnlyList<Detection> Detections => m_Results;
    public int InferenceId => m_InferCount;   // increments each completed inference (for the tracker)

    Worker m_Worker;
    Tensor<float> m_Input;
    RenderTexture m_InputRT;
    TextureTransform m_Transform;
    int m_FrameCount, m_InferCount;
    bool m_Busy, m_Ready;

    void Awake()
    {
        if (modelAsset == null) { CyberLog.Err("DET", "modelAsset not assigned"); return; }
        try
        {
            Model baseModel = ModelLoader.Load(modelAsset);
            const float inv = 1f / INPUT;
            using var toCorners = new Tensor<float>(new TensorShape(4, 4), new float[] {
                 inv,      0f,       inv,      0f,
                 0f,       inv,      0f,       inv,
                -0.5f*inv, 0f,       0.5f*inv, 0f,
                 0f,      -0.5f*inv, 0f,       0.5f*inv });

            var graph  = new FunctionalGraph();
            var inputs = graph.AddInputs(baseModel);
            var output = Functional.Forward(baseModel, inputs)[0];      // (1,84,8400)
            var boxCoords = output[0, 0..4, ..].Transpose(0, 1);        // (8400,4) center xywh px
            var allScores = output[0, 4.., ..];                         // (80,8400)
            var scores    = Functional.ReduceMax(allScores, 0);         // (8400)
            var classIDs  = Functional.ArgMax(allScores, 0);            // (8400) int
            var cornersN  = Functional.MatMul(boxCoords, Functional.Constant(toCorners)); // (8400,4) xyxy 0..1
            var keep      = Functional.NMS(cornersN, scores, iou, confidence);
            var keptBoxes = Functional.IndexSelect(boxCoords, 0, keep);
            var keptClass = Functional.IndexSelect(classIDs, 0, keep);
            var keptScore = Functional.IndexSelect(scores, 0, keep);
            Model runtime = graph.Compile(keptBoxes, keptClass, keptScore);

            var backend = SystemInfo.supportsComputeShaders ? BackendType.GPUCompute : BackendType.CPU;
            m_Worker = new Worker(runtime, backend);
            m_Input = new Tensor<float>(new TensorShape(1, 3, INPUT, INPUT));
            m_InputRT = new RenderTexture(INPUT, INPUT, 0, RenderTextureFormat.ARGB32);
            m_Transform = new TextureTransform();
            m_Ready = true;
            CyberLog.Info("DET", $"init OK model=yolov8n backend={backend} computeShaders={SystemInfo.supportsComputeShaders}");
        }
        catch (Exception e) { CyberLog.Err("DET", "init FAILED: " + e.Message); }
    }

    Texture Source()
    {
        if (preferTestImage && testTexture != null) return testTexture;
        var live = eyeFeed != null ? eyeFeed.PreviewTex : null;
        return live != null ? live : testTexture;
    }

    void Update()
    {
        if (!m_Ready || m_Busy) return;
        if (Source() == null) return;
        if (++m_FrameCount < inferenceInterval) return;
        m_FrameCount = 0;
        _ = RunInferenceAsync();
    }

    async Awaitable RunInferenceAsync()
    {
        m_Busy = true;
        try
        {
            var src = Source();
            if (src == null) return;
            Graphics.Blit(src, m_InputRT);
            TextureConverter.ToTensor(m_InputRT, m_Input, m_Transform);   // [0,1] RGB NCHW
            m_Worker.Schedule(m_Input);

            var boxRef   = m_Worker.PeekOutput("output_0") as Tensor<float>;
            var clsRef   = m_Worker.PeekOutput("output_1") as Tensor<int>;
            var scoreRef = m_Worker.PeekOutput("output_2") as Tensor<float>;
            if (boxRef == null || clsRef == null || scoreRef == null) { CyberLog.Err("DET", "null output tensor"); return; }

            using var boxes   = await boxRef.ReadbackAndCloneAsync();
            using var classes = await clsRef.ReadbackAndCloneAsync();
            using var scores  = await scoreRef.ReadbackAndCloneAsync();
            ParseInto(boxes, classes, scores);
        }
        catch (Exception e) { CyberLog.Err("DET", "inference error: " + e.Message); }
        finally { m_Busy = false; }
    }

    void ParseInto(Tensor<float> boxes, Tensor<int> classes, Tensor<float> scores)
    {
        m_Results.Clear();
        int[]   cls = classes.DownloadToArray();
        float[] box = boxes.DownloadToArray();
        float[] sc  = scores.DownloadToArray();
        int rawK = cls.Length;
        for (int i = 0; i < rawK; i++)
        {
            int id = cls[i];
            if (!IsWanted(id)) continue;
            float conf = sc[i];
            if (conf < confidence) continue;
            float cx = box[i*4+0] / INPUT, cy = box[i*4+1] / INPUT, w = box[i*4+2] / INPUT, h = box[i*4+3] / INPUT;
            m_Results.Add(new Detection {
                classId = id, label = (id >= 0 && id < Labels.Length) ? Labels[id] : id.ToString(),
                confidence = conf, x = cx - w*0.5f, y = cy - h*0.5f, w = w, h = h });
        }

        m_InferCount++;
        if (m_Results.Count > 0)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < m_Results.Count && i < 6; i++) sb.Append($"{m_Results[i].label}:{m_Results[i].confidence:F2} ");
            CyberLog.Info("DET", $"#{m_InferCount} rawK={rawK} wanted={m_Results.Count} [{sb}]");
        }
        else if (m_InferCount % 10 == 0)
        {
            CyberLog.Info("DET", $"#{m_InferCount} rawK={rawK} wanted=0 (source={(Source()==testTexture ? "testImg" : "liveFeed")})");
        }
    }

    void OnDisable()
    {
        m_Worker?.Dispose(); m_Worker = null;
        m_Input?.Dispose(); m_Input = null;
        if (m_InputRT != null) { m_InputRT.Release(); m_InputRT = null; }
    }
}
