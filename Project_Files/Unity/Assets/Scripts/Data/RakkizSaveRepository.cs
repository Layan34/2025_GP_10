using System.IO;
using System.Collections.Generic;
using UnityEngine;

public sealed class RakkizSaveRepository
{
    private readonly string _fileName; // JSON file name used for saved player data.

    public RakkizSaveRepository(string fileName)
    {
        _fileName = fileName;
    }

    public bool TryLoad(out RakkizSaveData data)
    {
        data = null; // Start empty until loading succeeds.

        string persistentPath = GetPersistentPath(); // Main writable save location.
        Debug.Log($"SAVE PATH: {persistentPath}");

        if (File.Exists(persistentPath)) // Prefer the saved file created during gameplay.
        {
            string json = File.ReadAllText(persistentPath);
            return TryParseJson(json, out data);
        }

        string streamingPath = GetStreamingAssetsPath(); // Fallback default file inside the project.
        if (File.Exists(streamingPath)) // Copy default save data on first launch.
        {
            string defaultJson = File.ReadAllText(streamingPath);

            string dir = Path.GetDirectoryName(persistentPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir); // Create the folder before writing the JSON file.

            File.WriteAllText(persistentPath, defaultJson);
            return TryParseJson(defaultJson, out data);
        }

        data = CreateNew(); // Create a clean save file if no file exists.
        Save(data);
        return true;
    }

    public void Save(RakkizSaveData data)
    {
        string path = GetPersistentPath();

        string dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir); // Create the folder before writing the JSON file.

        string json = JsonUtility.ToJson(data, true); // Convert the save object to readable JSON.
        File.WriteAllText(path, json);
    }

    public RakkizSaveData LoadOrCreate()
    {
        if (TryLoad(out var data) && data != null)
            return data;

        data = CreateNew(); // Create a clean save file if no file exists.
        Save(data);
        return data;
    }

    public void SetPlayerName(string playerName)
    {
        var data = LoadOrCreate();

        string trimmed = playerName == null ? string.Empty : playerName.Trim(); // Remove empty spaces from the name.

        data.player.playerName = trimmed;
        data.hasPlayerName = !string.IsNullOrWhiteSpace(trimmed);
        Save(data);
    }

    public string GetPlayerNameOrDefault(string fallback = "Player")
    {
        var data = LoadOrCreate();
        if (data.player == null || string.IsNullOrWhiteSpace(data.player.playerName))
            return fallback;

        return data.player.playerName;
    }

    public bool HasPlayerName()
    {
        var data = LoadOrCreate();
        return data.hasPlayerName
               && data.player != null
               && !string.IsNullOrWhiteSpace(data.player.playerName);
    }

    public void AppendSession(SessionData session)
    {
        if (session == null)
            return;

        var data = LoadOrCreate();
        data.sessions ??= new List<SessionData>(); // Make sure the session list exists before adding.

        session.behavioralMetrics ??= new BehavioralMetrics(); // Avoid null errors when saving metrics.
        data.sessions.Add(session);

        Save(data);
    }

    public void AppendSessionToLastPlayer(SessionData session)
    {
        AppendSession(session);
    }

    public bool HasSeenTutorial()
    {
        var data = LoadOrCreate();
        return data.hasSeenTutorial;
    }

    public void MarkTutorialSeen()
    {
        var data = LoadOrCreate();
        data.hasSeenTutorial = true; // Prevent showing the tutorial again as first-time content.
        Save(data);
    }

    private bool TryParseJson(string json, out RakkizSaveData data)
    {
        data = null; // Start empty until loading succeeds.

        if (string.IsNullOrWhiteSpace(json)) // Empty JSON cannot be loaded.
            return false;

        Debug.Log($"SAVE JSON:\n{json}");

        data = JsonUtility.FromJson<RakkizSaveData>(json);
        if (data == null)
            return false;

        data.player ??= new PlayerData { playerName = string.Empty }; // Repair old save files missing player data.
        data.sessions ??= new List<SessionData>(); // Make sure the session list exists before adding.

        for (int i = 0; i < data.sessions.Count; i++)
        {
            data.sessions[i] ??= new SessionData(); // Repair missing session entries.
            data.sessions[i].behavioralMetrics ??= new BehavioralMetrics();
        }

        if (string.IsNullOrWhiteSpace(data.player.playerName))
            data.hasPlayerName = false;

        return true;
    }

    private RakkizSaveData CreateNew()
    {
        return new RakkizSaveData
        {
            player = new PlayerData { playerName = string.Empty },
            sessions = new List<SessionData>(),
            hasPlayerName = false,
            hasSeenTutorial = false
        };
    }

    private string GetPersistentPath()
    {
        return Path.Combine(Application.persistentDataPath, _fileName);
    }

    private string GetStreamingAssetsPath()
    {
        return Path.Combine(Application.streamingAssetsPath, _fileName);
    }
}
