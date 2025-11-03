using UnityEngine;
using TMPro;
using Android.BLE;
using Android.BLE.Commands;
using UnityEngine.Android;
using System;
using System.Collections;
using System.Collections.Generic;

public class BleUIListener : MonoBehaviour
{
    public TMP_Text logText;

    private BleAdapter adapter;

    // Replace with your MPU6050 BLE service UUIDs
    private const string IMU_SERVICE_UUID = "6E400001-B5A3-F393-E0A9-E50E24DCCA9E";
    private const string IMU_CHARACTERISTIC_UUID = "6E400003-B5A3-F393-E0A9-E50E24DCCA9E"; // TX from Pico
    // If you know the Pico's MAC, set it here (any case/with or without colons is fine)
    private const string TARGET_MAC = "28:CD:C1:14:B8:3C";


    private HashSet<string> connectedDevices = new HashSet<string>();

    
////////////////////////////
private static bool LooksLikeMac(string s)
{
    if (string.IsNullOrEmpty(s)) return false;
    // Accept 12 hex (no colons) OR 17 with colons
    return System.Text.RegularExpressions.Regex.IsMatch(
        s, @"^([0-9A-Fa-f]{12}|[0-9A-Fa-f]{2}(:[0-9A-Fa-f]{2}){5})$");
}

private static string NormalizeMac(string mac)
{
    if (string.IsNullOrEmpty(mac)) return mac;
    mac = mac.Replace(":", "").ToUpperInvariant();
    if (mac.Length == 12)
        mac = string.Join(":", System.Text.RegularExpressions.Regex.Split(mac, @"(?<=\G..)(?!$)"));
    return mac; // AA:BB:CC:DD:EE:FF
}

// loosen as needed (add your exact advertized name)
private static bool NameLooksLikePico(string name)
{
    if (string.IsNullOrEmpty(name)) return false;
    return name.IndexOf("pico", StringComparison.OrdinalIgnoreCase) >= 0
        || name.IndexOf("imu",  StringComparison.OrdinalIgnoreCase) >= 0
        || name.IndexOf("nordic", StringComparison.OrdinalIgnoreCase) >= 0
        || name.IndexOf("uart", StringComparison.OrdinalIgnoreCase) >= 0;
}
private bool isConnectingOrConnected = false;
private readonly HashSet<string> seenThisScan = new HashSet<string>();




//////////////////////////////////
IEnumerator Start()
{
    if (logText) logText.text = "Initializing BLE system...\n";

    // --- Log Android SDK ---
    int sdkInt = 0;
#if UNITY_ANDROID && !UNITY_EDITOR
    using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
        sdkInt = version.GetStatic<int>("SDK_INT");
#endif
    if (logText) logText.text += $"Android SDK: {sdkInt}\n";

    // --- Request runtime permissions depending on SDK ---
    bool needsLocation = sdkInt < 31; // Android 11 and below
    bool requestedSomething = false;

    if (!Permission.HasUserAuthorizedPermission("android.permission.BLUETOOTH_SCAN"))
    {
        Permission.RequestUserPermission("android.permission.BLUETOOTH_SCAN");
        requestedSomething = true;
    }
    if (!Permission.HasUserAuthorizedPermission("android.permission.BLUETOOTH_CONNECT"))
    {
        Permission.RequestUserPermission("android.permission.BLUETOOTH_CONNECT");
        requestedSomething = true;
    }
    if (needsLocation && !Permission.HasUserAuthorizedPermission("android.permission.ACCESS_FINE_LOCATION"))
    {
        Permission.RequestUserPermission("android.permission.ACCESS_FINE_LOCATION");
        requestedSomething = true;
    }

    // --- Wait (with timeout) for whatever we requested to settle ---
    float t = 0f, timeout = 8f;
    while (t < timeout)
    {
        bool ok =
            Permission.HasUserAuthorizedPermission("android.permission.BLUETOOTH_SCAN") &&
            Permission.HasUserAuthorizedPermission("android.permission.BLUETOOTH_CONNECT") &&
            (!needsLocation || Permission.HasUserAuthorizedPermission("android.permission.ACCESS_FINE_LOCATION"));

        if (ok) break;

        t += Time.unscaledDeltaTime;
        yield return null;
    }

    // Log final permission state so you can see what's missing
    bool scanGranted    = Permission.HasUserAuthorizedPermission("android.permission.BLUETOOTH_SCAN");
    bool connectGranted = Permission.HasUserAuthorizedPermission("android.permission.BLUETOOTH_CONNECT");
    string fineLocState = needsLocation
        ? Permission.HasUserAuthorizedPermission("android.permission.ACCESS_FINE_LOCATION").ToString()
        : "N/A";

    if (logText)
        logText.text += "SCAN=" + scanGranted +
                        ", CONNECT=" + connectGranted +
                        ", FINE_LOC(only<31)=" + fineLocState + "\n";


    if (sdkInt < 31)
    {
        // Pre-Android 12 truly needs Location + classic BLUETOOTH perms
        if (!scanGranted || !connectGranted ||
            !Permission.HasUserAuthorizedPermission("android.permission.ACCESS_FINE_LOCATION"))
        {
            if (logText) logText.text += "Pre-Android12: missing BLE/Location permission. Aborting.\n";
            yield break;
        }
    }
    else
    {
        // Android 12+: Unity may misreport; try anyway and let OS throw if blocked
        if (!scanGranted || !connectGranted)
            if (logText) logText.text += "Unity may misreport BLE perms; attempting scan anyway...\n";
    }


    // --- Ensure a BleAdapter exists in the scene ---
    adapter = FindObjectOfType<BleAdapter>();
    if (adapter == null)
    {
        var go = new GameObject("BleAdapter");
        adapter = go.AddComponent<BleAdapter>();
        if (logText) logText.text += "Created BleAdapter at runtime.\n";
    }

    adapter.OnErrorReceived += OnBleError;

    // --- Initialize & sanity check Bluetooth enabled ---
    BleManager.Instance.Initialize();
    if (logText) logText.text += "BLE Manager initialized.\n";

#if UNITY_ANDROID && !UNITY_EDITOR
    try
    {
        using (var bluetoothAdapterClass = new AndroidJavaClass("android.bluetooth.BluetoothAdapter"))
        using (var adapterObj = bluetoothAdapterClass.CallStatic<AndroidJavaObject>("getDefaultAdapter"))
        {
            bool isEnabled = adapterObj != null && adapterObj.Call<bool>("isEnabled");
            if (!isEnabled)
            {
                if (logText) logText.text += "Bluetooth is OFF on the headset. Turn it on in settings.\n";
                yield break;
            }
        }
    }
    catch (Exception e)
    {
        Debug.LogWarning("Could not query Bluetooth state: " + e);
    }
#endif

    yield return new WaitForSeconds(1f);

    if (logText) logText.text += "Starting BLE scan...\n";
    BleManager.Instance.QueueCommand(new DiscoverDevices(OnDeviceFound, OnScanFinished));
}


