# PID Lab VR Rehabilitation Software

# HOW TO USE THIS REPOSITORY

## Software Needed Before Cloning

- **Unity Game Engine** – with editor 2022.3.8f1 installed, make sure that the Android Build Support module is installed with the editor as well

- **SideQuest** – This is a software that allows users to install custom game builds onto VR headsets, and it also allows users to search through the headset files, so we can grab the CSV files that the game is creating  
  - Download Link: https://sidequestvr.com/setup-howto (make sure to download the desktop app and not the VR app)

- **Thonny** – This software allows users to change code that is located on a Raspberry Pi  
  - Download Link: https://thonny.org/

- **Android Debug Bridge (ADB)** – Allows a user to use a console on a computer for debugging purposes while testing a build on the VR headset, because normally you can’t see a console that displays errors and debug logs, so it is very useful for debugging  
  - Download Link: https://developer.android.com/tools/releases/platform-tools

- **Meta Horizon Link** – Allows a user to test with the VR headset with the Unity Editor in real time, useful for simple testing, but Unity doesn’t connect to the Bluetooth IMU circuit when testing this way. But, it is good for testing changes not related to Bluetooth  
  - Download Link: https://www.meta.com/help/quest/1517439565442928/

---

## How to Create a Build and Test It

- After cloning the repository, open Unity Hub
- Then click on the “Projects” tab
- Click on **Add → Add project from disk**
- Locate where you cloned the repository and open the folder named **BluetoothtestCS491**
- Click on Edit, then Project Settings(We are going to confirm that the hand tracking is enabled)
- Click on XR Plug-in Management, click on the Android tab and make sure that the OpenXR check box is checked
- Then Click on OpenXR and then click on the Android tab
- Check the Hand Tracking Subsystem and Meta Quest Support checkboxes
- Then click the plus button and add the Hand Interaction Profile, Meta Quest Touch Pro Controller Profile, and Meta Quest Touch Plus Controller Profile
- Close out of the window, then click on **File → Build Settings**
- Switch the Platform to **Android** if needed
- Then click **Build**
- After building, open SideQuest and plug in the VR headset into the computer with a USB-C cable
- On the top right of SideQuest, there is a button called **Install APK from Folder on Computer**. Click on that
- Locate the Build file (.apk file) and install it onto the headset.  
  **Note:** Make sure that you delete any old build from the headset, or else the install will fail

- Once the build is installed on the headset, plug in the IMU circuit to power, and put the headset on
- Once the headset is powered on, click on **Library**, then **Unknown Sources**, and you should see the build named something like **BluetoothTest**
- Open the game, then allow the game to access Bluetooth when the pop-up appears, and then click on **Approximate Location** when the next pop-up appears
- Now the game will crash (This happens every time you test out the build for the FIRST time. We tried making it so this doesn’t happen, but our efforts failed)
- Reopen the game, and now you should see the IMU object in the game, rotating with the real-life counterparts' rotation
- Every 45 seconds, the game will paste the location and rotation of the player's hands + joints onto a CSV file that we will extract later (this will cause the game to freeze for a second, so don’t panic if you think the game crashed)

## How to Quit Out of Game
- Turn your palm to face towards you, and pinch with your thumb and index finger, the Meta Quest Home Screen should appear
- pinch on the Quit button to shut down the game

---

## How to Locate the CSV Files

- Open up SideQuest and make sure the headset is plugged into the computer
- Then, in SideQuest, on the top right, click on the **Manage files on the headset** button
- Then click on **Android → data → com.unity.template.vr → files**
- Inside the files folder, you should see a CSV file named something like  
  **HandRecording_LEFT_2025-12-12_13-15-54**, **HandRecording_RIGHT_2025-12-12_13-15-54**, and **IMU_Recording_2025-12-16_00-32-01.csv**
- Download these files onto your computer and open it to see if the values printed

---

## IMPORTANT CODE FILES AND HOW TO SET UP THE IMU CIRCUIT

### HOW TO SET UP CIRCUIT

Here is a youtube link on how to put the wiring together for the IMU Circuit.
https://www.youtube.com/watch?v=HezXoT12E40&t=9&authuser=1

### HandTracking.cs

