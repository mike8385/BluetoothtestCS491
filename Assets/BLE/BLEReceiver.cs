using UnityEngine;
using UnityEngine.UI;  // or TMPro for TextMeshPro
using System;
using TMPro; // TextMeshPro namespace
using System.Text;
using System.Runtime.InteropServices;

public class BLEReceiver : MonoBehaviour
{
    public TMP_Text logText; // assign in Inspector
    // If using TextMeshPro: public TMP_Text logText;
    private AndroidJavaObject bleManager;

    private const string DEVICE_NAME = "PICO-IMU";
    private const string NUS_TX_CHAR = "6E400003-B5A3-F393-E0A9-E50E24DCCA9E";

    void Start()
    {
        Debug.Log("Starting BLE scan...");
        AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

        bleManager = new AndroidJavaObject("com.velorexe.unityble.BleManager", currentActivity);
        bleManager.Call("setCallback", new BleCallback(this));

        bleManager.Call("startScan");
    }

    public void OnDeviceFound(string name, string address)
    {
        if (name == DEVICE_NAME)
        {
            Debug.Log($"Found {name}, connecting...");
            bleManager.Call("connect", address);
        }
    }

    public void OnConnected()
    {
        Debug.Log("Connected! Subscribing to notifications...");
        bleManager.Call("subscribe", NUS_TX_CHAR);
    }

    public void OnDataReceived(byte[] data)
    {
        if (data.Length == 24) // 6 floats * 4 bytes each
        {
            float[] values = new float[6];
            Buffer.BlockCopy(data, 0, values, 0, 24);

            string message = $"ax={values[0]:F2} ay={values[1]:F2} az={values[2]:F2} | gx={values[3]:F0} gy={values[4]:F0} gz={values[5]:F0}";

            // Log to console
            Debug.Log(message);

            // Update on-screen UI
            if (logText != null)
                logText.text = message;
        }
    }


    private class BleCallback : AndroidJavaProxy
    {
        private BLEReceiver parent;
        public BleCallback(BLEReceiver parent) : base("com.velorexe.unityble.BleCallback") { this.parent = parent; }

        void onDeviceFound(string name, string address) => parent.OnDeviceFound(name, address);
        void onConnected() => parent.OnConnected();
        void onData(byte[] data) => parent.OnDataReceived(data);
    }
}
