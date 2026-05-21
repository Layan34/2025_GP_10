using UnityEngine;
using System.IO;
using System;


public static class NFRLoadTimeLogger
{
    private static readonly string FileName = "NFR3_LoadTimes.csv";

    private static string GetFilePath()
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string folder      = Path.Combine(projectRoot, "NFR_Results");

        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);

        return Path.Combine(folder, FileName);
    }

 
    public static void LogTransition(string label, float loadTimeSeconds)
    {
        string path   = GetFilePath();
        bool   isNew  = !File.Exists(path);

        string pass   = loadTimeSeconds <= 1.0f ? "PASS" : "FAIL";
        string time   = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        string row    = $"{time},{label},{loadTimeSeconds:F4},{pass}";

        try
        {
            using (StreamWriter sw = new StreamWriter(path, append: true))
            {
                if (isNew)
                    sw.WriteLine("Timestamp,Transition,LoadTime_Seconds,Result");

                sw.WriteLine(row);
            }

            Debug.Log($"[NFR3] {label} | {loadTimeSeconds:F4}s | {pass}  →  saved to NFR_Results/{FileName}");
        }
        catch (Exception e)
        {
            Debug.LogError("[NFR3] Could not write log file: " + e.Message);
        }
    }
}