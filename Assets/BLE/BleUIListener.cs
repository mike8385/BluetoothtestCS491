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

    private HashSet<string> connectedDevices = new HashSet<string>();

    IEnumerator Start()
    {
        if (logText != null)
            logText.text = "Initializing BLE system...\n";

        // 1️⃣ Request permissions
        if (!Permission.HasUserAuthorizedPermission("android.permission.BLUETOOTH_SCAN"))
            Permission.RequestUserPermission("android.permission.BLUETOOTH_SCAN");
        if (!Permission.HasUserAuthorizedPermission("android.permission.BLUETOOTH_CONNECT"))
            Permission.RequestUserPermission("android.permission.BLUETOOTH_CONNECT");
        if (!Permission.HasUserAuthorizedPermission("android.permission.ACCESS_FINE_LOCATION"))
            Permission.RequestUserPermission("android.permission.ACCESS_FINE_LOCATION");

        yield return new WaitUntil(() =>
            Permission.HasUserAuthorizedPermission("android.permission.BLUETOOTH_SCAN") &&
            Permission.HasUserAuthorizedPermission("android.permission.BLUETOOTH_CONNECT") &&
            Permission.HasUserAuthorizedPermission("android.permission.ACCESS_FINE_LOCATION")
        );

        logText.text += "Permissions granted.\n";

        // 2️⃣ Find BLE adapter
        adapter = FindObjectOfType<BleAdapter>();
        if (adapter == null)
        {
            Debug.LogError("BleAdapter not found!");
            logText.text += "BleAdapter not found!\n";
            yield break;
        }

        adapter.OnErrorReceived += OnBleError;

        // 3️⃣ Initialize BLE manager
        BleManager.Instance.Initialize();
        logText.text += "BLE Manager initialized.\n";

        yield return new WaitForSeconds(1f); // small delay before scanning

        // 4️⃣ Start scanning
        logText.text += "Starting BLE scan...\n";
        BleManager.Instance.QueueCommand(new DiscoverDevices(OnDeviceFound, OnScanFinished));
    }

    private void OnDeviceFound(string address, string name)
    {
        if (connectedDevices.Contains(address))
            return; // already connected

        connectedDevices.Add(address);

        Debug.Log($"Found device: {name} ({address})");
        if (logText != null)
            logText.text += $"Found device: {name}\n";

        // Connect to the device
        BleManager.Instance.QueueCommand(new ConnectToDevice(address, OnDeviceConnected, OnBleError));
    }

    private void OnScanFinished()
    {
        Debug.Log("Scan finished.");
        if (logText != null)
            logText.text += "Scan finished.\n";

        // Optional: restart scan only if no device was connected
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
        Debug.Log("Connected to " + address);
        if (logText != null)
            logText.text += $"Connected: {address}\n";

        // Subscribe to the characteristic
        BleManager.Instance.QueueCommand(
            new SubscribeToCharacteristic(address, IMU_SERVICE_UUID, IMU_CHARACTERISTIC_UUID, OnCharacteristicChanged)
        );
    }

    private void OnBleError(string error)
    {
        Debug.LogError("BLE Error: " + error);
        if (logText != null)
            logText.text += $"BLE Error: {error}\n";
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
