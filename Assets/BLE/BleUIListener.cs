using UnityEngine;
using TMPro;
using Android.BLE;
using System;
using Android.BLE.Commands;
using UnityEngine.Android;
using System.IO;
using System.Collections.Generic;

//NOTE: You cannot use OVR use OXR, OVR is an older system and both were active and they caused issues
//ALSO this is the only file I made, the others were apart of the Github repo I found by Velorexe (https://github.com/Velorexe/Unity-Android-Bluetooth-Low-Energy)
/********************
    NOTE: HOW BLUETOOTH WORKS (In basic dumbed down terms): 
    (I Had to figure it out on my own so if this doesnt make sense, I apologize Im still not 100% sure)   
    
    1.) SCAN: You scan and find nearby devices
        -BLE devices send broadcast advertisements that contain device name, MAC address, UUIDs (optional), and signal strength
        -Scanning isnt connecting, just a way of saying "I see you"
    2.) CONNECT: Establish a BLE session
        -Before connecting, you ONLY hear broadcast advertisements. Only AFTER you connect you get access to the internal "BLE database"
        -Connecting establishes GATT session
    3.) DISCOVER SERVICES: Read the "Database structure", like a folder on the device
        -When you connect to a BLE device you are connecting to a GATT server, which is basically a tiny database inside the device
        -These UUIDs tell BLE which service/characteristic (“pipe”) you want to access.
        -They DO NOT explain the data format or how to use the pipe — that is defined by the characteristic’s PROPERTIES (READ / WRITE / NOTIFY).
        -Its designed this way so that it can expose data in a standard way, the services have a UUID and so do the characteristics
    4.) SUBSCRIBE: Listen for notifications
        -Tells the PICO, whenever this value changes, send me notifications
        -Cannot recieve IMU data unless you subscribe. Scanning wont give you data nor connecting, only notifications
    5.) RECIEVE DATA: Your callback fires with IMU bytes
        -Callbacks are functions that BLE systems will call later when something happens, you dont call them yourself
        -They get called when events happen
********************/

//Finds the adapter and attaches event handlers
public class BleUIListener : MonoBehaviour
{
    public TMP_Text logText; //The text in screen so we can see the data flooding in

    private BleAdapter adapter;
    private bool isConnecting = false;
    private bool _connected = false;

    public Transform jackhammer; //The object you drag into the inspector

    private Vector3 currentRotation = Vector3.zero;

    // Replace with your MPU6050 BLE service UUID
    private const string NUS_SERVICE = "6E400001-B5A3-F393-E0A9-E50E24DCCA9E"; //Identifies the entire Nordic UART service (folder)
    private const string NUS_RX_UUID = "6E400002-B5A3-F393-E0A9-E50E24DCCA9E"; // Write (Quest -> Pico)
    private const string NUS_TX_UUID = "6E400003-B5A3-F393-E0A9-E50E24DCCA9E"; // Notify (Pico -> Quest)

    //The MAC address of PICO-IMU (Might change depending on wifi, so far it hasnt changed)
    private const string TARGET_MAC = "28:CD:C1:14:B8:3C";

    // ==============================
    // CSV LOGGING VARIABLES
    // ==============================
    // StreamWriter writes text to a file on disk
    private StreamWriter imuWriter;

    // Buffer stores IMU frames temporarily before writing to disk
    // This avoids writing to disk every BLE packet (performance + safety)
    private List<IMUFrame> imuBuffer = new List<IMUFrame>();

    // Controls how often the buffer is flushed to disk
    private float nextFlushTime = 0f;

    //Initially the data was sent as 1 package of floats in the python code, around 26 bytes, but the script only allowed for 20 bytes
    //So instead, we send it as integers, in 2 packages, the accel and gyro. Then we convert said numbers to floats in C#
    private float[] _acc3, _gyro3;

    //Helper class: ONE IMU packet = ONE CSV row

    public bool record = false;
    
    [Serializable]
    private class IMUFrame
    {
        public float time;
        public float ax, ay, az;
        public float gx, gy, gz;
    }

    //Helps whitelist mac address of pico
    private static string NormalizeMac(string mac)
    {
        if (string.IsNullOrEmpty(mac)) return mac;
        mac = mac.Replace(":", "").ToUpperInvariant();
        if (mac.Length == 12)
            mac = string.Join(":", System.Text.RegularExpressions.Regex.Split(mac, @"(?<=\G..)(?!$)"));
        return mac;
    }

    //IEnumerator(Coroutine) start to try to avoid crashing when you first load it after installing the APK
    //I am pretty sure the issue is the the Location permission issues. Havent figured out how to stop the crashing
    private System.Collections.IEnumerator Start()
    {
        if (!Permission.HasUserAuthorizedPermission("android.permission.BLUETOOTH_SCAN"))
            Permission.RequestUserPermission("android.permission.BLUETOOTH_SCAN");
        if (!Permission.HasUserAuthorizedPermission("android.permission.BLUETOOTH_CONNECT"))
            Permission.RequestUserPermission("android.permission.BLUETOOTH_CONNECT");
        if (!Permission.HasUserAuthorizedPermission("android.permission.ACCESS_FINE_LOCATION"))
            Permission.RequestUserPermission("android.permission.ACCESS_FINE_LOCATION");

        float t = 0f;
        while (t < 3f &&
            (!Permission.HasUserAuthorizedPermission("android.permission.BLUETOOTH_SCAN") ||
             !Permission.HasUserAuthorizedPermission("android.permission.BLUETOOTH_CONNECT")))
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        adapter = FindObjectOfType<BleAdapter>();
        if (!adapter) { Debug.LogError("BleAdapter not found!"); yield break; }

        adapter.OnErrorReceived += OnBleError;

        if (logText) logText.text = "Ready to scan for BLE devices...";

        BleManager.Instance.Initialize();
        yield return new WaitForSeconds(0.25f);

        // ==============================
        // CREATE CSV FILE FOR IMU DATA
        // ==============================
        // Each BLE notification will become ONE ROW in this file
        string date = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        string path = Path.Combine(
            Application.persistentDataPath,
            $"IMU_Recording_{date}.csv"
        );

        imuWriter = new StreamWriter(path);

        // CSV HEADER: one timestamp, then all IMU values
        imuWriter.WriteLine("timestamp,ax,ay,az,gx,gy,gz");
        imuWriter.Flush();

        Debug.Log("IMU CSV created at: " + path);

        // Flush CSV every 15 seconds
        nextFlushTime = Time.time + 15f;

        BleManager.Instance.QueueCommand(
            new DiscoverDevices(OnDeviceFound, OnScanFinished)
        );
    }

