// # Shows Pi is on by turning on LED when plugged in
// import machine
// LED = machine.Pin("LED", machine.Pin.OUT)
// LED.on()

// # --- NEW: BLE imports ---
// import bluetooth, struct
// from micropython import const

// # --- BLE IRQ event codes for your firmware ---
// _IRQ_CENTRAL_CONNECT    = const(1)
// _IRQ_CENTRAL_DISCONNECT = const(2)
// _IRQ_GATTS_WRITE        = const(3)


// from imu import MPU6050
// from time import sleep
// from machine import Pin, I2C

// # --- BLE: Nordic UART Service (NUS) UUIDs ---
// _UART_SERVICE_UUID = bluetooth.UUID("6E400001-B5A3-F393-E0A9-E50E24DCCA9E")
// _UART_TX_UUID      = bluetooth.UUID("6E400003-B5A3-F393-E0A9-E50E24DCCA9E")  # notify to PC
// _UART_RX_UUID      = bluetooth.UUID("6E400002-B5A3-F393-E0A9-E50E24DCCA9E")  # optional write from PC

// # ---- CHANGES START ----
// _FLAG_READ   = const(0x0002)
// _FLAG_WRITE  = const(0x0008)
// _FLAG_NOTIFY = const(0x0010)

// ble = bluetooth.BLE()
// ble.active(True)

// # TX = NOTIFY (and READ helps for quick manual reads), RX = WRITE
// tx_char = (_UART_TX_UUID, _FLAG_NOTIFY | _FLAG_READ)
// rx_char = (_UART_RX_UUID, _FLAG_WRITE)
// uart_service = (_UART_SERVICE_UUID, (tx_char, rx_char))
// _res = ble.gatts_register_services((uart_service,))

// # Newer builds: ((service_handle, (tx, rx)),)
// # Older/simpler builds: ((tx, rx),)
// try:
//     # Try the “newer” nested form first
//     tx_handle = _res[0][1][0]
//     rx_handle = _res[0][1][1]
// except TypeError:
//     # Fall back to the flat (tx, rx) form
//     tx_handle = _res[0][0]
//     rx_handle = _res[0][1]

// conn_handle = None
// notify_enabled = False

// def _cccd_handle_for_tx():
//     # CCCD is usually the handle right after the value handle
//     return tx_handle + 1

// def _ble_irq(event, data):
//     global conn_handle, notify_enabled
//     if event == _IRQ_CENTRAL_CONNECT:
//         conn_handle = data[0]
//         notify_enabled = False  # will flip True after CCCD write from the phone
//     elif event == _IRQ_CENTRAL_DISCONNECT:
//         conn_handle = None
//         notify_enabled = False
//         _advertise()
//     elif event == _IRQ_GATTS_WRITE:
//         # When the phone subscribes, it writes CCCD on TX; enable notifications then
//         if conn_handle is not None:
//             cccd = ble.gatts_read(_cccd_handle_for_tx())
//             # 0x0001 = notifications enabled
//             notify_enabled = (len(cccd) >= 2 and cccd[0] == 0x01)

// ble.irq(_ble_irq)

// def _advertise(name="PICO-IMU"):
//     # Flags + Complete Local Name
//     adv = b"\x02\x01\x06" + bytes([len(name)+1, 0x09]) + name.encode()
//     ble.gap_advertise(100_000, adv)  # 100 ms

// _advertise("PICO-IMU")

// # --- your existing IMU setup (unchanged) ---
// i2c = I2C(0, sda=Pin(0), scl=Pin(1), freq=400000)
// imu = MPU6050(i2c)

// while True:
//     # read your values
//     ax = float(imu.accel.x)
//     ay = float(imu.accel.y)
//     az = float(imu.accel.z)
//     gx = float(imu.gyro.x)
//     gy = float(imu.gyro.y)
//     gz = float(imu.gyro.z)

//     # keep your print if you like (debug)
//     print(f"ax {ax:.2f}\tay {ay:.2f}\taz {az:.2f}\tgx {gx:.0f}\tgy {gy:.0f}\tgz {gz:.0f}", end="\r")

//     # send a 24-byte packet: 6 float32 little-endian
//     if conn_handle is not None:
//         try:
//             ble.gatts_notify(conn_handle, tx_handle, struct.pack("<fff", ax, ay, az))
//             ble.gatts_notify(conn_handle, tx_handle, struct.pack("<fff", gx, gy, gz))

//         except OSError:
//             pass  # not subscribed yet or buffer momentarily full

//     sleep(0.02)  # ~50 Hz



// using UnityEngine;
// using TMPro;
// using Android.BLE;
// using System; 
// using Android.BLE.Commands; 
// using UnityEngine.Android;


