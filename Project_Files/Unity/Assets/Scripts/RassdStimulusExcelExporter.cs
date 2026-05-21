using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

[Serializable]
public class RassdStimulusRecord
{
    public int trialNumber; // Trial number inside the session.
    public string sessionId; // Unique ID for this game session.
    public string playerName; // Player name saved with the report.
    public string timestamp;
    public string difficultyZone; // Difficulty level during this trial.
    public float gameTimeSeconds;

    public string stimulusType; // Coffee or scorpion.
    public string position;
    public bool isTarget; // True when the correct action is to press.

    public bool pressed;
    public bool correct;
    public string responseType; // Hit, Miss, FalseAlarm, etc.

    public float targetReactionTimeMs = -1f;
    public float falseAlarmReactionTimeMs = -1f;

    public float trialDurationSec;
    public float stimDurationSec;
    public int scoreAfterTrial;
    public int hitsAfterTrial;
    public int missesAfterTrial;
    public int falseAlarmsAfterTrial;
    public int correctRejectionsAfterTrial;

    public int eegWindowIndex = -1; // Matching EEG window for this trial.
    public string eegZone = "";
    public float eegScore = 0f;
    public float eegGeneralScore = 0f;
    public float eegConfidence = 0f;
    public bool eegIsFocused = false;

    public float thetaAvg = 0f;
    public float alphaAvg = 0f;
    public float betaAvg = 0f;
    public float tbrMean = 0f;
    public float barMean = 0f;

    public float tbrMedian = 0f;
    public float tbrStd = 0f;
    public float tbrAF3 = 0f;
    public float tbrF7 = 0f;
    public float tbrF3 = 0f;
    public float tbrFC5 = 0f;
    public float tbrT7 = 0f;
    public float tbrP7 = 0f;
    public float tbrO1 = 0f;
    public float tbrO2 = 0f;
    public float tbrP8 = 0f;
    public float tbrT8 = 0f;
    public float tbrFC6 = 0f;
    public float tbrF4 = 0f;
    public float tbrF8 = 0f;
    public float tbrAF4 = 0f;
}

