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
    BleManager.Instance.QueueCommand(new DiscoverDevices());//OnDeviceFound, OnScanFinished));


    }

private void OnBleDataReceived(BleObject obj)
{
    // Example: assuming your BLE JSON contains "ax","ay","az","gx","gy","gz"
    var data = JsonUtility.FromJson<MyIMUData>(JsonUtility.ToJson(obj));
    string msg = $"ax={data.ax:F2} ay={data.ay:F2} az={data.az:F2} | gx={data.gx:F0} gy={data.gy:F0} gz={data.gz:F0}";
    
    Debug.Log(msg);
    if (logText != null)
        logText.text = msg;
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
}
