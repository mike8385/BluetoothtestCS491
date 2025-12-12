using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.XR.Hands;

public class HandRecorder : MonoBehaviour
{
    public XRHandSubsystem handSubsystem;
    public bool record = false;

    private List<FrameData> frameBuffer = new List<FrameData>();
    private string filePath;
    private StreamWriter writer;
    private float nextFlushTime = 0f;

    // 26 joints per hand
    private readonly XRHandJointID[] jointOrder = new XRHandJointID[]
    {
        XRHandJointID.Wrist,
        XRHandJointID.Palm,

        XRHandJointID.ThumbMetacarpal,
        XRHandJointID.ThumbProximal,
        XRHandJointID.ThumbDistal,
        XRHandJointID.ThumbTip,

        XRHandJointID.IndexMetacarpal,
        XRHandJointID.IndexProximal,
        XRHandJointID.IndexIntermediate,
        XRHandJointID.IndexDistal,
        XRHandJointID.IndexTip,

        XRHandJointID.MiddleMetacarpal,
        XRHandJointID.MiddleProximal,
        XRHandJointID.MiddleIntermediate,
        XRHandJointID.MiddleDistal,
        XRHandJointID.MiddleTip,

        XRHandJointID.RingMetacarpal,
        XRHandJointID.RingProximal,
        XRHandJointID.RingIntermediate,
        XRHandJointID.RingDistal,
        XRHandJointID.RingTip,

        XRHandJointID.LittleMetacarpal,
        XRHandJointID.LittleProximal,
        XRHandJointID.LittleIntermediate,
        XRHandJointID.LittleDistal,
        XRHandJointID.LittleTip
    };

    [Serializable]
    public class FrameData
    {
        public float time;
        public bool leftTracked;
        public bool rightTracked;
        public List<Vector3> leftPositions = new List<Vector3>();
        public List<Quaternion> leftRotations = new List<Quaternion>();
        public List<Vector3> rightPositions = new List<Vector3>();
        public List<Quaternion> rightRotations = new List<Quaternion>();
    }

    void Start()
    {
        Debug.Log("Code is being picked up.");
        Debug.Log(Application.persistentDataPath);


        string date = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        string filePath = Path.Combine(Application.persistentDataPath, $"HandRecording_{date}.csv");

        string customPath = @"C:\Users\victo\git\bluetooth\BluetoothtestCS491\Assets";

if (Directory.Exists(customPath))
{
    filePath = Path.Combine(customPath, $"HandRecording_{date}.csv");
    Debug.Log("Assets folder found → Writing to: " + filePath);
}
else
{
    Debug.Log("Assets folder not found → Writing to persistent path: " + filePath);
}
        writer = new StreamWriter(filePath);
        WriteCSVHeader();

        Debug.Log("Streaming hand data to: " + filePath);
        nextFlushTime = Time.time + 15f;

        var subsystems = new List<XRHandSubsystem>();
        SubsystemManager.GetInstances(subsystems);

       if (subsystems.Count > 0)
    {
        handSubsystem = subsystems[0];
    Debug.Log("Hand subsystem found!");
    }
    else
{
    Debug.LogError("No XRHandSubsystem found!");
    }
    }

    void Update()
    {
        if (!record || handSubsystem == null)
            return;

        var left = handSubsystem.leftHand;
        var right = handSubsystem.rightHand;

        FrameData frame = new FrameData();
        frame.time = Time.time;
        frame.leftTracked = left.isTracked;
        frame.rightTracked = right.isTracked;

        foreach (var jointID in jointOrder)
        {
            // LEFT
            if (frame.leftTracked && left.GetJoint(jointID).TryGetPose(out Pose leftPose))
            {
                frame.leftPositions.Add(leftPose.position);
                frame.leftRotations.Add(leftPose.rotation);
            }
            else
            {
                frame.leftPositions.Add(Vector3.zero);
                frame.leftRotations.Add(Quaternion.identity);
            }

            // RIGHT
            if (frame.rightTracked && right.GetJoint(jointID).TryGetPose(out Pose rightPose))
            {
                frame.rightPositions.Add(rightPose.position);
                frame.rightRotations.Add(rightPose.rotation);
            }
            else
            {
                frame.rightPositions.Add(Vector3.zero);
                frame.rightRotations.Add(Quaternion.identity);
            }
        }

        frameBuffer.Add(frame);

        if (Time.time >= nextFlushTime)
        {
            FlushBuffer();
            nextFlushTime = Time.time + 30f;
        }
    }

    private void WriteCSVHeader()
    {
        writer.Write("timestamp, hand, joint, posX, posY, posZ, rotX, rotY, rotZ, rotW\n");
        writer.Flush();
    }

    private void FlushBuffer()
    {
        if (frameBuffer.Count == 0) return;

        foreach (var frame in frameBuffer)
        {
            for (int i = 0; i < jointOrder.Length; i++)
            {
                // LEFT hand entries
                if (frame.leftTracked)
                {
                    Vector3 p = frame.leftPositions[i];
                    Quaternion r = frame.leftRotations[i];
                    writer.WriteLine($"{frame.time}, LEFT, {jointOrder[i]}, {p.x}, {p.y}, {p.z}, {r.x}, {r.y}, {r.z}, {r.w}");
                }

                // RIGHT hand entries
                if (frame.rightTracked)
                {
                    Vector3 p = frame.rightPositions[i];
                    Quaternion r = frame.rightRotations[i];
                    writer.WriteLine($"{frame.time}, RIGHT, {jointOrder[i]}, {p.x}, {p.y}, {p.z}, {r.x}, {r.y}, {r.z}, {r.w}");
                }
            }
        }

        writer.Flush();
        frameBuffer.Clear();
        Debug.Log("CSV Buffer Written.");
    }

    public void StopRecording()
    {
        record = false;
        FlushBuffer();
        CloseFile();
    }

    private void OnApplicationQuit()
    {
        FlushBuffer();
        CloseFile();
    }

    private void CloseFile()
    {
        if (writer != null)
        {
            writer.Close();
            writer = null;
            Debug.Log("CSV closed.");
        }
    }
}
