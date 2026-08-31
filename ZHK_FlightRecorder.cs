using System;
using SaccFlightAndVehicles;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class ZHK_FlightRecorder : UdonSharpBehaviour
{
    /*
    /   Zhakami Modules - Flight Recorder
    /   Records flight data for playback via ZHK_SAV_AI
    /
    /   Records:
    /   - Position
    /   - Rotation
    /   - Throttle State
    /   - Flaps State
    /   - Brakes State
    /
    /   Recording interval: Configurable (default 0.5 seconds)
    */

    [Header("Required References")]
    public SaccAirVehicle SAV;
    public DFUNC_Flaps DFUNC_FLAPS;
    public DFUNC_Brake DFUNC_BRAKE;
    public ZHK_AI_Brakes BRAKES;

    [Header("Recording Settings")]
    [Tooltip("Time interval between recordings (in seconds)")]
    public float recordingInterval = 0.5f;

    [Tooltip("Maximum number of recorded waypoints (increase for longer recordings)")]
    public int maxRecordedWaypoints = 1000;

    [Header("Recording Control")]
    public bool isRecording = false;

    [Header("Recorded Data - Do not modify manually")]
    // Arrays to store recorded data (UdonSharp doesn't support structs)
    // Position data
    public float[] recordedPosX;
    public float[] recordedPosY;
    public float[] recordedPosZ;

    // Rotation data (stored as Euler angles)
    public float[] recordedRotX;
    public float[] recordedRotY;
    public float[] recordedRotZ;

    // State data
    public float[] recordedThrottle;
    public bool[] recordedFlaps;
    public float[] recordedBrakes;

    // Recording metadata
    public int recordedWaypointCount = 0;
    public string recordingName = "Flight_Recording";

    // Private variables
    private float recordingTimer = 0f;
    private int currentRecordingIndex = 0;
    private Transform aircraftTransform;

    void Start()
    {
        // Initialize arrays
        InitializeArrays();

        if (SAV != null)
        {
            aircraftTransform = SAV.transform;
        }
    }

    void InitializeArrays()
    {
        recordedPosX = new float[maxRecordedWaypoints];
        recordedPosY = new float[maxRecordedWaypoints];
        recordedPosZ = new float[maxRecordedWaypoints];

        recordedRotX = new float[maxRecordedWaypoints];
        recordedRotY = new float[maxRecordedWaypoints];
        recordedRotZ = new float[maxRecordedWaypoints];

        recordedThrottle = new float[maxRecordedWaypoints];
        recordedFlaps = new bool[maxRecordedWaypoints];
        recordedBrakes = new float[maxRecordedWaypoints];
    }

    void Update()
    {
        if (!isRecording || SAV == null || aircraftTransform == null)
            return;

        // Only record if engine is on and someone is piloting
        if (!SAV.EngineOn || !SAV.Occupied)
            return;

        recordingTimer += Time.deltaTime;

        if (recordingTimer >= recordingInterval)
        {
            RecordCurrentState();
            recordingTimer = 0f;
        }
    }

    void RecordCurrentState()
    {
        // Check if we've reached the maximum capacity
        if (currentRecordingIndex >= maxRecordedWaypoints)
        {
            Debug.LogWarning("[ZHK_FlightRecorder] Maximum recording capacity reached. Stopping recording.");
            StopRecording();
            return;
        }

        // Record Position
        Vector3 pos = aircraftTransform.position;
        recordedPosX[currentRecordingIndex] = pos.x;
        recordedPosY[currentRecordingIndex] = pos.y;
        recordedPosZ[currentRecordingIndex] = pos.z;

        // Record Rotation (as Euler angles)
        Vector3 rot = aircraftTransform.eulerAngles;
        recordedRotX[currentRecordingIndex] = rot.x;
        recordedRotY[currentRecordingIndex] = rot.y;
        recordedRotZ[currentRecordingIndex] = rot.z;

        // Record Throttle
        recordedThrottle[currentRecordingIndex] = SAV.ThrottleInput;

        // Record Flaps
        recordedFlaps[currentRecordingIndex] = (DFUNC_FLAPS != null) ? DFUNC_FLAPS.Flaps : false;

        // Record Brakes
        float brakeValue = 0f;
        if (DFUNC_BRAKE != null)
        {
            brakeValue = DFUNC_BRAKE.BrakeInput;
        }
        else if (BRAKES != null)
        {
            brakeValue = BRAKES.AIBrakeInput;
        }
        recordedBrakes[currentRecordingIndex] = brakeValue;

        currentRecordingIndex++;
        recordedWaypointCount = currentRecordingIndex;

        Debug.Log($"[ZHK_FlightRecorder] Recorded waypoint {currentRecordingIndex}/{maxRecordedWaypoints}");
    }

    // Public methods for controlling recording
    public void StartRecording()
    {
        if (SAV == null)
        {
            Debug.LogError("[ZHK_FlightRecorder] SAV reference is missing!");
            return;
        }

        Debug.Log("[ZHK_FlightRecorder] Starting recording...");
        isRecording = true;
        currentRecordingIndex = 0;
        recordedWaypointCount = 0;
        recordingTimer = 0f;

        // Reinitialize arrays to clear old data
        InitializeArrays();
    }

    public void StopRecording()
    {
        Debug.Log($"[ZHK_FlightRecorder] Recording stopped. Total waypoints: {recordedWaypointCount}");
        isRecording = false;
    }

    public void ClearRecording()
    {
        Debug.Log("[ZHK_FlightRecorder] Clearing recorded data...");
        InitializeArrays();
        currentRecordingIndex = 0;
        recordedWaypointCount = 0;
        isRecording = false;
    }

    // Export recording data as JSON-formatted string
    public string ExportToJSON()
    {
        if (recordedWaypointCount == 0)
        {
            Debug.LogWarning("[ZHK_FlightRecorder] No data to export!");
            return "{}";
        }

        // Build JSON manually (since Unity's JsonUtility doesn't work well with UdonSharp)
        string json = "{\n";
        json += $"  \"recordingName\": \"{recordingName}\",\n";
        json += $"  \"waypointCount\": {recordedWaypointCount},\n";
        json += $"  \"recordingInterval\": {recordingInterval},\n";
        json += "  \"waypoints\": [\n";

        for (int i = 0; i < recordedWaypointCount; i++)
        {
            json += "    {\n";
            json += $"      \"position\": {{ \"x\": {recordedPosX[i]}, \"y\": {recordedPosY[i]}, \"z\": {recordedPosZ[i]} }},\n";
            json += $"      \"rotation\": {{ \"x\": {recordedRotX[i]}, \"y\": {recordedRotY[i]}, \"z\": {recordedRotZ[i]} }},\n";
            json += $"      \"throttle\": {recordedThrottle[i]},\n";
            json += $"      \"flaps\": {(recordedFlaps[i] ? "true" : "false")},\n";
            json += $"      \"brakes\": {recordedBrakes[i]}\n";
            json += "    }";

            if (i < recordedWaypointCount - 1)
                json += ",\n";
            else
                json += "\n";
        }

        json += "  ]\n";
        json += "}";

        Debug.Log("[ZHK_FlightRecorder] JSON export completed.");
        Debug.Log(json); // Print to console for copying

        return json;
    }

    // Get recorded data at specific index (for playback)
    public Vector3 GetRecordedPosition(int index)
    {
        if (index < 0 || index >= recordedWaypointCount)
            return Vector3.zero;

        return new Vector3(recordedPosX[index], recordedPosY[index], recordedPosZ[index]);
    }

    public Vector3 GetRecordedRotation(int index)
    {
        if (index < 0 || index >= recordedWaypointCount)
            return Vector3.zero;

        return new Vector3(recordedRotX[index], recordedRotY[index], recordedRotZ[index]);
    }

    public float GetRecordedThrottle(int index)
    {
        if (index < 0 || index >= recordedWaypointCount)
            return 0f;

        return recordedThrottle[index];
    }

    public bool GetRecordedFlaps(int index)
    {
        if (index < 0 || index >= recordedWaypointCount)
            return false;

        return recordedFlaps[index];
    }

    public float GetRecordedBrakes(int index)
    {
        if (index < 0 || index >= recordedWaypointCount)
            return 0f;

        return recordedBrakes[index];
    }
}
