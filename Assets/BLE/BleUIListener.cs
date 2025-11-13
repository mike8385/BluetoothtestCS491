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
    private bool isConnecting = false;
    private bool _connected = false;



     // Replace with your MPU6050 BLE service UUID
    //private const string IMU_SERVICE_UUID = "6E400001-B5A3-F393-E0A9-E50E24DCCA9E"; 
   // private const string IMU_CHARACTERISTIC_UUID = "6E400003-B5A3-F393-E0A9-E50E24DCCA9E"; // TX from Pico
    // Replace your two IMU consts with these three:
    private const string NUS_SERVICE = "6E400001-B5A3-F393-E0A9-E50E24DCCA9E";
    private const string NUS_RX_UUID = "6E400002-B5A3-F393-E0A9-E50E24DCCA9E"; // Write (phone -> Pico)
    private const string NUS_TX_UUID = "6E400003-B5A3-F393-E0A9-E50E24DCCA9E"; // Notify (Pico -> phone)

    private const string TARGET_MAC = "28:CD:C1:14:B8:3C";
    
    private float[] _acc3, _gyro3;

    

    //Helps whitelist mac address of pico
    private static string NormalizeMac(string mac) {
        if (string.IsNullOrEmpty(mac)) return mac;
        mac = mac.Replace(":", "").ToUpperInvariant();
        if (mac.Length == 12)
            mac = string.Join(":", System.Text.RegularExpressions.Regex.Split(mac, @"(?<=\G..)(?!$)"));
        return mac;
    }



    private System.Collections.IEnumerator Start()
    {

        if (!Permission.HasUserAuthorizedPermission("android.permission.BLUETOOTH_SCAN"))
            Permission.RequestUserPermission("android.permission.BLUETOOTH_SCAN");
        if (!Permission.HasUserAuthorizedPermission("android.permission.BLUETOOTH_CONNECT"))
            Permission.RequestUserPermission("android.permission.BLUETOOTH_CONNECT");
        if (!Permission.HasUserAuthorizedPermission("android.permission.ACCESS_FINE_LOCATION"))
            Permission.RequestUserPermission("android.permission.ACCESS_FINE_LOCATION");

        // Wait up to ~3s for user to respond to prompts
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

        // small delay to let adapter settle
        yield return new WaitForSeconds(0.25f);

        BleManager.Instance.QueueCommand(new DiscoverDevices(OnDeviceFound, OnScanFinished));
    }


// //Dont think we use this anymore
// private void OnBleDataReceived(BleObject obj)
// {
//     if (!string.IsNullOrEmpty(obj.Base64Message))
//     {
//         try
//         {
//             //Assumed the pico sends 6 floats (ax,ay,az,gx,gy,gz) as raw bytes
//             //It converts it to an array and extract each float
//             byte[] bytes = Convert.FromBase64String(obj.Base64Message);
            
//             // Make sure length is 24 bytes (6 floats)
//             if (bytes.Length >= 24)
//             {
//                 float ax = BitConverter.ToSingle(bytes, 0);
//                 float ay = BitConverter.ToSingle(bytes, 4);
//                 float az = BitConverter.ToSingle(bytes, 8);
//                 float gx = BitConverter.ToSingle(bytes, 12);
//                 float gy = BitConverter.ToSingle(bytes, 16);
//                 float gz = BitConverter.ToSingle(bytes, 20);

//                 string msg = $"ax={ax:F2} ay={ay:F2} az={az:F2} | gx={gx:F0} gy={gy:F0} gz={gz:F0}";
//                 Debug.Log(msg);
//                 if (logText != null)
//                     logText.text = msg;
//             }
//         }
//         catch (Exception e)
//         {
//             Debug.LogError("Failed to decode BLE data: " + e);
//         }
//     }
//     else
//     {
//         if (logText != null)
//             logText.text = "No data received yet";
//     }
// }

//Filters PICO by name or exact MAC address. If it matches and we arent connected, it queues it to ConnectToDevice
private void OnDeviceFound(string address, string name)
{
    bool looksLikePico =
        (!string.IsNullOrEmpty(name) && name.IndexOf("PICO", StringComparison.OrdinalIgnoreCase) >= 0)
        || string.Equals(NormalizeMac(address), NormalizeMac(TARGET_MAC), StringComparison.OrdinalIgnoreCase);

    if (!looksLikePico || isConnecting) return;
    isConnecting = true;

    Debug.Log($"Found device: {name} ({address})");
    if (logText) logText.text = $"Found: {name}";
    BleManager.Instance.QueueCommand(new ConnectToDevice(address, OnDeviceConnected, OnBleError));
}



//If its not connected, It restarts scaning (Keeps searching until found)
private void OnScanFinished()
{
    if (_connected) return;                 // don't restart scan if already connected
    Debug.Log("Scan finished. Restarting scan…");
    BleManager.Instance.QueueCommand(new DiscoverDevices(OnDeviceFound, OnScanFinished));
}

//Marks it as connected then starts a coroutine to subscribe a short moment later
private void OnDeviceConnected(string address)
{
    isConnecting = false;
    _connected = true;
    if (logText) logText.text = $"Connected: {address}";
    StartCoroutine(SubscribeAfter(address));
}


