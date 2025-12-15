
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.XR.Hands;
public class HandRecorder : MonoBehaviour
{
    // Reference to the XR hand tracking subsystem
    public XRHandSubsystem handSubsystem;
    // Toggle to start/stop recording (can be controlled from Inspector or code)
    public bool record = false;
    // Stores recorded frames before writing them to disk
    private List<FrameData> frameBuffer = new List<FrameData>();
    // Path where the CSV file will be written
    private string filePath;
    // StreamWriter handles writing text to the CSV file
    private StreamWriter writer;
    // Time at which the next buffer flush should occur
    private float nextFlushTime = 0f;
    // Defines the exact order of joints (26 joints per hand)
    // This ensures consistent indexing when writing to CSV
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
    // Allows this class to be serialized (important for Unity + data storage)
    [Serializable]
    public class FrameData
    {
        // Timestamp of the frame (seconds since start)
        public float time;

        // Whether each hand is currently tracked
        public bool leftTracked;
        public bool rightTracked;

        // Left hand joint positions and rotations
        public List<Vector3> leftPositions = new List<Vector3>();
        public List<Quaternion> leftRotations = new List<Quaternion>();

        // Right hand joint positions and rotations
        public List<Vector3> rightPositions = new List<Vector3>();
        public List<Quaternion> rightRotations = new List<Quaternion>();
    }

    void Start()
    {
        // Confirms script is running
        Debug.Log("Code is being picked up.");

        // Logs Unity's persistent storage path
        Debug.Log(Application.persistentDataPath);

        // Creates a timestamped filename
        string date = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");

        // Default save path (persistent data)
        string filePath = Path.Combine(
            Application.persistentDataPath,
            $"HandRecording_{date}.csv"
        );
        // Custom path inside the Unity Assets folder
        string customPath = @"C:\Users\victo\git\bluetooth\BluetoothtestCS491\Assets";

        // If the Assets folder exists, write the CSV there instead
        if (Directory.Exists(customPath))
        {
            filePath = Path.Combine(customPath, $"HandRecording_{date}.csv");
            Debug.Log("Assets folder found → Writing to: " + filePath);
        }
        else
        {
            Debug.Log("Assets folder not found → Writing to persistent path: " + filePath);
        }
        // Opens the CSV file for writing
        writer = new StreamWriter(filePath);
        // Writes the column headers to the CSV
        WriteCSVHeader();
        // Logs the active output path
        Debug.Log("Streaming hand data to: " + filePath);
        // Sets the first flush time (15 seconds after start)
        nextFlushTime = Time.time + 15f;
        // Get all XRHandSubsystem instances
        var subsystems = new List<XRHandSubsystem>();
        SubsystemManager.GetInstances(subsystems);
        // Use the first available hand subsystem
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
        // Stop execution if recording is disabled or hand tracking is unavailable
        if (!record || handSubsystem == null)
            return;

        // Get references to left and right hands
        var left = handSubsystem.leftHand;
        var right = handSubsystem.rightHand;
        // Create a new frame to store this update's data
        FrameData frame = new FrameData();
        // Store timestamp and tracking state
        frame.time = Time.time;
        frame.leftTracked = left.isTracked;
        frame.rightTracked = right.isTracked;
        // Loop through all joints in the predefined order
        foreach (var jointID in jointOrder)
        {
            // ----- LEFT HAND -----
            if (frame.leftTracked &&
                left.GetJoint(jointID).TryGetPose(out Pose leftPose))
            {
                // Save real joint data
                frame.leftPositions.Add(leftPose.position);
                frame.leftRotations.Add(leftPose.rotation);
            }
            else
            {
                // Save placeholders if joint is not tracked
                frame.leftPositions.Add(Vector3.zero);
                frame.leftRotations.Add(Quaternion.identity);
            }

            // ----- RIGHT HAND -----
            if (frame.rightTracked &&
                right.GetJoint(jointID).TryGetPose(out Pose rightPose))
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
        // Add this frame to the buffer
        frameBuffer.Add(frame);
        // Periodically write buffered data to disk
        if (Time.time >= nextFlushTime)
        {
            FlushBuffer();
            nextFlushTime = Time.time + 30f;
        }
    }
    // Writes the CSV column headers
    private void WriteCSVHeader()
    {
        writer.Write(
            "timestamp, hand, joint, posX, posY, posZ, rotX, rotY, rotZ, rotW\n"
        );
        writer.Flush();
    }
    // Writes buffered frame data to the CSV file
    private void FlushBuffer()
    {
        // Do nothing if there is no data
        if (frameBuffer.Count == 0) return;
        foreach (var frame in frameBuffer)
        {
            for (int i = 0; i < jointOrder.Length; i++)
            {
                // LEFT HAND ROW
                if (frame.leftTracked)
                {
                    Vector3 p = frame.leftPositions[i];
                    Quaternion r = frame.leftRotations[i];

                    writer.WriteLine(
                        $"{frame.time}, LEFT, {jointOrder[i]}, " +
                        $"{p.x}, {p.y}, {p.z}, {r.x}, {r.y}, {r.z}, {r.w}"
                    );
                }
                // RIGHT HAND ROW
                if (frame.rightTracked)
                {
                    Vector3 p = frame.rightPositions[i];
                    Quaternion r = frame.rightRotations[i];

                    writer.WriteLine(
                        $"{frame.time}, RIGHT, {jointOrder[i]}, " +
                        $"{p.x}, {p.y}, {p.z}, {r.x}, {r.y}, {r.z}, {r.w}"
                    );
                }
            }
        }

        // Forces data to be written to disk
        writer.Flush();
        // Clear memory buffer after writing
        frameBuffer.Clear();
        Debug.Log("CSV Buffer Written.");
    }
    // Public method to stop recording manually
    public void StopRecording()
    {
        record = false;
        FlushBuffer();
        CloseFile();
    }
    // Ensures data is saved when the application exits by flushing all the remaining information and then closing the file
    private void OnApplicationQuit()
    {
        FlushBuffer();
        CloseFile();
    }
    // Closes the CSV file safely
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
