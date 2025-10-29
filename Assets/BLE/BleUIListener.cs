using UnityEngine;
using TMPro;
using Android.BLE;
using System; 
using Android.BLE.Commands; 
using UnityEngine.Android;


public class BleUIListener : MonoBehaviour
{
    public TMP_Text logText;
    private BleAdapter adapter;

    void Start()
    {
        adapter = FindObjectOfType<BleAdapter>();
        if (adapter == null)
        {
            Debug.LogError("BleAdapter not found!");
            return;
        }

        // Subscribe to real C# events
        adapter.OnMessageReceived += OnBleDataReceived;
        adapter.OnErrorReceived += OnBleError;

        if (logText != null)
            logText.text = "Ready to scan for BLE devices...";
        
    if (!Permission.HasUserAuthorizedPermission("android.permission.BLUETOOTH_SCAN"))
        Permission.RequestUserPermission("android.permission.BLUETOOTH_SCAN");

    if (!Permission.HasUserAuthorizedPermission("android.permission.BLUETOOTH_CONNECT"))
        Permission.RequestUserPermission("android.permission.BLUETOOTH_CONNECT");

    if (!Permission.HasUserAuthorizedPermission("android.permission.ACCESS_FINE_LOCATION"))
        Permission.RequestUserPermission("android.permission.ACCESS_FINE_LOCATION");

    // Now safe to initialize BLE
    BleManager.Instance.Initialize();
    BleManager.Instance.QueueCommand(new DiscoverDevices(OnDeviceFound, OnScanFinished));


    }

    // private void OnBleDataReceived(BleObject obj)
    // {
    //     // Example: assuming your BLE JSON contains "ax","ay","az","gx","gy","gz"
    //     var data = JsonUtility.FromJson<MyIMUData>(JsonUtility.ToJson(obj));
    //     string msg = $"ax={data.ax:F2} ay={data.ay:F2} az={data.az:F2} | gx={data.gx:F0} gy={data.gy:F0} gz={data.gz:F0}";
        
    //     Debug.Log(msg);
    //     if (logText != null)
    //         logText.text = msg;
    // }

    private void OnBleDataReceived(BleObject obj)
    {
    // Convert the BLE object to JSON text
    string json = JsonUtility.ToJson(obj, true);

    // Log to Unity console for debugging
    Debug.Log("Received BLE JSON:\n" + json);

    // Show the JSON directly in your on-screen text box
    if (logText != null)
    {
        logText.text = json;
    }
    }

    // helper class for parsing
    [Serializable]
    public class MyIMUData
    {
        public float ax, ay, az;
        public float gx, gy, gz;
    }

    private void OnBleError(string error)
    {
        Debug.LogError("BLE Error: " + error);
        if (logText != null)
            logText.text = "Error: " + error;
    }
    // void Update()
    // {
    //     #if UNITY_EDITOR

    //         // Simulate fake BLE data once per second
    //         if (Time.frameCount % 60 == 0)
    //         {
    //             float ax = UnityEngine.Random.Range(-1f, 1f);
    //             float ay = UnityEngine.Random.Range(-1f, 1f);
    //             float az = UnityEngine.Random.Range(-1f, 1f);
    //             float gx = UnityEngine.Random.Range(-180f, 180f);
    //             float gy = UnityEngine.Random.Range(-180f, 180f);
    //             float gz = UnityEngine.Random.Range(-180f, 180f);

    //             string message = $"[SIM] ax={ax:F2} ay={ay:F2} az={az:F2} | gx={gx:F0} gy={gy:F0} gz={gz:F0}";
    //             if (logText!=null)
    //             {
    //                 logText.text = message;
    //             }
    //             Debug.Log(message);

    //         }
    //     #endif
    // }

    private void OnDeviceFound(string address, string name)
    {
        Debug.Log($"Found device: {name} ({address})");
        if (logText != null)
            logText.text = $"Found: {name}";
    }

    private void OnScanFinished()
    {
        Debug.Log("Scan finished. Restarting scan...");
        BleManager.Instance.QueueCommand(new DiscoverDevices(OnDeviceFound, OnScanFinished));
    }
}



