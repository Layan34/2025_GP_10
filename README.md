# Rakkiz | ركّز

## Introduction

Rakkiz is a graduation project that introduces an interactive attention-training game powered by Brain-Computer Interface (BCI) technology. The system uses EEG signals to support real-time attention monitoring and adaptive gameplay. Through two game modes, Rassd and Tayyar, the application helps players practice focus in an engaging environment while collecting behavioral and EEG-related session data.

In Release 2, the project includes the implemented Unity application, EEG data processing components, Lab Streaming Layer (LSL) marker integration, Emotiv-LSL streaming support, runtime inference support, session data saving, and a dashboard for reviewing player performance.

## Technology

- Unity 2022.3.x LTS
- C# for Unity scripting
- Python for EEG preprocessing, calibration, and runtime inference
- Lab Streaming Layer (LSL) for EEG and marker synchronization
- LabRecorder for XDF recording
- Emotiv Launcher for EEG headset connection
- Cortex API for Emotiv EEG access
- GitHub for version control and repository management
- Jira for project management

## Repository Structure

```text
2025_GP_10/
│
├── Project_Files/
│   ├── Unity/
│   └── EEG_Data/
│
├── External_Tools/
│   └── emotiv-lsl/
│
├── Documents/
├── README.md
├── Authors.txt
├── .gitignore
└── .gitattributes
```

## Project Files

### Unity Application

The Unity folder contains the main game application, scenes, scripts, UI, prefabs, assets, and project settings.

```text
Project_Files/Unity
```

### EEG_Data

The EEG_Data folder contains the Python scripts and data folders used for EEG preprocessing, player calibration, and runtime inference.

Important runtime files include:

```text
Project_Files/EEG_Data/player_scripts/5_runtime_pipeline.py
Project_Files/EEG_Data/player_scripts/8_runtime_inference_server.py
Project_Files/EEG_Data/requirements.txt
```

### Emotiv-LSL

The Emotiv-LSL folder is used to stream EEG data from Emotiv into LSL.

For repository organization, it is included in:

```text
External_Tools/emotiv-lsl
```

For reliable runtime execution, copy the `emotiv-lsl` folder to the Desktop before running the full EEG version:

```text
C:\Users\<YourUsername>\Desktop\emotiv-lsl
```

If the computer uses OneDrive Desktop, it can also be placed here:

```text
C:\Users\<YourUsername>\OneDrive\Desktop\emotiv-lsl
```

The folder must contain:

```text
emotiv-lsl/
├── main.py
├── requirements.txt
└── other required files
```

## System Requirements

Before running the project, make sure the following are installed:

- Unity Hub
- Unity 2022.3.x LTS
- Python 3.x
- Emotiv Launcher
- Emotiv account
- Emotiv EEG headset
- Cortex API access
- LabRecorder
- Required Python libraries listed in `Project_Files/EEG_Data/requirements.txt`

## Python Setup

Open a terminal in the EEG_Data folder:

```bash
cd Project_Files/EEG_Data
```

Create a virtual environment:

```bash
python -m venv .venv
```

Activate the virtual environment on Windows:

```bash
.venv\Scripts\activate
```

Install the required libraries:

```bash
pip install -r requirements.txt
```

The Unity project expects the Python virtual environment to be created inside:

```text
Project_Files/EEG_Data/.venv
```

## Emotiv-LSL Setup

Before running the full EEG-based version, copy the Emotiv-LSL folder from:

```text
External_Tools/emotiv-lsl
```

to the Desktop:

```text
C:\Users\<YourUsername>\Desktop\emotiv-lsl
```

Then install its required Python libraries if needed:

```bash
cd C:\Users\<YourUsername>\Desktop\emotiv-lsl
python -m venv .venv
.venv\Scripts\activate
pip install -r requirements.txt
```

## Launching Instructions

1. Clone the repository:

```bash
git clone https://github.com/Layan34/2025_GP_10.git
```

2. Open Unity Hub.

3. Select **Add project from disk**.

4. Open the Unity project from:

```text
2025_GP_10/Project_Files/Unity
```

5. Use the required Unity version:

```text
Unity 2022.3.x LTS
```

6. Open the main scene:

```text
Assets/Scenes/StartScene.unity
```

7. Press **Play** in Unity.

8. Enter a player name.

9. Start the game and choose one of the available game modes:

- Rassd
- Tayyar

10. After completing the session, open the dashboard to review the saved session results.

## Running With EEG

To run the full EEG-based features:

1. Open Emotiv Launcher.
2. Log in using an Emotiv account.
3. Connect the Emotiv EEG headset.
4. Make sure Cortex API access is available for the account and headset.
5. Make sure the headset is detected and streaming.
6. Make sure the `emotiv-lsl` folder is placed on the Desktop.
7. Run the Unity application.
8. The system will use LSL markers, Emotiv-LSL streaming, LabRecorder, and the Python runtime pipeline to support EEG-based gameplay and recording.

## Running Without EEG

The Unity application can still be opened for interface navigation and basic gameplay demonstration without the headset. However, the main EEG-based features, including real-time attention-related behavior, EEG streaming, and XDF recording, require the Emotiv headset, Emotiv Launcher, Cortex API access, and the required Python/LSL setup.

## Testing Information

Release 2 testing included:

- Starting the application from the welcome screen
- Entering a player name
- Running Rassd and Tayyar game sessions
- Testing pause and return-to-home navigation
- Testing adaptive difficulty behavior
- Testing LSL marker streaming
- Testing the EEG-related runtime pipeline
- Testing session result saving
- Testing CSV and JSON output generation
- Testing dashboard display after completed sessions

## Login / Credentials

No application login credentials are required.

The player only needs to enter a player name inside the application.

For EEG-based testing, an Emotiv account is required through Emotiv Launcher.

## GitHub Repository Link

```text
https://github.com/Layan34/2025_GP_10
```

## Authors

See `Authors.txt`.
