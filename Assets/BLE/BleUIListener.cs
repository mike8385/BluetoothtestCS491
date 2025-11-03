using UnityEngine;
using TMPro;
using Android.BLE;
using System; 
using Android.BLE.Commands; 
using UnityEngine.Android;


//Finds the adapter and attaches event handlers

public class BleUIListener : MonoBehaviour
{
    public TMP_Text logText; //The text in screen so we can see the data flooding in

    private BleAdapter adapter; 

     // Replace with your MPU6050 BLE service UUID
    private const string IMU_SERVICE_UUID = "6E400001-B5A3-F393-E0A9-E50E24DCCA9E"; 
    private const string IMU_CHARACTERISTIC_UUID = "6E400003-B5A3-F393-E0A9-E50E24DCCA9E"; // TX from Pico


    void Start() //Finds BLE adapter in the scene
    {
        adapter = FindObjectOfType<BleAdapter>();
        if (adapter == null)
        {
            Debug.Log("BleAdapter not found!");
            return;
        }

        // Subscribe to real C# events
        //adapter.OnMessageReceived += OnCharacteristicChanged;
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

        //Triggers scanning, and prints each device as its found
        BleManager.Instance.QueueCommand(new DiscoverDevices(OnDeviceFound, OnScanFinished));

    }

//Dont think we use this anymore
private void OnBleDataReceived(BleObject obj)
{
    if (!string.IsNullOrEmpty(obj.Base64Message))
    {
        try
        {
            //Assumed the pico sends 6 floats (ax,ay,az,gx,gy,gz) as raw bytes
            //It converts it to an array and extract each float
            byte[] bytes = Convert.FromBase64String(obj.Base64Message);
            
            // Make sure length is 24 bytes (6 floats)
            if (bytes.Length >= 24)
            {
                float ax = BitConverter.ToSingle(bytes, 0);
                float ay = BitConverter.ToSingle(bytes, 4);
                float az = BitConverter.ToSingle(bytes, 8);
                float gx = BitConverter.ToSingle(bytes, 12);
                float gy = BitConverter.ToSingle(bytes, 16);
                float gz = BitConverter.ToSingle(bytes, 20);

                string msg = $"ax={ax:F2} ay={ay:F2} az={az:F2} | gx={gx:F0} gy={gy:F0} gz={gz:F0}";
                LogDebug(msg);
                if (logText != null)
                    logText.text = msg;
            }
        }
        catch (Exception e)
        {
            Debug.Log("Failed to decode BLE data: " + e);
        }
    }
    else
    {
        if (logText != null)
            logText.text = "No data received yet";
    }
}


    private void OnDeviceFound(string address, string name) //Called whenever a device is found during scanning
    {
        LogDebug($"Found device: {name} ({address})");
        if (logText != null)
            logText.text = $"Found: {name}";
        //Connect to a device, OnDeviceConnected fires once the connection is estabilished
        BleManager.Instance.QueueCommand(new ConnectToDevice(address, OnDeviceConnected, OnBleError));
    }

    private void OnScanFinished() //Allows a loop to keep scanning, since initally it scans only for 10 seconds, now it loops
    {
        LogDebug("Scan finished. Restarting scan...");

        BleManager.Instance.QueueCommand(new DiscoverDevices(OnDeviceFound, OnScanFinished));
    }

    private void OnDeviceConnected(string address) //once the device connects, it sends a subscribe characterisitc so it can subscribe to the BlueTooth
    {
        LogDebug("Connected to " + address);
        if (logText != null) logText.text = $"Connected: {address}";

        string characteristicUuid = "6E400003-B5A3-F393-E0A9-E50E24DCCA9E"; // TX characteristic UUID
        //SSubscribes to characterisitcs
        BleManager.Instance.QueueCommand(new SubscribeToCharacteristic(address, IMU_SERVICE_UUID, IMU_CHARACTERISTIC_UUID, OnCharacteristicChanged));
    
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
        Debug.Log("BLE Error: " + error);
        if (logText != null)
            logText.text = "Error: " + error;
    }
    
    private void OnCharacteristicChanged(byte[] bytes) //ALlows us to get the data as numbers
    {
        try
        {
            if (bytes.Length >= 24)
            {
                float ax = BitConverter.ToSingle(bytes, 0);
                float ay = BitConverter.ToSingle(bytes, 4);
                float az = BitConverter.ToSingle(bytes, 8);
                float gx = BitConverter.ToSingle(bytes, 12);
                float gy = BitConverter.ToSingle(bytes, 16);
                float gz = BitConverter.ToSingle(bytes, 20);

                string msg = $"ax={ax:F2} ay={ay:F2} az={az:F2} | gx={gx:F0} gy={gy:F0} gz={gz:F0}";
                LogDebug(msg);
                if (logText != null)
                    logText.text = msg;
            }
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to decode BLE characteristic data: " + e);
        }
    }

    private void LogDebug(string msg)
    {
        LogDebug(msg);
        if (logText != null)
            logText.text += "\n" + msg;
    }



}



