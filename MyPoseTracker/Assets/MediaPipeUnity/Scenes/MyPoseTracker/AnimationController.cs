using Mediapipe;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class AnimationController : MonoBehaviour
{
    public class JointPoint
    {
        public Vector2 Pos2D = new Vector2();
        public float score2D;

        public Vector3 Pos3D = new Vector3();
        public Vector3 Now3D = new Vector3();
        public Vector3 PrevPos3D = new Vector3();
        public float score3D;

        // Bones
        public Transform transform = null;
        public Quaternion InitRotation;
        public Quaternion Inverse;

        public JointPoint Child = null;
    }

    // Joint position and bone
    private JointPoint[] jointPoints;

    private Vector3 initPosition; // Initial center position

    private Quaternion InitGazeRotation;
    private Quaternion gazeInverse;

    [SerializeField] private GameObject Nose;
    [SerializeField] private GameObject LeftEar;
    [SerializeField] private GameObject RightEar;
    [SerializeField] private GameObject LeftEye;
    [SerializeField] private GameObject RightEye;


    [SerializeField] private SkinnedMeshRenderer _betaJoints;
    [SerializeField] private SkinnedMeshRenderer _betaSurfaces;
    private Vector3[] _initialPositions;

    private const float MULTIPLIER = 5f;

    private const int HIP = 0;
    private const int SPINE = 1;
    private const int SPINE1 = 2;
    private const int NECK = 4;
    private const int HEAD = 5;
    private const int LEFT_SHOULDER = 7;
    private const int LEFT_FOREARM = 8;
    private const int LEFT_HAND = 9;
    private const int LEFT_HAND_THUMB = 11;
    private const int LEFT_HAND_MIDDLE = 16;
    private const int RIGHT_SHOULDER = 26;
    private const int RIGHT_FOREARM = 27;
    private const int RIGHT_HAND = 28;
    private const int RIGHT_HAND_THUMB = 42;
    private const int RIGHT_HAND_MIDDLE = 35;
    private const int LEFT_UP_LEG = 44;
    private const int LEFT_LEG = 45;
    private const int LEFT_FOOT = 46;
    private const int LEFT_TOE = 47;
    private const int RIGHT_UP_LEG = 48;
    private const int RIGHT_LEG = 49;
    private const int RIGHT_FOOT = 50;
    private const int RIGHT_TOE = 51;
    private const int NOSE = 52;
    private const int LEFT_EAR = 53;
    private const int RIGHT_EAR = 54;
    private const int LEFT_EYE = 55;
    private const int RIGHT_EYE = 56;

    private const int BONE_COUNT = 57;

    private void Start()
    {
        var i = 0;
        foreach (Transform bone in _betaJoints.bones)
        {
            Debug.Log(bone.name + " " + i++);
        }

        Init();
    }

    public void Init()
    {
        jointPoints = new JointPoint[BONE_COUNT];
        for (var i = 0; i < BONE_COUNT; i++) jointPoints[i] = new JointPoint();

        _initialPositions = new Vector3[BONE_COUNT];

        var index = 0;
        foreach (Transform bone in _betaJoints.bones)
        {
            jointPoints[index].transform = bone;    
            _initialPositions[index] = bone.localPosition;
            index++;
        }

        jointPoints[NOSE].transform = Nose.transform;
        _initialPositions[NOSE] = Nose.transform.localPosition;

        jointPoints[LEFT_EAR].transform = LeftEar.transform;
        _initialPositions[LEFT_EAR] = LeftEar.transform.localPosition;

        jointPoints[RIGHT_EAR].transform = RightEar.transform;
        _initialPositions[RIGHT_EAR] = RightEar.transform.localPosition;

        jointPoints[LEFT_EYE].transform = LeftEye.transform;
        _initialPositions[LEFT_EYE] = LeftEye.transform.localPosition;

        jointPoints[RIGHT_EYE].transform = RightEye.transform;
        _initialPositions[RIGHT_EYE] = RightEye.transform.localPosition;



        // Children
        // Right Arm
        jointPoints[RIGHT_SHOULDER].Child = jointPoints[RIGHT_FOREARM];
        jointPoints[RIGHT_FOREARM].Child = jointPoints[RIGHT_HAND];

        // Left Hand
        jointPoints[LEFT_SHOULDER].Child = jointPoints[LEFT_FOREARM];
        jointPoints[LEFT_FOREARM].Child = jointPoints[LEFT_HAND];

        // Right Leg
        jointPoints[RIGHT_UP_LEG].Child = jointPoints[RIGHT_LEG];
        jointPoints[RIGHT_LEG].Child = jointPoints[RIGHT_FOOT];
        jointPoints[RIGHT_FOOT].Child = jointPoints[RIGHT_TOE];

        // Left Leg
        jointPoints[LEFT_UP_LEG].Child = jointPoints[LEFT_LEG];
        jointPoints[LEFT_LEG].Child = jointPoints[LEFT_FOOT];
        jointPoints[LEFT_FOOT].Child = jointPoints[LEFT_TOE];

        // Main body
        jointPoints[SPINE].Child = jointPoints[SPINE1];
        jointPoints[SPINE1].Child = jointPoints[NECK];
        jointPoints[NECK].Child = jointPoints[HEAD];
        //jointPoints[SPINE].Child = jointPoints[SPINE1];

        // Set Inverse
        foreach (var jointPoint in jointPoints)
        {
            if (jointPoint.transform != null)
            {
                jointPoint.InitRotation = jointPoint.transform.rotation;
            }

            if (jointPoint.Child != null)
            {
                jointPoint.Inverse = GetInverse(jointPoint, jointPoint.Child);
            }
        }

        initPosition = jointPoints[HIP].transform.position;
        var forward = TriangleNormal(jointPoints[HIP].transform.position, jointPoints[LEFT_UP_LEG].transform.position, jointPoints[RIGHT_UP_LEG].transform.position);
        jointPoints[HIP].Inverse = Quaternion.Inverse(Quaternion.LookRotation(forward));

        // Head rotation
        jointPoints[HEAD].InitRotation = jointPoints[HEAD].transform.rotation;
        var gaze = jointPoints[NOSE].transform.position - jointPoints[HEAD].transform.position;
        jointPoints[HEAD].Inverse = Quaternion.Inverse(Quaternion.LookRotation(gaze));

        // Left hand rotation
        jointPoints[LEFT_HAND].InitRotation = jointPoints[LEFT_HAND].transform.rotation;
        jointPoints[LEFT_HAND].Inverse = Quaternion.Inverse(Quaternion.LookRotation(jointPoints[LEFT_HAND_THUMB].transform.position - jointPoints[LEFT_HAND_MIDDLE].transform.position));

        // Right hand rotation
        jointPoints[RIGHT_HAND].InitRotation = jointPoints[RIGHT_HAND].transform.rotation;
        jointPoints[RIGHT_HAND].Inverse = Quaternion.Inverse(Quaternion.LookRotation(jointPoints[RIGHT_HAND_THUMB].transform.position - jointPoints[RIGHT_HAND_MIDDLE].transform.position));

    }

    public IEnumerator UpdateBones(ArrayList landmarkList) 
    {
        Predict(landmarkList);

        var forward = TriangleNormal(jointPoints[HIP].Pos3D, jointPoints[LEFT_UP_LEG].Pos3D, jointPoints[RIGHT_UP_LEG].Pos3D);
        jointPoints[HIP].transform.position = jointPoints[HIP].Pos3D * 0.01f + new Vector3(initPosition.x, 0f, initPosition.z);
        jointPoints[HIP].transform.rotation = Quaternion.LookRotation(forward) * jointPoints[HIP].Inverse * jointPoints[HIP].InitRotation;

        // Set rotation based on the child joint
        foreach (var jointPoint in jointPoints)
        {
            if (jointPoint.Child != null)
            {

                jointPoint.transform.rotation = Quaternion.LookRotation(jointPoint.Pos3D - jointPoint.Child.Pos3D, forward) * jointPoint.Inverse * jointPoint.InitRotation;
            }
        }

        // Head Rotation
        var gaze = jointPoints[NOSE].Pos3D - jointPoints[HEAD].Pos3D;
        var f = TriangleNormal(jointPoints[NOSE].Pos3D, jointPoints[RIGHT_EYE].Pos3D, jointPoints[LEFT_EYE].Pos3D);
        var head = jointPoints[HEAD];
        head.transform.rotation = Quaternion.LookRotation(gaze, f) * head.Inverse * head.InitRotation;

        // Wrist rotation (Test code)
        var lf = TriangleNormal(jointPoints[LEFT_HAND].Pos3D, jointPoints[LEFT_HAND_MIDDLE].Pos3D, jointPoints[LEFT_HAND_THUMB].Pos3D);
        var lHand = jointPoints[LEFT_HAND];
        lHand.transform.rotation = Quaternion.LookRotation(jointPoints[LEFT_HAND_THUMB].Pos3D - jointPoints[LEFT_HAND_MIDDLE].Pos3D, lf) * lHand.Inverse * lHand.InitRotation;
        var rf = TriangleNormal(jointPoints[RIGHT_HAND].Pos3D, jointPoints[RIGHT_HAND_THUMB].Pos3D, jointPoints[RIGHT_HAND_MIDDLE].Pos3D);
        var rHand = jointPoints[RIGHT_HAND];
        rHand.transform.rotation = Quaternion.LookRotation(jointPoints[RIGHT_HAND_THUMB].Pos3D - jointPoints[RIGHT_HAND_MIDDLE].Pos3D, rf) * rHand.Inverse * rHand.InitRotation;

        yield return null;
    }

    public void Predict(ArrayList landmarkList)
    {
        // Assign the joint positions to landmark positions
        jointPoints[NOSE].Now3D = _initialPositions[NOSE] + (Vector3)(landmarkList[0]);
        jointPoints[LEFT_EAR].Now3D = _initialPositions[LEFT_EAR] + (Vector3)landmarkList[7];
        jointPoints[RIGHT_EAR].Now3D = _initialPositions[RIGHT_EAR] + (Vector3)landmarkList[8];
        jointPoints[LEFT_EYE].Now3D = _initialPositions[LEFT_EYE] + (Vector3)landmarkList[2];
        jointPoints[RIGHT_EYE].Now3D = _initialPositions[RIGHT_EYE] + (Vector3)landmarkList[5];
        jointPoints[LEFT_SHOULDER].Now3D = _initialPositions[LEFT_SHOULDER] + (Vector3)landmarkList[11];
        jointPoints[LEFT_FOREARM].Now3D = _initialPositions[LEFT_FOREARM] + (Vector3)landmarkList[13];
        jointPoints[LEFT_HAND].Now3D = _initialPositions[LEFT_HAND] + (Vector3)landmarkList[15];
        jointPoints[LEFT_HAND_MIDDLE].Now3D = _initialPositions[LEFT_HAND_MIDDLE] + (Vector3)landmarkList[19];
        jointPoints[LEFT_HAND_THUMB].Now3D = _initialPositions[LEFT_HAND_THUMB] + (Vector3)landmarkList[21];
        jointPoints[RIGHT_SHOULDER].Now3D = _initialPositions[RIGHT_SHOULDER] + (Vector3)landmarkList[12];
        jointPoints[RIGHT_FOREARM].Now3D = _initialPositions[RIGHT_FOREARM] + (Vector3)landmarkList[14];
        jointPoints[RIGHT_HAND].Now3D = _initialPositions[RIGHT_HAND] + (Vector3)landmarkList[16];
        jointPoints[RIGHT_HAND_MIDDLE].Now3D = _initialPositions[RIGHT_HAND_MIDDLE] + (Vector3)landmarkList[20];
        jointPoints[RIGHT_HAND_THUMB].Now3D = _initialPositions[RIGHT_HAND_THUMB] + (Vector3)landmarkList[22];
        jointPoints[LEFT_UP_LEG].Now3D = _initialPositions[LEFT_UP_LEG] + (Vector3)landmarkList[23];
        jointPoints[LEFT_LEG].Now3D = _initialPositions[LEFT_LEG] + (Vector3)landmarkList[25];
        jointPoints[LEFT_FOOT].Now3D = _initialPositions[LEFT_FOOT] + (Vector3)landmarkList[27];
        jointPoints[LEFT_TOE].Now3D = _initialPositions[LEFT_TOE] + (Vector3)landmarkList[31];
        jointPoints[RIGHT_UP_LEG].Now3D = _initialPositions[RIGHT_UP_LEG] + (Vector3)landmarkList[24];
        jointPoints[RIGHT_LEG].Now3D = _initialPositions[RIGHT_LEG] + (Vector3)landmarkList[26];
        jointPoints[RIGHT_FOOT].Now3D = _initialPositions[RIGHT_FOOT] + (Vector3)landmarkList[28];
        jointPoints[RIGHT_TOE].Now3D = _initialPositions[RIGHT_TOE] + (Vector3)landmarkList[32];

        // Calculate the points that are not avalaible to us in MediaPipe
        // hip
        var lc = (jointPoints[LEFT_UP_LEG].Now3D + jointPoints[RIGHT_UP_LEG].Now3D) / 2f;
        var neck = (jointPoints[LEFT_SHOULDER].Now3D + jointPoints[RIGHT_SHOULDER].Now3D) / 2f;
        jointPoints[SPINE].Now3D = (lc + neck) / 2f;
        jointPoints[SPINE1].Now3D = (jointPoints[SPINE].Now3D + neck) / 2f;
        jointPoints[NECK].Now3D = neck;

        jointPoints[HIP].Now3D = (jointPoints[SPINE].Now3D + lc) / 2f;

        // head
        var cEar = (jointPoints[LEFT_EAR].Now3D + jointPoints[RIGHT_EAR].Now3D  ) / 2f;
        var hv = cEar - jointPoints[NECK].Now3D;
        var nhv = Vector3.Normalize(hv);
        var nv = jointPoints[NOSE].Now3D - jointPoints[NECK].Now3D;
        jointPoints[HEAD].Now3D = jointPoints[NECK].Now3D + nhv * Vector3.Dot(nhv, nv);

        foreach (var jp in jointPoints)
        {
            jp.Pos3D = jp.PrevPos3D * 0.5f + jp.Now3D * 0.5f;
            jp.PrevPos3D = jp.Pos3D;
        }
    }

    Vector3 TriangleNormal(Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 d1 = a - b;
        Vector3 d2 = a - c;

        Vector3 dd = Vector3.Cross(d1, d2);
        dd.Normalize();

        return dd;
    }

    private Quaternion GetInverse(JointPoint p1, JointPoint p2)
    {
        return Quaternion.Inverse(Quaternion.LookRotation(p1.transform.position - p2.transform.position));
    }
}
