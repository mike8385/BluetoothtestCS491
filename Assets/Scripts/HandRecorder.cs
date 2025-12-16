using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.XR.Hands;

public class HandRecorder : MonoBehaviour
{
    // XR Hand subsystem used to access hand tracking data
    public XRHandSubsystem handSubsystem;

    // Toggle recording on/off
    public bool record = false;

    // Buffer that temporarily stores frames before writing to disk
    private List<FrameData> frameBuffer = new List<FrameData>();

    // Separate writers for left and right hands
    private StreamWriter leftWriter;
    private StreamWriter rightWriter;

    // Controls how often buffered data is written to disk
    private float nextFlushTime = 0f;

    // Order of joints – must stay consistent for CSV column alignment
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

    // Stores all joint data for one frame in time
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
        // Find an active XRHandSubsystem
        var subsystems = new List<XRHandSubsystem>();
        SubsystemManager.GetInstances(subsystems);

        if (subsystems.Count > 0)
        {
            handSubsystem = subsystems[0];
            Debug.Log("XRHandSubsystem found.");
        }
        else
        {
            Debug.LogError("No XRHandSubsystem found!");
            return;
        }

        // Create unique filenames using timestamp
        string date = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");

        string leftPath = Path.Combine(
            Application.persistentDataPath,
            $"HandRecording_LEFT_{date}.csv"
        );

        string rightPath = Path.Combine(
            Application.persistentDataPath,
            $"HandRecording_RIGHT_{date}.csv"
        );

        // Open file streams
        leftWriter = new StreamWriter(leftPath);
        rightWriter = new StreamWriter(rightPath);

        // Write CSV headers (column names)
        WriteCSVHeader(leftWriter);
        WriteCSVHeader(rightWriter);

        Debug.Log("Left hand CSV: " + leftPath);
        Debug.Log("Right hand CSV: " + rightPath);

        // Schedule first buffer flush
        nextFlushTime = Time.time + 15f;
    }

    void Update()
    {
        // Do nothing if not recording or no hand subsystem
        if (!record || handSubsystem == null)
            return;

        var left = handSubsystem.leftHand;
        var right = handSubsystem.rightHand;

        // Create a new frame entry
        FrameData frame = new FrameData
        {
            time = Time.time,
            leftTracked = left.isTracked,
            rightTracked = right.isTracked
        };

        // Collect joint data in a fixed order
        foreach (var jointID in jointOrder)
        {
            // LEFT HAND
            if (frame.leftTracked && left.GetJoint(jointID).TryGetPose(out Pose leftPose))
            {
                frame.leftPositions.Add(leftPose.position);
                frame.leftRotations.Add(leftPose.rotation);
            }
            else
            {
                // Fill with zeros if not tracked to keep column alignment
                frame.leftPositions.Add(Vector3.zero);
                frame.leftRotations.Add(Quaternion.identity);
            }

            // RIGHT HAND
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

        // Store frame in buffer (not written yet)
        frameBuffer.Add(frame);

        // Periodically write buffered data to disk
        if (Time.time >= nextFlushTime)
        {
            FlushBuffer();
            nextFlushTime = Time.time + 30f;
        }
    }

    // Writes the header row (column names) for wide-format CSV
    private void WriteCSVHeader(StreamWriter writer)
    {
        writer.Write("timestamp");

        // Each joint contributes 7 columns (position + rotation)
        foreach (var joint in jointOrder)
        {
            writer.Write($",{joint}_posX,{joint}_posY,{joint}_posZ");
            writer.Write($",{joint}_rotX,{joint}_rotY,{joint}_rotZ,{joint}_rotW");
        }

        writer.WriteLine();
        writer.Flush();
    }

    // Converts buffered frames into CSV rows
    private void FlushBuffer()
    {
        if (frameBuffer.Count == 0)
            return;

        foreach (var frame in frameBuffer)
        {
            // LEFT HAND — one row per frame
            if (frame.leftTracked)
            {
                leftWriter.Write(frame.time);

                for (int i = 0; i < jointOrder.Length; i++)
                {
                    Vector3 p = frame.leftPositions[i];
                    Quaternion r = frame.leftRotations[i];

                    leftWriter.Write($",{p.x},{p.y},{p.z},{r.x},{r.y},{r.z},{r.w}");
                }

                leftWriter.WriteLine();
            }

            // RIGHT HAND — one row per frame
            if (frame.rightTracked)
            {
                rightWriter.Write(frame.time);

                for (int i = 0; i < jointOrder.Length; i++)
                {
                    Vector3 p = frame.rightPositions[i];
                    Quaternion r = frame.rightRotations[i];

                    rightWriter.Write($",{p.x},{p.y},{p.z},{r.x},{r.y},{r.z},{r.w}");
                }

                rightWriter.WriteLine();
            }
        }

        // Force data to be written to disk
        leftWriter.Flush();
        rightWriter.Flush();

        // Clear buffer after writing
        frameBuffer.Clear();

        Debug.Log("Hand data written to CSV.");
    }

    // Stop recording and save remaining data
    public void StopRecording()
    {
        record = false;
        FlushBuffer();
        CloseFiles();
    }

    private void OnApplicationQuit()
    {
        FlushBuffer();
        CloseFiles();
    }

    // Close file streams safely
    private void CloseFiles()
    {
        if (leftWriter != null)
        {
            leftWriter.Close();
            leftWriter = null;
        }

        if (rightWriter != null)
        {
            rightWriter.Close();
            rightWriter = null;
        }

        Debug.Log("CSV files closed.");
    }
}