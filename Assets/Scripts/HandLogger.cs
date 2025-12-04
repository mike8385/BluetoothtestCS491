using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Management;
using TMPro;
using System.Text;

public class HandLogger : MonoBehaviour
{
    [Header("UI")]
    public TextMeshProUGUI leftHandText;   // assign LeftHandText here
    public TextMeshProUGUI rightHandText;  // assign RightHandText here

    private XRHandSubsystem handSubsystem;

    // All valid XR hand joints (26 per hand)
    private static readonly XRHandJointID[] allJointIDs = new XRHandJointID[]
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

    void Start()
    {
        var settings = XRGeneralSettings.Instance;
        if (settings != null && settings.Manager != null)
        {
            handSubsystem = settings.Manager.activeLoader
                .GetLoadedSubsystem<XRHandSubsystem>();
        }

        if (handSubsystem == null)
            Debug.LogError("HandLogger: XRHandSubsystem not found. " +
                           "Is XR Hands enabled in OpenXR?");
    }

    void Update()
    {
        if (handSubsystem == null || !handSubsystem.running)
            return;

        // LEFT HAND
        if (leftHandText != null)
        {
            StringBuilder leftSb = new StringBuilder(512);
            LogHand(leftSb, handSubsystem.leftHand, "LEFT");
            leftHandText.text = leftSb.ToString();
        }

        // RIGHT HAND
        if (rightHandText != null)
        {
            StringBuilder rightSb = new StringBuilder(512);
            LogHand(rightSb, handSubsystem.rightHand, "RIGHT");
            rightHandText.text = rightSb.ToString();
        }
    }

    private void LogHand(StringBuilder sb, XRHand hand, string label)
    {
        if (!hand.isTracked)
        {
            sb.AppendLine($"{label} hand: NOT TRACKED");
            return;
        }

        sb.AppendLine($"{label} hand:");

        foreach (var jointId in allJointIDs)
        {
            XRHandJoint joint = hand.GetJoint(jointId);

            if (joint.TryGetPose(out Pose pose))
            {
                Vector3 p = pose.position;
                Vector3 r = pose.rotation.eulerAngles;

                sb.AppendLine(
                    $"  {jointId,-20} Pos({p.x:F3}, {p.y:F3}, {p.z:F3})  " +
                    $"Rot({r.x:F1}, {r.y:F1}, {r.z:F1})");
            }
            else
            {
                sb.AppendLine($"  {jointId,-20} NO POSE");
            }
        }
    }
}
