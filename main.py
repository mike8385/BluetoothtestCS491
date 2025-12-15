'''
This program turns the Raspberry Pi Pico W and MPU6050 into a
Bluetooth Low Energy IMU sensor that advertises itself as 
PICO-IMU and continously streams accelerometer + gyroscope data
to any connected device (The VR headset).


The code itself, is more confusing then the C# code, we used a youtube video to show how
to get the IMU to send data to the Raspberry Pi, and then we had to figure out how to send
the data through bluetooth. So I apologize in advance if my comments arent the best, since half
of this file is from a youtube video sent by the project head.
'''


# Shows Pi is on by turning on LED when plugged in
import machine
LED = machine.Pin("LED", machine.Pin.OUT)
LED.on()

# --- NEW: BLE imports ---
import bluetooth, struct
from micropython import const

# --- BLE IRQ event codes for your firmware ---
# These are BLE event IDs
_IRQ_CENTRAL_CONNECT    = const(1) # Connect to a device
_IRQ_CENTRAL_DISCONNECT = const(2) # Disconnect a device
_IRQ_GATTS_WRITE        = const(3) # Writes to a characteristic


from imu import MPU6050 # Imports the MPU6050 IMU driver
from time import sleep
from machine import Pin, I2C

# --- BLE: Nordic UART Service (NUS) UUIDs ---
_UART_SERVICE_UUID = bluetooth.UUID("6E400001-B5A3-F393-E0A9-E50E24DCCA9E")
_UART_TX_UUID      = bluetooth.UUID("6E400003-B5A3-F393-E0A9-E50E24DCCA9E")  # notify to PC
_UART_RX_UUID      = bluetooth.UUID("6E400002-B5A3-F393-E0A9-E50E24DCCA9E")  # optional write from PC

# ---- CHANGES START ----
_FLAG_READ   = const(0x0002) # Allows the characterisitc to be read
_FLAG_WRITE  = const(0x0008) # Allows the characteristic to be written to
_FLAG_NOTIFY = const(0x0010)

# Enables BLE
ble = bluetooth.BLE() # creates a controller object
ble.active(True) # Turns on the BLE, required before advertising or creating services

# TX = NOTIFY (and READ helps for quick manual reads), RX = WRITE
tx_char = (_UART_TX_UUID, _FLAG_NOTIFY | _FLAG_READ)
rx_char = (_UART_RX_UUID, _FLAG_WRITE)
uart_service = (_UART_SERVICE_UUID, (tx_char, rx_char))

#This registers the service with the BLE stack
_res = ble.gatts_register_services((uart_service,))

# Newer builds: ((service_handle, (tx, rx)),)
# Older/simpler builds: ((tx, rx),)
try:
    # Try the “newer” nested form first
    tx_handle = _res[0][1][0]
    rx_handle = _res[0][1][1]
except TypeError:
    # Fall back to the flat (tx, rx) form
    tx_handle = _res[0][0]
    rx_handle = _res[0][1]

conn_handle = None
notify_enabled = False

def _cccd_handle_for_tx():
    # CCCD is usually the handle right after the value handle
    return tx_handle + 1

def _ble_irq(event, data):
    global conn_handle, notify_enabled
    if event == _IRQ_CENTRAL_CONNECT:
        conn_handle = data[0]
        notify_enabled = False  # will flip True after CCCD write from the phone
    elif event == _IRQ_CENTRAL_DISCONNECT:
        conn_handle = None
        notify_enabled = False
        _advertise()
    elif event == _IRQ_GATTS_WRITE:
        # When the phone subscribes, it writes CCCD on TX; enable notifications then
        if conn_handle is not None:
            cccd = ble.gatts_read(_cccd_handle_for_tx())
            # 0x0001 = notifications enabled
            notify_enabled = (len(cccd) >= 2 and cccd[0] == 0x01)

ble.irq(_ble_irq)

def _advertise(name="PICO-IMU"):
    # ---- ADVERTISE DATA ----
    adv_data = b"\x02\x01\x06"               # Flags

    # ---- SCAN RESPONSE: Complete Local Name ----
    resp_name = bytes([len(name)+1, 0x09]) + name.encode()

    # ---- OPTIONAL: include NUS UUID for better detection ----
    nus = bytes(reversed(bytes.fromhex("6E400001B5A3F393E0A9E50E24DCCA9E")))
    resp_uuid = bytes([len(nus)+1, 0x07]) + nus

    resp_data = resp_name + resp_uuid

    # Also set GAP name (helps Android!)
    ble.config(gap_name=name)

    # Start advertising
    ble.gap_advertise(
        100_000,
        adv_data=adv_data,
        resp_data=resp_data,
        connectable=True
    )

# Call it
_advertise("PICO-IMU")


# --- your existing IMU setup (unchanged) ---
i2c = I2C(0, sda=Pin(0), scl=Pin(1), freq=400000)
imu = MPU6050(i2c)

while True:
    # read your values
    ax = float(imu.accel.x)
    ay = float(imu.accel.y)
    az = float(imu.accel.z)
    gx = float(imu.gyro.x)
    gy = float(imu.gyro.y)
    gz = float(imu.gyro.z)

    # keep your print if you like (debug)
    print(f"ax {ax:.2f}\tay {ay:.2f}\taz {az:.2f}\tgx {gx:.0f}\tgy {gy:.0f}\tgz {gz:.0f}", end="\r")

    # send a 24-byte packet: 6 float32 little-endian
    if conn_handle is not None:
        try:
            # ax,ay,az in g; gx,gy,gz in dps
            ax_i, ay_i, az_i = int(ax*1000), int(ay*1000), int(az*1000)
            gx_i, gy_i, gz_i = int(gx*100),  int(gy*100),  int(gz*100)
            pkt = struct.pack("<hhhhhh", ax_i, ay_i, az_i, gx_i, gy_i, gz_i)
            ble.gatts_notify(conn_handle, tx_handle, pkt)

            #pkt = struct.pack("<ffffff", ax, ay, az, gx, gy, gz)
            #ble.gatts_notify(conn_handle, tx_handle, pkt)

        except OSError:
            pass  # not subscribed yet or buffer momentarily full

    sleep(0.02)  # ~50 Hz