// //Finds the adapter and attaches event handlers

// public class BleUIListener : MonoBehaviour
// {
//     public TMP_Text logText; //The text in screen so we can see the data flooding in

//     private BleAdapter adapter; 
//     private bool isConnecting = false;


//      // Replace with your MPU6050 BLE service UUID
//     //private const string IMU_SERVICE_UUID = "6E400001-B5A3-F393-E0A9-E50E24DCCA9E"; 
//    // private const string IMU_CHARACTERISTIC_UUID = "6E400003-B5A3-F393-E0A9-E50E24DCCA9E"; // TX from Pico
//     // Replace your two IMU consts with these three:
//     private const string NUS_SERVICE = "6E400001-B5A3-F393-E0A9-E50E24DCCA9E";
//     private const string NUS_RX_UUID = "6E400002-B5A3-F393-E0A9-E50E24DCCA9E"; // Write (phone -> Pico)
//     private const string NUS_TX_UUID = "6E400003-B5A3-F393-E0A9-E50E24DCCA9E"; // Notify (Pico -> phone)

//     private const string TARGET_MAC = "28:CD:C1:14:B8:3C";
    

//     //Helps whitelist mac address of pico
//     private static string NormalizeMac(string mac) {
//         if (string.IsNullOrEmpty(mac)) return mac;
//         mac = mac.Replace(":", "").ToUpperInvariant();
//         if (mac.Length == 12)
//             mac = string.Join(":", System.Text.RegularExpressions.Regex.Split(mac, @"(?<=\G..)(?!$)"));
//         return mac;
//     }



//     // void Start() //Finds BLE adapter in the scene
//     // {
//     //     adapter = FindObjectOfType<BleAdapter>();
//     //     if (adapter == null)
//     //     {
//     //         Debug.LogError("BleAdapter not found!");
//     //         return;
//     //     }

//     //     // Subscribe to real C# events
//     //    // adapter.OnMessageReceived += OnCharacteristicChanged; //We will rely on subscription callback
//     //     adapter.OnErrorReceived += OnBleError;

//     //     adapter.OnMessageReceived += (obj) =>
//     //     {
//     //         if (!string.IsNullOrEmpty(obj.Base64Message))
//     //             OnBleDataReceived(obj);
//     //     };

//     //     if (logText != null)
//     //         logText.text = "Ready to scan for BLE devices...";
        
//     //     if (!Permission.HasUserAuthorizedPermission("android.permission.BLUETOOTH_SCAN"))
//     //         Permission.RequestUserPermission("android.permission.BLUETOOTH_SCAN");

//     //     if (!Permission.HasUserAuthorizedPermission("android.permission.BLUETOOTH_CONNECT"))
//     //         Permission.RequestUserPermission("android.permission.BLUETOOTH_CONNECT");

//     //     if (!Permission.HasUserAuthorizedPermission("android.permission.ACCESS_FINE_LOCATION"))
//     //         Permission.RequestUserPermission("android.permission.ACCESS_FINE_LOCATION");

//     //     // Now safe to initialize BLE
//     //     BleManager.Instance.Initialize();

//     //     //Triggers scanning, and prints each device as its found
//     //     BleManager.Instance.QueueCommand(new DiscoverDevices(OnDeviceFound, OnScanFinished));

//     // }

//     private System.Collections.IEnumerator Start()
//     {
//         adapter = FindObjectOfType<BleAdapter>();
//         if (!adapter) { Debug.LogError("BleAdapter not found!"); yield break; }

//         adapter.OnErrorReceived += OnBleError;
//         adapter.OnMessageReceived += (obj) =>
//         {
//             if (!string.IsNullOrEmpty(obj.Base64Message))
//                 OnBleDataReceived(obj);
//         };

//         if (logText) logText.text = "Ready to scan for BLE devices...";

//         if (!Permission.HasUserAuthorizedPermission("android.permission.BLUETOOTH_SCAN"))
//             Permission.RequestUserPermission("android.permission.BLUETOOTH_SCAN");
//         if (!Permission.HasUserAuthorizedPermission("android.permission.BLUETOOTH_CONNECT"))
//             Permission.RequestUserPermission("android.permission.BLUETOOTH_CONNECT");
//         if (!Permission.HasUserAuthorizedPermission("android.permission.ACCESS_FINE_LOCATION"))
//             Permission.RequestUserPermission("android.permission.ACCESS_FINE_LOCATION");