//Waits 3 seconds then calls SubscribeToCharacterisitc on the TX characteristic
//customGatt: true is required for 128-bit UUIDs like NUS
//Sends 'hi' to nudge stack
private System.Collections.IEnumerator SubscribeAfter(string address)
{
    // small grace after connect
    yield return new WaitForSeconds(0.3f);

    Action doSub = () => BleManager.Instance.QueueCommand(
        new SubscribeToCharacteristic(
            address,
            NUS_SERVICE.ToLowerInvariant(),   // safest: lowercase
            NUS_TX_UUID.ToLowerInvariant(),
            OnCharacteristicChanged,
            true                               // customGatt must be true for 128-bit UUIDs
        )
    );

    bool firstAttemptFailed = false;

    try
    {
        doSub();
    }
    catch (Exception ex)
    {
        Debug.LogWarning("Subscribe attempt 1 failed: " + ex.Message);
        firstAttemptFailed = true;
    }

    if (firstAttemptFailed)
    {
        // wait OUTSIDE the catch
        yield return new WaitForSeconds(0.5f);
        // try once more
        try { doSub(); }
        catch (Exception ex2)
        {
            Debug.LogError("Subscribe attempt 2 failed: " + ex2.Message);
            // optional: trigger a reconnect or rescan here
        }
    }

    if (logText) logText.text = "Subscribed… waiting for notifications";

    // Optional tiny write to RX to nudge stacks
    BleManager.Instance.QueueCommand(new WriteToCharacteristic(
        address,
        NUS_SERVICE.ToLowerInvariant(),
        NUS_RX_UUID.ToLowerInvariant(),
        System.Text.Encoding.UTF8.GetBytes("hi")
    ));
}







    // helper class for parsing
    [Serializable]
    public class MyIMUData
    {
        public float ax, ay, az;
        public float gx, gy, gz;
    }


//Logs the error, restart states, and restarts scanning
private void OnBleError(string error)
{
    isConnecting = false;
    _connected = false;

    Debug.LogError("BLE Error: " + error);
    if (logText) logText.text = "Error: " + error;

    // Always resume scanning on disconnect / GATT fail
    var e = error.ToLowerInvariant();
    if (e.Contains("disconnect") || e.Contains("cant find") || e.Contains("gatt") || e.Contains("closed"))
    {
        BleManager.Instance.QueueCommand(new DiscoverDevices(OnDeviceFound, OnScanFinished));
    }
}

    
    // private void OnCharacteristicChanged(byte[] bytes) //ALlows us to get the data as numbers
    // {
    //     try
    //     {
    //         if (bytes.Length >= 24)
    //         {
    //             float ax = BitConverter.ToSingle(bytes, 0);
    //             float ay = BitConverter.ToSingle(bytes, 4);
    //             float az = BitConverter.ToSingle(bytes, 8);
    //             float gx = BitConverter.ToSingle(bytes, 12);
    //             float gy = BitConverter.ToSingle(bytes, 16);
    //             float gz = BitConverter.ToSingle(bytes, 20);

    //             string msg = $"ax={ax:F2} ay={ay:F2} az={az:F2} | gx={gx:F0} gy={gy:F0} gz={gz:F0}";
    //             Debug.Log(msg);
    //             if (logText != null)
    //                 logText.text = msg;
    //         }
    //     }
    //     catch (Exception e)
    //     {
    //         Debug.LogError("Failed to decode BLE characteristic data: " + e);
    //     }
    // }


//Notification Handler, if its byte length is 24, it parses 6 floats (sx,ay,az,gx,gy,gz)
//If its byte length is 12, then it trys to decide if its accel or gyro (only makes sense if you purposely send two seperate 12 byte floats)
private void OnCharacteristicChanged(byte[] bytes)
{
    try
    {
        if (bytes == null || bytes.Length == 0)
        {
            if (logText) logText.text = "Notify: empty";
            return;
        }

        if (bytes.Length == 24)
        {
            float ax = BitConverter.ToSingle(bytes, 0);
            float ay = BitConverter.ToSingle(bytes, 4);
            float az = BitConverter.ToSingle(bytes, 8);
            float gx = BitConverter.ToSingle(bytes, 12);
            float gy = BitConverter.ToSingle(bytes, 16);
            float gz = BitConverter.ToSingle(bytes, 20);
            Show(ax, ay, az, gx, gy, gz);
            return;
        }

        if (bytes.Length == 12)
        {
            short ax_i = BitConverter.ToInt16(bytes, 0);
            short ay_i = BitConverter.ToInt16(bytes, 2);
            short az_i = BitConverter.ToInt16(bytes, 4);
            short gx_i = BitConverter.ToInt16(bytes, 6);
            short gy_i = BitConverter.ToInt16(bytes, 8);
            short gz_i = BitConverter.ToInt16(bytes,10);

            float ax = ax_i/1000f, ay = ay_i/1000f, az = az_i/1000f;
            float gx = gx_i/100f,  gy = gy_i/100f,  gz = gz_i/100f;

            Debug.Log($"IMU ax={ax:F3} ay={ay:F3} az={az:F3} gx={gx:F2} gy={gy:F2} gz={gz:F2}");
            // (Optional) also update on-screen TMP text
            if (logText) logText.text = $"ax={ax:F2} ay={ay:F2} az={az:F2} | gx={gx:F1} gy={gy:F1} gz={gz:F1}";
            return;
        }


        // Fallback preview for unexpected lengths
        var hex = BitConverter.ToString(bytes, 0, Math.Min(bytes.Length, 32));
        if (logText) logText.text = $"Notify len={bytes.Length} hex={hex}";
    }
    catch (Exception e)
    {
        Debug.LogError("Failed to decode BLE characteristic data: " + e);
        if (logText) logText.text = "Decode error: " + e.Message;
    }
}

private void Show(float ax, float ay, float az, float gx, float gy, float gz)
{
    string msg = $"ax={ax:F2} ay={ay:F2} az={az:F2} | gx={gx:F0} gy={gy:F0} gz={gz:F0}";
    Debug.Log(msg);
    if (logText) logText.text = msg;
}
}