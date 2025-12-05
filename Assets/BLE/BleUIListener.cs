using UnityEngine;
using TMPro;
using Android.BLE;
using System;
using Android.BLE.Commands;
using UnityEngine.Android;

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
    private float rotationSpeed = 1.0f;



    // Replace with your MPU6050 BLE service UUID
    private const string NUS_SERVICE = "6E400001-B5A3-F393-E0A9-E50E24DCCA9E"; //Identifies the entire Nordic UART service (folder)
    private const string NUS_RX_UUID = "6E400002-B5A3-F393-E0A9-E50E24DCCA9E"; // Write (Quest -> Pico)
    private const string NUS_TX_UUID = "6E400003-B5A3-F393-E0A9-E50E24DCCA9E"; // Notify (Pico -> Quest)

    //The MAC address of PICO-IMU (Might change depending on wifi, so far it hasnt changed)
    private const string TARGET_MAC = "28:CD:C1:14:B8:3C";

    //Initially the data was sent as 1 package of floats in the python code, around 26 bytes, but the script only allowed for 20 bytes
    //So instead, we send it as integers, in 2 packages, the accel and gyro. Then we convert said numbers to floats in C#
    private float[] _acc3, _gyro3;



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

        // Wait up to ~3s for user to respond to prompts
        float t = 0f;
        while (t < 3f &&
            (!Permission.HasUserAuthorizedPermission("android.permission.BLUETOOTH_SCAN") ||
                !Permission.HasUserAuthorizedPermission("android.permission.BLUETOOTH_CONNECT")))
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        //The unity component, it handles callbacks raised from Android
        //It provides events like forwarding device discovery, errors, and completed commands
        adapter = FindObjectOfType<BleAdapter>();

        if (!adapter) { Debug.LogError("BleAdapter not found!"); yield break; }

        adapter.OnErrorReceived += OnBleError;


        if (logText) logText.text = "Ready to scan for BLE devices...";

        //Singleton that handles the entire BLE stack on Android
        BleManager.Instance.Initialize();

        // small delay to let adapter settle
        yield return new WaitForSeconds(0.25f);

        //DiscoverDevices allows the BLE to scan for devices and calls a callback for a seen device
        //The Quest listens, and everytime a BLE device advertises, the plugin calls OnDeviceFound(address, name)
        BleManager.Instance.QueueCommand(new DiscoverDevices(OnDeviceFound, OnScanFinished)); //This STARTS the scanning
    }




    //Filters PICO by name or exact MAC address. If it matches and we arent connected, it queues it to ConnectToDevice
    //I think this is where it goes onceit scans and finds a device, and this function allows us to connect
    private void OnDeviceFound(string address, string name) //When the device is found, filters to only the pico then calls Connect
    {
        bool looksLikePico =
            (!string.IsNullOrEmpty(name) && name.IndexOf("PICO", StringComparison.OrdinalIgnoreCase) >= 0)
            || string.Equals(NormalizeMac(address), NormalizeMac(TARGET_MAC), StringComparison.OrdinalIgnoreCase);

        if (!looksLikePico || isConnecting) return; //This stops it from continuously connecting to devices all the time
        //Note: This might be why it wont reconnect once we loose connection
        isConnecting = true;

        Debug.Log($"Found device: {name} ({address})");
        if (logText) logText.text = $"Found: {name}";

        //It opens a low-power radio link, and allows us to read and write as well as subscribe to notifications
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
    private void OnDeviceConnected(string address) //Starts the subscribe coroutine
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
            new SubscribeToCharacteristic( //This allows us to subscribe to characterisitc so we recieve IMU notifications
                address,
                NUS_SERVICE.ToLowerInvariant(),   // safest: lowercase
                NUS_TX_UUID.ToLowerInvariant(),
                OnCharacteristicChanged, //Everytime the Pico sends IMU data, the plugin fires this callback, this is the ONLY place you get data
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
    //rescan isn't working for some reason, so when IMU disconnects, you have to restart the game
    private void OnBleError(string error)
    {
        isConnecting = false;
        _connected = false;

        Debug.LogError("BLE Error: " + error);
        if (logText) logText.text = "Error: " + error;

        // Reinitialize BLE stack
        BleManager.Instance.Initialize();

        // Restart scanning after short delay
        StartCoroutine(RestartScanDelay());
    }

    private System.Collections.IEnumerator RestartScanDelay()
    {
        yield return new WaitForSeconds(0.4f);
        BleManager.Instance.QueueCommand(new DiscoverDevices(OnDeviceFound, OnScanFinished));
    }


    //Notification Handler, if its byte length is 24, it parses 6 floats (sx,ay,az,gx,gy,gz)
    //If its byte length is 12, then it trys to decide if its accel or gyro (only makes sense if you purposely send two seperate 12 byte floats)
    private void OnCharacteristicChanged(byte[] bytes) //Data arrives
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

                UpdateRotation(gx, gy, gz);
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
                short gz_i = BitConverter.ToInt16(bytes, 10);

                float ax = ax_i / 1000f, ay = ay_i / 1000f, az = az_i / 1000f;
                float gx = gx_i / 100f, gy = gy_i / 100f, gz = gz_i / 100f;

                Debug.Log($"IMU ax={ax:F3} ay={ay:F3} az={az:F3} gx={gx:F2} gy={gy:F2} gz={gz:F2}");

                UpdateRotation(gx, gy, gz);
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

    //This allows the object to rotate given the IMU data
    private void UpdateRotation(float gx, float gy, float gz)
    {
        float dt = Time.deltaTime;

        // Gyro values are degrees/sec, integrate to degrees
        currentRotation.x += gx * dt;
        currentRotation.y += gy * dt;
        currentRotation.z += gz * dt;

        if (jackhammer != null)
            jackhammer.localRotation = Quaternion.Euler(currentRotation);
    }
}