//         // Wait up to ~3s for user to respond to prompts
//         float t = 0f;
//         while (t < 3f &&
//             (!Permission.HasUserAuthorizedPermission("android.permission.BLUETOOTH_SCAN") ||
//                 !Permission.HasUserAuthorizedPermission("android.permission.BLUETOOTH_CONNECT")))
//         {
//             t += Time.unscaledDeltaTime;
//             yield return null;
//         }

//         BleManager.Instance.Initialize();

//         // small delay to let adapter settle
//         yield return new WaitForSeconds(0.25f);

//         BleManager.Instance.QueueCommand(new DiscoverDevices(OnDeviceFound, OnScanFinished));
//     }


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


//     private void OnDeviceFound(string address, string name) //Called whenever a device is found during scanning
//     {
//         //Only connect to pico
//         if (NormalizeMac(address) != NormalizeMac(TARGET_MAC)) return;

//         // Prevent repeated connection attempts
//         if (isConnecting) return;
//         isConnecting = true;

//         Debug.Log($"Found device: {name} ({address})");
//         if (logText != null)
//             logText.text = $"Found: {name}";
//         //Connect to a device, OnDeviceConnected fires once the connection is estabilished
//         BleManager.Instance.QueueCommand(new ConnectToDevice(address, OnDeviceConnected, OnBleError));
//     }

//     private void OnScanFinished() //Allows a loop to keep scanning, since initally it scans only for 10 seconds, now it loops
//     {
//         Debug.Log("Scan finished. Restarting scan...");

//         BleManager.Instance.QueueCommand(new DiscoverDevices(OnDeviceFound, OnScanFinished));
//     }

// private void OnDeviceConnected(string address)
// {
//     isConnecting = false;
//     if (logText) logText.text = $"Connected: {address}";
//     StartCoroutine(SubscribeAfter(address));
// }
// private System.Collections.IEnumerator SubscribeAfter(string address)
// {
//     yield return new WaitForSeconds(0.3f);
//     BleManager.Instance.QueueCommand(
//         new SubscribeToCharacteristic(
//             address,
//             NUS_SERVICE.ToLowerInvariant(),
//             NUS_TX_UUID.ToLowerInvariant(),
//             OnCharacteristicChanged,
//             true // customGatt
//         )
//     );
//     if (logText) logText.text = "Subscribed… waiting for notifications";
// }



//     // helper class for parsing
//     [Serializable]
//     public class MyIMUData
//     {
//         public float ax, ay, az;
//         public float gx, gy, gz;
//     }

//     private void OnBleError(string error)
//     {
//         isConnecting = false;  // reset if it failed
//         Debug.LogError("BLE Error: " + error);
//         if (logText != null)
//             logText.text = "Error: " + error;

//         //If connection drops, scan again
//         if (error.ToLower().Contains("disconnect") || error.ToLower().Contains("cant find"))
//                 BleManager.Instance.QueueCommand(new DiscoverDevices(OnDeviceFound, OnScanFinished));
//     }
    
//     // private void OnCharacteristicChanged(byte[] bytes) //ALlows us to get the data as numbers
//     // {
//     //     try
//     //     {
//     //         if (bytes.Length >= 24)
//     //         {
//     //             float ax = BitConverter.ToSingle(bytes, 0);
//     //             float ay = BitConverter.ToSingle(bytes, 4);
//     //             float az = BitConverter.ToSingle(bytes, 8);
//     //             float gx = BitConverter.ToSingle(bytes, 12);
//     //             float gy = BitConverter.ToSingle(bytes, 16);
//     //             float gz = BitConverter.ToSingle(bytes, 20);

//     //             string msg = $"ax={ax:F2} ay={ay:F2} az={az:F2} | gx={gx:F0} gy={gy:F0} gz={gz:F0}";
//     //             Debug.Log(msg);
//     //             if (logText != null)
//     //                 logText.text = msg;
//     //         }
//     //     }
//     //     catch (Exception e)
//     //     {
//     //         Debug.LogError("Failed to decode BLE characteristic data: " + e);
//     //     }
//     // }

//     private void OnCharacteristicChanged(byte[] bytes)
//     {
//         try
//         {
//             if (bytes == null || bytes.Length == 0)
//             {
//                 if (logText) logText.text = "Notify: empty";
//                 return;
//             }

//             int preview = Math.Min(bytes.Length, 32);
//             var hex = BitConverter.ToString(bytes, 0, preview);
//             if (logText) logText.text = $"Notify len={bytes.Length} hex={hex}";

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
//                 if (logText) logText.text = msg;
//             }
//         }
//         catch (Exception e)
//         {
//             Debug.LogError("Failed to decode BLE characteristic data: " + e);
//             if (logText) logText.text = "Decode error: " + e.Message;
//         }
//     }



