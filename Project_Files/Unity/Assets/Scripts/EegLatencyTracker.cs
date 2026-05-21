using UnityEngine;
using System.IO;
using System;
using System.Collections.Generic;

public static class EegLatencyTracker
{
    public static float SignalTime = 0f; // Time when the EEG window becomes available.

    private static readonly List<float> readings = new List<float>(); // Stores latency readings for the report.
    private static bool reportSaved = false; // Stops logging after the report is saved.
    private const int TargetCount = 10; // Number of readings required for NFR testing.

    public static void LogLatency(float latency)
    {
        if (reportSaved)
            return; // Stop after collecting the required readings.

        readings.Add(latency);
        string pass = latency <= 2.0f ? "PASS" : "FAIL"; // NFR passes if response is within 2 seconds.

        Debug.Log($"[NFR1] Reading {readings.Count}/{TargetCount} | Latency: {latency:F4}s | {pass}");

        if (readings.Count >= TargetCount)
            SaveReport(); // Save once enough readings are collected.
    }

    private static void SaveReport()
    {
        reportSaved = true;

        float fastest = float.MaxValue;
        float slowest = float.MinValue;
        float sum     = 0f;
        int   passed  = 0;

        foreach (float v in readings)
        {
            if (v < fastest) fastest = v;
            if (v > slowest) slowest = v;
            sum += v;

            if (v <= 2.0f)
                passed++; // Count successful latency readings.
        }

        float average    = sum / readings.Count;
        string finalPass = passed == TargetCount ? "PASS" : "FAIL"; // All readings must pass.

        string folder = Path.Combine(
            Directory.GetParent(Application.dataPath).FullName,
            "NFR_Results");

        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder); // Create result folder if missing.

        string path = Path.Combine(folder, "NFR1_EEGLatency.csv");

        try
        {
            using (StreamWriter sw = new StreamWriter(path, append: false))
            {
                sw.WriteLine("Test#,Latency_Seconds,Result,Timestamp");

                for (int i = 0; i < readings.Count; i++)
                {
                    string r = readings[i] <= 2.0f ? "PASS" : "FAIL";
                    sw.WriteLine($"{i + 1},{readings[i]:F4},{r},{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                }

                sw.WriteLine("");
                sw.WriteLine("--- SUMMARY ---");
                sw.WriteLine($"Number of tests,{TargetCount}");
                sw.WriteLine($"Fastest response,{fastest:F4}s");
                sw.WriteLine($"Slowest response,{slowest:F4}s");
                sw.WriteLine($"Average response time,{average:F4}s");
                sw.WriteLine($"Tests passed,{passed}/{TargetCount}");
                sw.WriteLine($"Final Result,{finalPass}");
            }

            Debug.Log($"[NFR1] Report saved. Fastest={fastest:F4}s | Slowest={slowest:F4}s | Average={average:F4}s | {finalPass}");
        }
        catch (Exception e)
        {
            Debug.LogError("[NFR1] Could not write log file: " + e.Message);
        }
    }

    public static void Reset()
    {
        readings.Clear(); // Clear old latency readings.
        reportSaved = false;
        SignalTime  = 0f;
    }
}