HandTracking.cs is useful for the sake of recreating the patient's movements for future study. It works by calling the 26 joint values from the XR subsystem and storing each one of those joint values as rotational and position values to stream, which then pushes it to a file. This was done so that if the program isn’t shut down properly, the information is still saved, and if it were constantly writing the program every frame, it would cause it to slow down.

### BleUIListener.cs

BleUIListener.cs is crucial for the Bluetooth connection, as it enables Unity to establish a Bluetooth connection and communicate with an Android device. It works by scanning for a Bluetooth device, which is filtered specifically to find the IMU’s MAC address and name. After it finds the pico, it connects to the device and subscribes to its characteristics, and waits for the notifications to collect data. If it fails to connect initially, it rescans until it finds the pico and successfully connects to it. Also houses the code for creating the IMU csv creation to track the IMU's position and rotation.

### Main.py

Main.py is also important since that is the code that's running on the Raspberry Pi. Its goal is to collect the IMU data and send it over Bluetooth to the VR headset. It's the device that the headset connects to and will be getting the data from. It sends a signal to devices searching for Bluetooth and sends them notifications/data.

Main.py configures a Raspberry Pi Pico W connected to an MPU6050 inertial measurement unit (IMU) to function as a Bluetooth Low Energy (BLE) motion sensor. The system continuously captures accelerometer and gyroscope data from the IMU and wirelessly streams this information to a connected device, such as a VR headset or computer, in real time. The device advertises itself over Bluetooth as **PICO-IMU**, allowing external systems to discover and connect to it easily.

When the program starts, the Pico W turns on its onboard LED, providing a simple visual indicator that the device is powered and running correctly. The code then initializes the Bluetooth subsystem and activates BLE functionality, which is required before any services can be created or advertised.

A Nordic UART Service (NUS) is configured, which is a commonly used BLE profile for transmitting continuous streams of data. This service includes one characteristic for transmitting IMU data to the connected device and another optional characteristic for receiving data.

After the BLE service is registered, the Pico W begins advertising itself as a connectable BLE device. Advertising data includes the device name and service UUIDs to improve detection and compatibility, particularly on Android-based systems.

The program also sets up event handlers that respond to Bluetooth connection events, such as when a central device connects, disconnects, or subscribes to notifications. If a device disconnects, the Pico W automatically resumes advertising so that another device can connect.

The program then initializes communication with the MPU6050 IMU using the I2C protocol. This allows the Pico W to read sensor data from the IMU, including three-axis acceleration and three-axis angular velocity.

Once initialization is complete, the system enters a continuous loop where it repeatedly reads motion data from the sensor.

Inside this loop, the accelerometer and gyroscope values are retrieved, formatted, and printed to the serial console for debugging purposes. When a Bluetooth connection is active and notifications are enabled, the motion data is scaled, packed into a compact binary format, and transmitted to the connected device using BLE notifications.

The data transmission occurs at approximately 50 Hz, which is sufficient for motion tracking applications such as virtual reality input or gesture recognition.

Overall, this program transforms the Raspberry Pi Pico W and MPU6050 into a wireless IMU streaming device, acting as a bridge between physical motion sensors and a VR system by delivering real-time movement data over Bluetooth Low Energy.

FYI: The files should still be on the PI, but in case they're lost, you should find it in the directory.

---

## HOW TO RECREATE FROM SCRATCH IF SOMETHING GOES WRONG

### Hardware

- Raspberry Pi Pico W
- IMU MPU6050
- BreadBoard

---

### Setting Up the Unity Project

- Switch platform to Android  
  - Go to **File → Build Settings**  
  - Select **Android**  
  - Click **Switch Platform**

- Make sure the project allows a custom Main Manifest  
  - Go to **Edit → Project Settings → Player**

- Install XR Plugin Management  
  - Go to **Window → Project Manager → XR Plug-In Management**  
  - Click **Install XR Plugin Management**  
  - After it is installed, go to XR plugin management in the same edit menu, click on **Open XR**  
  - Then click XR plugin management, then click **Open XR**. Then, under **Open XR Feature Group**, make sure **Hand Tracking Subsystem** is selected.

- Install XR Interaction Toolkit  
  - Go to **Window → Project Manager**  
  - Click on the plus button on the top left of the window  
  - Click on **Add package by name**  
  - Type in **com.unity.xr.interaction.toolkit** and install the package  
  - After installing the package, click on samples, then import the **Starter Assets** and **Hands Interaction Demo** assets