// }
// Why isnt it sending data its only saying connected: MACADDRESS then subscribed... waiting for notifications and no matter what changes we make its n ot sending data



// /*

// # Shows Pi is on by turning on LED when plugged in
// import machine
// LED = machine.Pin("LED", machine.Pin.OUT)
// LED.on()

// # --- NEW: BLE imports ---
// import bluetooth, struct
// from micropython import const

// # --- BLE IRQ event codes for your firmware ---
// _IRQ_CENTRAL_CONNECT    = const(1)
// _IRQ_CENTRAL_DISCONNECT = const(2)
// _IRQ_GATTS_WRITE        = const(3)


// from imu import MPU6050
// from time import sleep
// from machine import Pin, I2C

// # --- BLE: Nordic UART Service (NUS) UUIDs ---
// _UART_SERVICE_UUID = bluetooth.UUID("6E400001-B5A3-F393-E0A9-E50E24DCCA9E")
// _UART_TX_UUID      = bluetooth.UUID("6E400003-B5A3-F393-E0A9-E50E24DCCA9E")  # notify to PC
// _UART_RX_UUID      = bluetooth.UUID("6E400002-B5A3-F393-E0A9-E50E24DCCA9E")  # optional write from PC

// _FLAG_WRITE  = const(0x0008)
// _FLAG_NOTIFY = const(0x0010)

// ble = bluetooth.BLE()
// ble.active(True)

// # Register a service with TX (notify) + RX (write)
// tx_char = (_UART_TX_UUID, _FLAG_NOTIFY)
// rx_char = (_UART_RX_UUID, _FLAG_WRITE)
// uart_service = (_UART_SERVICE_UUID, (tx_char, rx_char))
// _res = ble.gatts_register_services((uart_service,))
// # Newer builds: ((service_handle, (tx, rx)),)
// # Older/simpler builds: ((tx, rx),)
// try:
//     # Try the “newer” nested form first
//     tx_handle = _res[0][1][0]
//     rx_handle = _res[0][1][1]
// except TypeError:
//     # Fall back to the flat (tx, rx) form
//     tx_handle = _res[0][0]
//     rx_handle = _res[0][1]

// conn_handle = None

// def _ble_irq(event, data):
//     global conn_handle
//     if event == _IRQ_CENTRAL_CONNECT:
//         # data = (conn_handle, addr_type, addr)
//         conn_handle = data[0]
//         # optional: print("BLE: connected", conn_handle)
//     elif event == _IRQ_CENTRAL_DISCONNECT:
//         # data = (conn_handle, reason)
//         # optional: print("BLE: disconnected")
//         conn_handle = None
//         _advertise()
//     elif event == _IRQ_GATTS_WRITE:
//         # optional: read from RX if you want commands from PC
//         pass

// ble.irq(_ble_irq)

// def _advertise(name="PICO-IMU"):
//     # Flags + Complete Local Name
//     adv = b"\x02\x01\x06" + bytes([len(name)+1, 0x09]) + name.encode()
//     ble.gap_advertise(100_000, adv)  # 100 ms

// _advertise("PICO-IMU")

// # --- your existing IMU setup (unchanged) ---
// i2c = I2C(0, sda=Pin(0), scl=Pin(1), freq=400000)
// imu = MPU6050(i2c)

// while True:
//     # read your values
//     ax = float(imu.accel.x)
//     ay = float(imu.accel.y)
//     az = float(imu.accel.z)
//     gx = float(imu.gyro.x)
//     gy = float(imu.gyro.y)
//     gz = float(imu.gyro.z)

//     # keep your print if you like (debug)
//     print(f"ax {ax:.2f}\tay {ay:.2f}\taz {az:.2f}\tgx {gx:.0f}\tgy {gy:.0f}\tgz {gz:.0f}", end="\r")
// # send a 12-byte packet (scaled int16 values)
// # ... after reading ax,ay,az,gx,gy,gz ...
//     if conn_handle is not None:
//         try:
//             ble.gatts_notify(conn_handle, tx_handle, struct.pack("<fff", ax, ay, az))
//             ble.gatts_notify(conn_handle, tx_handle, struct.pack("<fff", gx, gy, gz))
//         except OSError:
//             pass

//     # send a 24-byte packet: 6 float32 little-endian
//     #if conn_handle is not None:
//      #   try:
//       #      pkt = struct.pack("<ffffff", ax, ay, az, gx, gy, gz)
//        #     ble.gatts_notify(conn_handle, tx_handle, pkt)
//         #except OSError:
//          #   pass  # not subscribed yet or buffer momentarily full

//     sleep(0.02)  # ~50 Hz

// */