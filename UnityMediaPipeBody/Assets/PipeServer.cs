using System.Collections;

using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using UnityEngine;

/* Currently very messy because both the server code and hand-drawn code is all in the same file here.
 * But it is still fairly straightforward to use as a reference/base.
 */

public class PipeServer : MonoBehaviour
{
    public Transform parent;
    public GameObject landmarkPrefab;
    public GameObject linePrefab;
    public GameObject headPrefab;
    public bool anchoredBody = false;
    public bool enableHead = true;
    public float multiplier = 10f;
    public float landmarkScale = 1f;
    public float maxSpeed = 50f;
    public int samplesForPose = 1;

    private Body body;
    private NamedPipeServerStream server;

    const int LANDMARK_COUNT = 33;
    const int LINES_COUNT = 11;

    private Vector3 GetNormal(Vector3 p1, Vector3 p2, Vector3 p3)
    {
        Vector3 u = p2 - p1;
        Vector3 v = p3 - p1;
        Vector3 n = new Vector3((u.y * v.z - u.z * v.y), (u.z * v.x - u.x * v.z), (u.x * v.y - u.y * v.x));
        float nl = Mathf.Sqrt(n[0] * n[0] + n[1] * n[1] + n[2] * n[2]);
        return new Vector3(n[0] / nl, n[1] / nl, n[2] / nl);
    }

    public struct AccumulatedBuffer
    {
        public Vector3 value;
        public int accumulatedValuesCount;
        public AccumulatedBuffer(Vector3 v,int ac)
        {
            value = v;
            accumulatedValuesCount = ac;
        }
    }

    public class Body 
    {
        public Transform parent;
        public AccumulatedBuffer[] positionsBuffer = new AccumulatedBuffer[LANDMARK_COUNT];
        public Vector3[] localPositionTargets = new Vector3[LANDMARK_COUNT];
        public GameObject[] instances = new GameObject[LANDMARK_COUNT];
        public LineRenderer[] lines = new LineRenderer[LINES_COUNT];
        public GameObject head;

        public bool active;

        public bool setCalibration = false;
        public Vector3 calibrationOffset;

        public Vector3 virtualHeadPosition;

        public Body(Transform parent, GameObject landmarkPrefab, GameObject linePrefab,float s, GameObject headPrefab)
        {
            this.parent = parent;
            for (int i = 0; i < instances.Length; ++i)
            {
                instances[i] = Instantiate(landmarkPrefab);
                instances[i].transform.localScale = Vector3.one * s;
                instances[i].transform.parent = parent;
                instances[i].name = ((Landmark)i).ToString();

                if (headPrefab && i >= 0 && i <= 10)
                {
                    instances[i].transform.localScale = Vector3.one * 0f;
                }
            }
            for (int i = 0; i < lines.Length; ++i)
            {
                lines[i] = Instantiate(linePrefab).GetComponent<LineRenderer>();
            }

            if (headPrefab)
            {
                head = Instantiate(headPrefab);
                head.transform.localPosition = headPrefab.transform.position;
                head.transform.localRotation = headPrefab.transform.localRotation;
                head.transform.localScale = headPrefab.transform.localScale;
            }
        }
        public void UpdateLines()
        {
            lines[0].positionCount = 4;
            lines[0].SetPosition(0, Position((Landmark)32));
            lines[0].SetPosition(1, Position((Landmark)30));
            lines[0].SetPosition(2, Position((Landmark)28));
            lines[0].SetPosition(3, Position((Landmark)32));
            lines[1].positionCount = 4;
            lines[1].SetPosition(0, Position((Landmark)31));
            lines[1].SetPosition(1, Position((Landmark)29));
            lines[1].SetPosition(2, Position((Landmark)27));
            lines[1].SetPosition(3, Position((Landmark)31));

            lines[2].positionCount = 3;
            lines[2].SetPosition(0, Position((Landmark)28));
            lines[2].SetPosition(1, Position((Landmark)26));
            lines[2].SetPosition(2, Position((Landmark)24));
            lines[3].positionCount = 3;
            lines[3].SetPosition(0, Position((Landmark)27));
            lines[3].SetPosition(1, Position((Landmark)25));
            lines[3].SetPosition(2, Position((Landmark)23));

            lines[4].positionCount = 5;
            lines[4].SetPosition(0, Position((Landmark)24));
            lines[4].SetPosition(1, Position((Landmark)23));
            lines[4].SetPosition(2, Position((Landmark)11));
            lines[4].SetPosition(3, Position((Landmark)12));
            lines[4].SetPosition(4, Position((Landmark)24));

            lines[5].positionCount = 4;
            lines[5].SetPosition(0, Position((Landmark)12));
            lines[5].SetPosition(1, Position((Landmark)14));
            lines[5].SetPosition(2, Position((Landmark)16));
            lines[5].SetPosition(3, Position((Landmark)22));
            lines[6].positionCount = 4;
            lines[6].SetPosition(0, Position((Landmark)11));
            lines[6].SetPosition(1, Position((Landmark)13));
            lines[6].SetPosition(2, Position((Landmark)15));
            lines[6].SetPosition(3, Position((Landmark)21));

            lines[7].positionCount = 4;
            lines[7].SetPosition(0, Position((Landmark)16));
            lines[7].SetPosition(1, Position((Landmark)18));
            lines[7].SetPosition(2, Position((Landmark)20));
            lines[7].SetPosition(3, Position((Landmark)16));
            lines[8].positionCount = 4;
            lines[8].SetPosition(0, Position((Landmark)15));
            lines[8].SetPosition(1, Position((Landmark)17));
            lines[8].SetPosition(2, Position((Landmark)19));
            lines[8].SetPosition(3, Position((Landmark)15));

            if (!head)
            {
                lines[9].positionCount = 2;
                lines[9].SetPosition(0, Position((Landmark)10));
                lines[9].SetPosition(1, Position((Landmark)9));

                lines[10].positionCount = 5;
                lines[10].SetPosition(0, Position((Landmark)8));
                lines[10].SetPosition(1, Position((Landmark)5));
                lines[10].SetPosition(2, Position((Landmark)0));
                lines[10].SetPosition(3, Position((Landmark)2));
                lines[10].SetPosition(4, Position((Landmark)7));
            }
        }
        public void Calibrate()
        {
            Vector3 centre = (localPositionTargets[(int)Landmark.LEFT_HIP] + localPositionTargets[(int)Landmark.RIGHT_HIP]) / 2f;
            calibrationOffset = -centre;
            setCalibration = true;
        }