    // private void OnDeviceFound(string address, string name)
    // {
    //     if (connectedDevices.Contains(address))
    //         return; // already connected

    //     connectedDevices.Add(address);

    //     Debug.Log($"Found device: {name} ({address})");
    //     if (logText != null)
    //         logText.text += $"Found device: {name}\n";

    //     // Connect to the device
    //     BleManager.Instance.QueueCommand(new ConnectToDevice(address, OnDeviceConnected, OnBleError));
    // }

// Whitelist connect: only your Pico's MAC
private void OnDeviceFound(string p1, string p2)
{
    // Detect which param is MAC vs name
    string macRaw = LooksLikeMac(p1) ? p1 : (LooksLikeMac(p2) ? p2 : null);
    string name   = LooksLikeMac(p1) ? p2 : p1;

    if (macRaw == null) return;                 // neither looked like a MAC
    string mac = NormalizeMac(macRaw);

    // Only proceed if it's the Pico you whitelisted
    if (NormalizeMac(TARGET_MAC) != mac) return;

    // De-dupe and prevent multiple simultaneous connects
    if (seenThisScan.Contains(mac) || connectedDevices.Contains(mac) || isConnectingOrConnected) return;

    seenThisScan.Add(mac);
    connectedDevices.Add(mac);
    isConnectingOrConnected = true;

    if (logText) logText.text += $"✅ Found Pico ({mac}) name='{name}'\n";
    Debug.Log($"✅ Found Pico ({mac}) name='{name}'");

    StartCoroutine(ConnectAfterShortDelay(mac));
}



private IEnumerator ConnectAfterShortDelay(string mac)
{
    yield return new WaitForSeconds(0.25f);
    BleManager.Instance.QueueCommand(new ConnectToDevice(mac, OnDeviceConnected, OnBleError));
}



    private void OnScanFinished()
    {
        Debug.Log("Scan finished.");
        if (logText != null) logText.text += "Scan finished.\n";

        seenThisScan.Clear(); // allow same MAC next scan

        if (connectedDevices.Count == 0)
            StartCoroutine(RestartScanWithDelay());
    }


    private IEnumerator RestartScanWithDelay()
    {
        yield return new WaitForSeconds(5f);
        BleManager.Instance.QueueCommand(new DiscoverDevices(OnDeviceFound, OnScanFinished));
    }

private void OnDeviceConnected(string address)
{
    string mac = NormalizeMac(address);
    Debug.Log("Connected to " + mac);
    if (logText != null) logText.text += $"Connected: {mac}\n";

    BleManager.Instance.QueueCommand(
        new SubscribeToCharacteristic(mac, IMU_SERVICE_UUID, IMU_CHARACTERISTIC_UUID, OnCharacteristicChanged)
    );
}


private void OnBleError(string error)
{
    Debug.LogError("BLE Error: " + error);
    if (logText != null) logText.text += $"BLE Error: {error}\n";

    if (error.IndexOf("SecurityException", StringComparison.OrdinalIgnoreCase) >= 0 ||
        error.IndexOf("android.permission", StringComparison.OrdinalIgnoreCase) >= 0)
    {
        if (logText) logText.text +=
            "=> OS blocked BLE. Check App Permissions > Nearby devices, or reinstall and allow prompts.\n";
    }

    if (error.IndexOf("can't find connected device", StringComparison.OrdinalIgnoreCase) >= 0 ||
        error.IndexOf("cant find connected device", StringComparison.OrdinalIgnoreCase) >= 0)
    {
        isConnectingOrConnected = false;
        StartCoroutine(RestartScanWithDelay());
    }
}


    private void OnCharacteristicChanged(byte[] bytes)
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
                Debug.Log(msg);
                if (logText != null)
                    logText.text = msg;
            }
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to decode BLE data: " + e);
        }
    }
}