    //Filters PICO by name or exact MAC address. If it matches and we arent connected, it queues it to ConnectToDevice
    private void OnDeviceFound(string address, string name)
    {
        bool looksLikePico =
            (!string.IsNullOrEmpty(name) && name.IndexOf("PICO", StringComparison.OrdinalIgnoreCase) >= 0)
            || string.Equals(NormalizeMac(address), NormalizeMac(TARGET_MAC), StringComparison.OrdinalIgnoreCase);

        if (!looksLikePico || isConnecting) return;
        isConnecting = true;

        BleManager.Instance.QueueCommand(
            new ConnectToDevice(address, OnDeviceConnected, OnBleError)
        );
    }

    private void OnScanFinished()
    {
        if (_connected) return;
        BleManager.Instance.QueueCommand(
            new DiscoverDevices(OnDeviceFound, OnScanFinished)
        );
    }

    private void OnDeviceConnected(string address)
    {
        isConnecting = false;
        _connected = true;
        StartCoroutine(SubscribeAfter(address));
    }

    private System.Collections.IEnumerator SubscribeAfter(string address)
    {
        yield return new WaitForSeconds(0.3f);

        BleManager.Instance.QueueCommand(
            new SubscribeToCharacteristic(
                address,
                NUS_SERVICE.ToLowerInvariant(),
                NUS_TX_UUID.ToLowerInvariant(),
                OnCharacteristicChanged,
                true
            )
        );
    }

    //Notification Handler — THIS IS WHERE IMU DATA ARRIVES
    private void OnCharacteristicChanged(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0)
            return;

        float ax = 0, ay = 0, az = 0;
        float gx = 0, gy = 0, gz = 0;

        // 24 bytes → 6 floats
        if (bytes.Length == 24)
        {
            ax = BitConverter.ToSingle(bytes, 0);
            ay = BitConverter.ToSingle(bytes, 4);
            az = BitConverter.ToSingle(bytes, 8);
            gx = BitConverter.ToSingle(bytes, 12);
            gy = BitConverter.ToSingle(bytes, 16);
            gz = BitConverter.ToSingle(bytes, 20);
        }
        // 12 bytes → packed int16
        else if (bytes.Length == 12)
        {
            ax = BitConverter.ToInt16(bytes, 0) / 1000f;
            ay = BitConverter.ToInt16(bytes, 2) / 1000f;
            az = BitConverter.ToInt16(bytes, 4) / 1000f;
            gx = BitConverter.ToInt16(bytes, 6) / 100f;
            gy = BitConverter.ToInt16(bytes, 8) / 100f;
            gz = BitConverter.ToInt16(bytes, 10) / 100f;
        }
        else return;

        // Rotate object using gyro
        UpdateRotation(gx, gy, gz);

        if (logText)
            logText.text = $"ax={ax:F2} ay={ay:F2} az={az:F2} | gx={gx:F1} gy={gy:F1} gz={gz:F1}";

        // ==============================
        // STORE THIS PACKET FOR CSV
        // ==============================
        // ONE BLE notification = ONE CSV row
        imuBuffer.Add(new IMUFrame
        {
            time = Time.time,
            ax = ax,
            ay = ay,
            az = az,
            gx = gx,
            gy = gy,
            gz = gz
        });

        if (Time.time >= nextFlushTime)
        {
            FlushIMUBuffer();
            nextFlushTime = Time.time + 15f;
        }
    }

    //Writes buffered IMU frames to disk
    private void FlushIMUBuffer()
    {
        if (imuBuffer.Count == 0 || imuWriter == null)
            return;

        foreach (var f in imuBuffer)
        {
            imuWriter.WriteLine(
                $"{f.time},{f.ax},{f.ay},{f.az},{f.gx},{f.gy},{f.gz}"
            );
        }

        imuWriter.Flush();
        imuBuffer.Clear();
    }

    //This allows the object to rotate given the IMU data
    private void UpdateRotation(float gx, float gy, float gz)
    {
        float dt = Time.deltaTime;

        currentRotation.x += gx * dt;
        currentRotation.y += gy * dt;
        currentRotation.z += gz * dt;

        if (jackhammer != null)
            jackhammer.localRotation = Quaternion.Euler(currentRotation);
    }

    private void OnApplicationQuit()
    {
        FlushIMUBuffer();

        if (imuWriter != null)
        {
            imuWriter.Close();
            imuWriter = null;
        }
    }

    //Logs the error, restart states, and restarts scanning
    private void OnBleError(string error)
    {
        isConnecting = false;
        _connected = false;
        Debug.LogError("BLE Error: " + error);
    }
}