- Install XR Hands Plugin  
  - Go to **Window → Project Manager**  
  - Click on the plus button on the top left of the window  
  - Click on **Add package by name**  
  - Type in **com.unity.xr.hands** and install the package  
  - After installing the package, click on samples, and import the **HandVisualizer** asset
 
- Enable Hand Tracking in the Game  
  - Click on Edit, then Project Settings
  - Click on XR Plug-in Management, click on the Android tab and make sure that the OpenXR check box is checked
  - Then Click on OpenXR and then click on the Android tab
  - Check the Hand Tracking Subsystem and Meta Quest Support checkboxes
  - Then click the plus button and add the Hand Interaction Profile, Meta Quest Touch Pro Controller Profile, and Meta Quest Touch Plus Controller Profile
  - Close out of the window

- Make sure the TextMesh Pro is downloaded (If you want to set up an output textbox of data)  
  - Same as installing XR Plugin Management, but download TextMesh Pro instead

- Make sure the Android Manifest uses these permissions:  
  - BLUETOOTH  
  - BLUETOOTH_SCAN  
  - BLUETOOTH_CONNECT  
  - BLUETOOTH_ADVERTISE  
  - BLUETOOTH_ADMIN  
  - ACCESS_FINE_LOCATION

---

## Folder Layout

Drag the **Editor** and **Samples~** folder from the Velorexe GitHub repo into the Assets folder of the Unity game.

Drag the **Plugins** folder from the Velorexe GitHub repo into the Assets folder of the Unity game.

Open the Plugins folder and drag the **Runtime** folder from the Velorexe GitHub repo into that folder.

Make a new folder named **BLE** in the assets folder and drag in the existing **BleUIListener** file that was created through this project.

---

## Bluetooth + Enabling Recording for the IMU position and rotation CSV

- Make a new empty object named **BleAdapter**
- Drag the **BleAdapter** script from **Assets → Plugins → Runtime → BLE** and attach it to the object
- Make a new empty object named **BleUIListener**
- Drag the **BleUIListener** script from **Assets → BLE** and attach it to the object in the inspector
- In the BleUIListener object inspector, drag the **Text Mesh Pro** object and attach it to the **Log Text** part of the script in the inspector
- Do the same thing as the step above, but with the object you want to track, and drag it into the **Jackhammer** slot
- Then check the Record box to enable recording IMU postion and rotation to a CSV file
- The file will record every 45 seconds the player spends in game
- When testing your code, it's best practice to turn off the IMU tracking by unchecking the **Record** box in the inspector, as it will continue to store these files in the headset, wasting memory.

---

## Hand Tracking

- After installing the XR Plugin Management, XR Interaction Toolkit, and XR Hands packages, setting up the hand tracking should not be too difficult
- Go to  
  **Assets → Samples → XR Interaction Toolkit → 3.1.2 (or whatever version you have installed) → Starter Assets → Prefabs**,  
  then drag the **XR Origin Hands (XR Rig)** prefab into the hierarchy, and hand tracking should work.  
  If it doesn't work, here is a YouTube tutorial that should help:  
  https://www.youtube.com/watch?v=mJ3fygb9Aw0

---

## SideQuest Info

- SideQuest is the free software we used to access the files created by storing the hand values in a CSV file. You connect using a USB-C cable from your computer to the headset. We also used SideQuest to install our newest builds onto the VR headset.  
  Download Link: https://sidequestvr.com/setup-howto

- The file should be under  
  **Android/data/com.unity.template.vr/files**,  
  with the name **HandRecording**, followed by the date and time it was created, for example,  
  **HandRecording_2025-12-12_13-15-54**

---

## Hand Information Tracking

- Create a new empty game object and name it **HandLogger**
- Drag the **Hand Recorder** script located in **Assets → Scripts** into HandLogger
- Then, in the inspector, check the **record** box
- While the game is running, it will freeze for a second at 45 seconds. This is because the hand values are stored in the CSV file.
- When testing your code, it's best practice to turn off Handtracking by unchecking the **Record** box in the inspector, as it will continue to store these files in the headset, wasting memory.


