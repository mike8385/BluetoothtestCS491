using UnityEngine;
using TMPro; // TextMeshPro namespace
using System;


public class BLEReceiver : MonoBehaviour
{
    [Header("Assign your TextMeshPro UI object")]
    public TMP_Text logText;  //Text Object to output data

    private AndroidJavaObject bleManager; //Talks to the Android Bluetooth system
    private string pendingMessage = null;

    private const string DEVICE_NAME = "PICO-IMU";
    private const string NUS_TX_CHAR = "6E400003-B5A3-F393-E0A9-E50E24DCCA9E";

    // Simulate BLE updates in Editor
    public bool simulateInEditor = true;

    void Start()
    {
        if (logText != null)
            logText.text = "Initializing BLEReceiver...";

#if UNITY_ANDROID && !UNITY_EDITOR
        SetupPermissionsAndScan();
#else
        Debug.Log("Running in Editor or non-Android: BLE simulation enabled");
#endif
    }

    void Update()
    {
        // Update UI from pendingMessage
        if (pendingMessage != null)
        {
            if (logText != null) logText.text = pendingMessage;
            pendingMessage = null;
        }

#if UNITY_EDITOR
        if (simulateInEditor)
        {
            // Simulate fake BLE data once per second
            if (Time.frameCount % 60 == 0)
            {
                float ax = UnityEngine.Random.Range(-1f, 1f);
                float ay = UnityEngine.Random.Range(-1f, 1f);
                float az = UnityEngine.Random.Range(-1f, 1f);
                float gx = UnityEngine.Random.Range(-180f, 180f);
                float gy = UnityEngine.Random.Range(-180f, 180f);
                float gz = UnityEngine.Random.Range(-180f, 180f);

                string message = $"[SIM] ax={ax:F2} ay={ay:F2} az={az:F2} | gx={gx:F0} gy={gy:F0} gz={gz:F0}";
                pendingMessage = message;
                Debug.Log(message);
            }
        }
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private void SetupPermissionsAndScan()
    {
        try
        {
            // Request runtime permissions
            using (var activity = new AndroidJavaClass("com.unity3d.player.UnityPlayer")
                .GetStatic<AndroidJavaObject>("currentActivity"))
            {
                string[] permissions = {
                    "android.permission.BLUETOOTH_SCAN",
                    "android.permission.BLUETOOTH_CONNECT",
                    "android.permission.ACCESS_FINE_LOCATION"
                };

                AndroidJavaClass permClass = new AndroidJavaClass("androidx.core.app.ActivityCompat");
                permClass.CallStatic("requestPermissions", activity, permissions, 0);

                // Initialize BLE manager
                bleManager = new AndroidJavaObject("com.velorexe.unityble.BleManager", activity);
                bleManager.Call("setCallback", new BleCallback(this));

                bleManager.Call("startScan");
                if (logText != null) logText.text = "Scanning for PICO-IMU...";
            }
        }
        catch (Exception e)
        {
            Debug.LogError("BLE setup failed: " + e.Message);
            if (logText != null) logText.text = "BLE setup failed. Check device/permissions.";
        }
    }
#endif

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
        Debug.Log("Connected! Subscribing...");
        bleManager.Call("subscribe", NUS_TX_CHAR);
    }

    public void OnDataReceived(byte[] data)
    {
        if (data.Length == 24) // 6 floats * 4 bytes
        {
            float[] values = new float[6];
            Buffer.BlockCopy(data, 0, values, 0, 24);

            string message = $"ax={values[0]:F2} ay={values[1]:F2} az={values[2]:F2} | gx={values[3]:F0} gy={values[4]:F0} gz={values[5]:F0}";
            Debug.Log(message);

            // Schedule UI update on main thread
            pendingMessage = message;
        }
    }

    private class BleCallback : AndroidJavaProxy
    {
        private BLEReceiver parent;
        public BleCallback(BLEReceiver parent) : base("com.velorexe.unityble.BleCallback")
        {
            this.parent = parent;
        }

        void onDeviceFound(string name, string address) => parent.OnDeviceFound(name, address);
        void onConnected() => parent.OnConnected();
        void onData(byte[] data) => parent.OnDataReceived(data);
    }
}