public static class RassdStimulusExcelExporter
{
    public static string Export(
        string playerName,
        string sessionId,
        int score,
        int hits,
        int misses,
        int falseAlarms,
        int correctRejections,
        float averageTargetReactionTimeMs,
        float targetRtvMs,
        List<RassdStimulusRecord> records)
    {
        if (records == null)
            records = new List<RassdStimulusRecord>();

        string folder = Path.Combine(Application.persistentDataPath, "RassdPerformance"); // Folder for Rassd CSV reports.
        Directory.CreateDirectory(folder); // Create folder if it does not exist.

        string safePlayerName = MakeSafeFileName(
            string.IsNullOrWhiteSpace(playerName) ? "Player" : playerName.Trim()
        );

        string fileName = $"Rassd_StimulusPerformance_{safePlayerName}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
        string path = Path.Combine(folder, fileName);

        int totalTargets = hits + misses;
        int totalNonTargets = falseAlarms + correctRejections;

        float hitRate = totalTargets > 0
            ? (float)hits / totalTargets
            : 0f;

        float falseAlarmRate = totalNonTargets > 0
            ? (float)falseAlarms / totalNonTargets
            : 0f;

        float dPrime = CalculateDPrime(
            hitRate,
            totalTargets,
            falseAlarmRate,
            totalNonTargets
        );

        var sb = new StringBuilder();
        sb.AppendLine("Rassd Stimulus Performance Report");
        sb.AppendLine("Player Name," + Csv(playerName));
        sb.AppendLine("Session ID," + Csv(sessionId));
        sb.AppendLine("Export Time," + Csv(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));
        sb.AppendLine("Score," + score.ToString(CultureInfo.InvariantCulture));
        sb.AppendLine("Correct Responses," + hits.ToString(CultureInfo.InvariantCulture));
        sb.AppendLine("Omissions," + misses.ToString(CultureInfo.InvariantCulture));
        sb.AppendLine("Commissions," + falseAlarms.ToString(CultureInfo.InvariantCulture));
        sb.AppendLine("Correct Rejections," + correctRejections.ToString(CultureInfo.InvariantCulture));
        sb.AppendLine("d_prime," + dPrime.ToString("F4", CultureInfo.InvariantCulture));
        sb.AppendLine("Average Reaction Time (ms)," + averageTargetReactionTimeMs.ToString("F2", CultureInfo.InvariantCulture));
        sb.AppendLine("Reaction Time Variability (ms)," + targetRtvMs.ToString("F2", CultureInfo.InvariantCulture));
        sb.AppendLine();

        sb.AppendLine(
            "Trial,Timestamp,Difficulty,Stimulus,Response Type,Reaction Time (ms),EEG Window Index,Prediction," +
            "Theta Avg,Alpha Avg,Beta Avg,TBR Mean,BAR Mean,TBR Median,TBR Std," +
            "TBR AF3,TBR F7,TBR F3,TBR FC5,TBR T7,TBR P7,TBR O1,TBR O2,TBR P8,TBR T8,TBR FC6,TBR F4,TBR F8,TBR AF4"
        );

        foreach (var r in records)
        {
            string stimulusLabel = r.isTarget ? "Target" : "Non Target";

            string reactionTimeText = "";

            if (r.targetReactionTimeMs >= 0f)
            {
                reactionTimeText = r.targetReactionTimeMs.ToString("F2", CultureInfo.InvariantCulture);
            }
            else if (r.falseAlarmReactionTimeMs >= 0f)
            {
                reactionTimeText = r.falseAlarmReactionTimeMs.ToString("F2", CultureInfo.InvariantCulture);
            }

            string finalResponseType = ConvertResponseTypeName(r.responseType);

            sb.AppendLine(string.Join(",", new string[]
            {
                r.trialNumber.ToString(CultureInfo.InvariantCulture),
                Csv(r.timestamp),
                Csv(r.difficultyZone),
                Csv(stimulusLabel),
                Csv(finalResponseType),
                reactionTimeText,

                r.eegWindowIndex.ToString(CultureInfo.InvariantCulture),

                r.eegIsFocused ? "1" : "0",

                r.thetaAvg.ToString("F4", CultureInfo.InvariantCulture),
                r.alphaAvg.ToString("F4", CultureInfo.InvariantCulture),
                r.betaAvg.ToString("F4", CultureInfo.InvariantCulture),
                r.tbrMean.ToString("F4", CultureInfo.InvariantCulture),
                r.barMean.ToString("F4", CultureInfo.InvariantCulture),
                r.tbrMedian.ToString("F4", CultureInfo.InvariantCulture),
                r.tbrStd.ToString("F4", CultureInfo.InvariantCulture),

                r.tbrAF3.ToString("F4", CultureInfo.InvariantCulture),
                r.tbrF7.ToString("F4", CultureInfo.InvariantCulture),
                r.tbrF3.ToString("F4", CultureInfo.InvariantCulture),
                r.tbrFC5.ToString("F4", CultureInfo.InvariantCulture),
                r.tbrT7.ToString("F4", CultureInfo.InvariantCulture),
                r.tbrP7.ToString("F4", CultureInfo.InvariantCulture),
                r.tbrO1.ToString("F4", CultureInfo.InvariantCulture),
                r.tbrO2.ToString("F4", CultureInfo.InvariantCulture),
                r.tbrP8.ToString("F4", CultureInfo.InvariantCulture),
                r.tbrT8.ToString("F4", CultureInfo.InvariantCulture),
                r.tbrFC6.ToString("F4", CultureInfo.InvariantCulture),
                r.tbrF4.ToString("F4", CultureInfo.InvariantCulture),
                r.tbrF8.ToString("F4", CultureInfo.InvariantCulture),
                r.tbrAF4.ToString("F4", CultureInfo.InvariantCulture)
            }));
        }

        File.WriteAllText(path, "\uFEFF" + sb.ToString(), Encoding.UTF8); // Save CSV with UTF-8 BOM for Excel.

        Debug.Log("[RassdStimulusExcelExporter] Exported cleaned single CSV: " + path);

        return path;
    }