        public float GetAngle(Landmark referenceFrom, Landmark referenceTo, Landmark from, Landmark to)
        {
            Vector3 reference = (instances[(int)referenceTo].transform.position - instances[(int)referenceFrom].transform.position).normalized;
            Vector3 direction = (instances[(int)to].transform.position - instances[(int)from].transform.position).normalized;
            return Vector3.SignedAngle(reference, direction, Vector3.Cross(reference, direction));
        }
        public float Distance(Landmark from,Landmark to)
        {
            return (instances[(int)from].transform.position - instances[(int)to].transform.position).magnitude;
        }
        public Vector3 LocalPosition(Landmark Mark)
        {
            return instances[(int)Mark].transform.localPosition;
        }
        public Vector3 Position(Landmark Mark)
        {
            return instances[(int)Mark].transform.position;
        }

    }

    private void Start()
    {
        System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.InvariantCulture;

        body = new Body(parent,landmarkPrefab,linePrefab,landmarkScale,enableHead?headPrefab:null);

        Thread t = new Thread(new ThreadStart(Run));
        t.Start();

    }
    private void Update()
    {
        UpdateBody(body);
    }
    private void UpdateBody(Body b)
    {
        if (b.active == false) return;

        for (int i = 0; i < LANDMARK_COUNT; ++i)
        {
            if (b.positionsBuffer[i].accumulatedValuesCount < samplesForPose)
                continue;
            b.localPositionTargets[i] = b.positionsBuffer[i].value / (float)b.positionsBuffer[i].accumulatedValuesCount * multiplier;
            b.positionsBuffer[i] = new AccumulatedBuffer(Vector3.zero,0);
        }

        if (!b.setCalibration)
        {
            print("Set Calibration Data");
            b.Calibrate();

            if(FindObjectOfType<CameraController>())
                FindObjectOfType<CameraController>().Calibrate(b.instances[(int)Landmark.NOSE].transform);
        }

        for (int i = 0; i < LANDMARK_COUNT; ++i)
        {
            b.instances[i].transform.localPosition=Vector3.MoveTowards(b.instances[i].transform.localPosition, b.localPositionTargets[i]+b.calibrationOffset, Time.deltaTime * maxSpeed);
        }
        b.UpdateLines();

        b.virtualHeadPosition = (b.Position(Landmark.RIGHT_EAR) + b.Position(Landmark.LEFT_EAR)) / 2f;

        if (b.head)
        {
            // Experimental method and getting the head pose.
            b.head.transform.position = b.virtualHeadPosition+Vector3.up* .5f;
            Vector3 n1 = Vector3.Scale(new Vector3(.1f, 1f, .1f), GetNormal(b.Position((Landmark)0), b.Position((Landmark)8), b.Position((Landmark)7))).normalized;
            Vector3 n2 = Vector3.Scale(new Vector3(1f, .1f, 1f), GetNormal(b.Position((Landmark)0), b.Position((Landmark)4), b.Position((Landmark)1))).normalized;
            b.head.transform.rotation = Quaternion.LookRotation(-n2, n1);
        }
    }

    private void Run()
    {
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;

        // Open the named pipe.
        server = new NamedPipeServerStream("UnityMediaPipeBody",PipeDirection.InOut, 99, PipeTransmissionMode.Message);

        print("Waiting for connection...");
        server.WaitForConnection();

        print("Connected.");
        var br = new BinaryReader(server, Encoding.UTF8);

        while (true)
        {
            try
            {
                Body h = body;
                var len = (int)br.ReadUInt32();
                var str = new string(br.ReadChars(len));
                string[] lines = str.Split('\n');

                foreach (string l in lines)
                {
                    if (string.IsNullOrWhiteSpace(l))
                        continue;

                    string[] s = l.Split('|');
                    if (s.Length < 5) continue;

                    if (anchoredBody && s[0] != "ANCHORED") continue;
                    if (!anchoredBody && s[0] != "FREE") continue;

                    int i;
                    if (!int.TryParse(s[1], out i)) continue;
                    h.positionsBuffer[i].value += new Vector3(float.Parse(s[2]), float.Parse(s[3]), float.Parse(s[4]));
                    h.positionsBuffer[i].accumulatedValuesCount += 1;
                    h.active = true;
                }
            }
            catch (EndOfStreamException)
            {
                break;                    // When client disconnects
            }
        }

    }

    private void OnDisable()
    {
        print("Client disconnected.");
        server.Close();
        server.Dispose();
    }
}