    private static string ConvertResponseTypeName(string responseType)
    {
        if (string.IsNullOrWhiteSpace(responseType))
            return "";

        switch (responseType.Trim())
        {
            case "Hit":
                return "Correct Response";

            case "Miss":
                return "Omission";

            case "FalseAlarm":
            case "False Alarm":
                return "Commission";

            case "CorrectRejection":
            case "Correct Rejection":
                return "Correct Rejection";

            case "InvalidRT":
            case "Invalid RT":
                return "Invalid Response";

            case "Pending":
                return "Pending";

            default:
                return responseType;
        }
    }

    private static float CalculateDPrime(
        float hitRate,
        int totalTargets,
        float falseAlarmRate,
        int totalNonTargets)
    {
        if (totalTargets <= 0 || totalNonTargets <= 0)
            return 0f;

        float adjustedHitRate = AdjustRateForZ(hitRate, totalTargets);
        float adjustedFalseAlarmRate = AdjustRateForZ(falseAlarmRate, totalNonTargets);

        float zHit = InverseNormalCdf(adjustedHitRate);
        float zFa = InverseNormalCdf(adjustedFalseAlarmRate);

        return zHit - zFa;
    }

    private static float AdjustRateForZ(float rate, int n)
    {
        if (n <= 0)
            return 0.5f;

        // Avoid infinite Z values when rate is exactly 0 or 1.
        if (rate <= 0f)
            return 0.5f / n;

        if (rate >= 1f)
            return 1f - (0.5f / n);

        return rate;
    }

    private static float InverseNormalCdf(float p)
    {
        if (p <= 0f || p >= 1f)
            throw new ArgumentOutOfRangeException(nameof(p), "p must be between 0 and 1.");

        // Peter J. Acklam approximation
        float[] a =
        {
            -3.969683028665376e+01f,
             2.209460984245205e+02f,
            -2.759285104469687e+02f,
             1.383577518672690e+02f,
            -3.066479806614716e+01f,
             2.506628277459239e+00f
        };

        float[] b =
        {
            -5.447609879822406e+01f,
             1.615858368580409e+02f,
            -1.556989798598866e+02f,
             6.680131188771972e+01f,
            -1.328068155288572e+01f
        };

        float[] c =
        {
            -7.784894002430293e-03f,
            -3.223964580411365e-01f,
            -2.400758277161838e+00f,
            -2.549732539343734e+00f,
             4.374664141464968e+00f,
             2.938163982698783e+00f
        };

        float[] d =
        {
             7.784695709041462e-03f,
             3.224671290700398e-01f,
             2.445134137142996e+00f,
             3.754408661907416e+00f
        };

        float plow = 0.02425f;
        float phigh = 1f - plow;
        float q;
        float r;

        if (p < plow)
        {
            q = Mathf.Sqrt(-2f * Mathf.Log(p));

            return (((((c[0] * q + c[1]) * q + c[2]) * q + c[3]) * q + c[4]) * q + c[5]) /
                   ((((d[0] * q + d[1]) * q + d[2]) * q + d[3]) * q + 1f);
        }

        if (p > phigh)
        {
            q = Mathf.Sqrt(-2f * Mathf.Log(1f - p));

            return -(((((c[0] * q + c[1]) * q + c[2]) * q + c[3]) * q + c[4]) * q + c[5]) /
                    ((((d[0] * q + d[1]) * q + d[2]) * q + d[3]) * q + 1f);
        }

        q = p - 0.5f;
        r = q * q;

        return (((((a[0] * r + a[1]) * r + a[2]) * r + a[3]) * r + a[4]) * r + a[5]) * q /
               (((((b[0] * r + b[1]) * r + b[2]) * r + b[3]) * r + b[4]) * r + 1f);
    }

    private static string Csv(string value)
    {
        if (value == null)
            value = "";

        value = value.Replace("\"", "\"\"");
        return "\"" + value + "\"";
    }

    private static string MakeSafeFileName(string value)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            value = value.Replace(c, '_');

        return value;
    }
}