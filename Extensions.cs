using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Windows.Storage;
using Windows.Storage.Streams;

namespace Extractor;

public static class Extensions
{
    public static async Task<DateTimeOffset> GetItemDate(this IStorageItem file) => (await file.GetBasicPropertiesAsync()).ItemDate;
    public static async Task<DateTimeOffset> GetModifiedDate(this IStorageItem file) => (await file.GetBasicPropertiesAsync()).DateModified;
    public static async Task<ulong> GetSize(this IStorageItem file) => (await file.GetBasicPropertiesAsync()).Size;

    /// <summary>
    /// An updated string truncation helper.
    /// </summary>
    /// <remarks>
    /// This can be helpful when the CharacterEllipsis TextTrimming Property is not available.
    /// </remarks>
    public static string Truncate(this string text, int maxLength, string mesial = "…")
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        if (maxLength > 0 && text.Length > maxLength)
        {
            var limit = maxLength / 2;
            if (limit > 1)
            {
                return String.Format("{0}{1}{2}", text.Substring(0, limit).Trim(), mesial, text.Substring(text.Length - limit).Trim());
            }
            else
            {
                var tmp = text.Length <= maxLength ? text : text.Substring(0, maxLength).Trim();
                return String.Format("{0}{1}", tmp, mesial);
            }
        }
        return text;
    }

    /// <summary>
    /// Converts long file size into typical browser file size.
    /// </summary>
    public static string ToFileSize(this ulong size)
    {
        if (size < 1024) { return (size).ToString("F0") + " Bytes"; }
        if (size < Math.Pow(1024, 2)) { return (size / 1024).ToString("F0") + "KB"; }
        if (size < Math.Pow(1024, 3)) { return (size / Math.Pow(1024, 2)).ToString("F0") + "MB"; }
        if (size < Math.Pow(1024, 4)) { return (size / Math.Pow(1024, 3)).ToString("F0") + "GB"; }
        if (size < Math.Pow(1024, 5)) { return (size / Math.Pow(1024, 4)).ToString("F0") + "TB"; }
        if (size < Math.Pow(1024, 6)) { return (size / Math.Pow(1024, 5)).ToString("F0") + "PB"; }
        return (size / Math.Pow(1024, 6)).ToString("F0") + "EB";
    }

    /// <summary>
    /// uint max = 4,294,967,295 (4.29 Gbps)
    /// </summary>
    /// <returns>formatted bit-rate string</returns>
    public static string FormatBitrate(this uint amount)
    {
        var sizes = new string[]
        {
            "bps",
            "Kbps", // kilo
            "Mbps", // mega
            "Gbps", // giga
            "Tbps", // tera
        };
        var order = amount.OrderOfMagnitude();
        var speed = amount / Math.Pow(1000, order);
        return $"{speed:0.##} {sizes[order]}";
    }

    /// <summary>
    /// ulong max = 18,446,744,073,709,551,615 (18.45 Ebps)
    /// </summary>
    /// <returns>formatted bit-rate string</returns>
    public static string FormatBitrate(this ulong amount)
    {
        var sizes = new string[]
        {
            "bps",
            "Kbps", // kilo
            "Mbps", // mega
            "Gbps", // giga
            "Tbps", // tera
            "Pbps", // peta
            "Ebps", // exa
            "Zbps", // zetta
            "Ybps"  // yotta
        };
        var order = amount.OrderOfMagnitude();
        var speed = amount / Math.Pow(1000, order);
        return $"{speed:0.##} {sizes[order]}";
    }

    /// <summary>
    /// Returns the order of magnitude (10^3)
    /// </summary>
    public static int OrderOfMagnitude(this ulong amount) => (int)Math.Floor(Math.Log(amount, 1000));

    /// <summary>
    /// Returns the order of magnitude (10^3)
    /// </summary>
    public static int OrderOfMagnitude(this uint amount) => (int)Math.Floor(Math.Log(amount, 1000));

    /// <summary>
    /// Gets a stream to a specified file from the application local folder.
    /// </summary>
    /// <param name="fileName">Relative name of the file to open. Can contains subfolders.</param>
    /// <param name="accessMode">File access mode. Default is read.</param>
    /// <returns>The file stream</returns>
    public static Task<IRandomAccessStream> GetLocalFileStreamAsync(this string fileName, FileAccessMode accessMode = FileAccessMode.Read)
    {
        var workingFolder = StorageFolder.GetFolderFromPathAsync(AppContext.BaseDirectory).AsTask<StorageFolder>();
        return GetFileStreamAsync(fileName, accessMode, workingFolder.Result);
    }
    static async Task<IRandomAccessStream> GetFileStreamAsync(string fullFileName, FileAccessMode accessMode, StorageFolder workingFolder)
    {
        var fileName = Path.GetFileName(fullFileName);
        workingFolder = await GetSubFolderAsync(fullFileName, workingFolder);
        var file = await workingFolder.GetFileAsync(fileName);
        return await file.OpenAsync(accessMode);
    }
    static async Task<StorageFolder> GetSubFolderAsync(string fullFileName, StorageFolder workingFolder)
    {
        var folderName = Path.GetDirectoryName(fullFileName);
        if (!string.IsNullOrEmpty(folderName) && folderName != @"\")
        {
            return await workingFolder.GetFolderAsync(folderName);
        }
        return workingFolder;
    }

}

/// <summary>
///   A memory efficient version of the <see cref="System.Diagnostics.Stopwatch"/>.
///   Because this timer's function is passive, there's no need/way for a
///   stop method. A reset method would be equivalent to calling StartNew().
/// </summary>
/// <remarks>
///   Structs are value types. This means they directly hold their data, 
///   unlike reference types (e.g. classes) that hold references to objects.
///   Value types cannot be null, they'll always have a value, even if it's 
///   the default value for their member data type(s). While you can't assign 
///   null directly to a struct, you can have struct members that are reference 
///   types (e.g. String), and those members can be null.
/// </remarks>
internal struct ValueStopwatch
{
    long _startTimestamp;
    // Set the ratio of timespan ticks to stopwatch ticks.
    static readonly double TimestampToTicks = TimeSpan.TicksPerSecond / (double)System.Diagnostics.Stopwatch.Frequency;
    public bool IsActive => _startTimestamp != 0;
    private ValueStopwatch(long startTimestamp) => _startTimestamp = startTimestamp;
    public static ValueStopwatch StartNew() => new ValueStopwatch(System.Diagnostics.Stopwatch.GetTimestamp());
    public TimeSpan GetElapsedTime()
    {
        // _startTimestamp cannot be zero for an initialized ValueStopwatch.
        if (!IsActive)
            throw new InvalidOperationException($"ValueStopwatch is uninitialized. Initialize the ValueStopwatch before using.");

        long end = System.Diagnostics.Stopwatch.GetTimestamp();
        long timestampDelta = end - _startTimestamp;
        long ticks = (long)(TimestampToTicks * timestampDelta);
        return new TimeSpan(ticks);
    }

    public string GetElapsedFriendly()
    {
        return ToHumanFriendly(GetElapsedTime());
    }

    #region [Helpers]
    string ToHumanFriendly(TimeSpan timeSpan)
    {
        if (timeSpan == TimeSpan.Zero)
            return "0 seconds";

        bool isNegative = false;
        List<string> parts = new();

        // Check for negative TimeSpan.
        if (timeSpan < TimeSpan.Zero)
        {
            isNegative = true;
            timeSpan = timeSpan.Negate(); // Make it positive for the calculations.
        }

        if (timeSpan.Days > 0)
            parts.Add($"{timeSpan.Days} day{(timeSpan.Days > 1 ? "s" : "")}");
        if (timeSpan.Hours > 0)
            parts.Add($"{timeSpan.Hours} hour{(timeSpan.Hours > 1 ? "s" : "")}");
        if (timeSpan.Minutes > 0)
            parts.Add($"{timeSpan.Minutes} minute{(timeSpan.Minutes > 1 ? "s" : "")}");
        if (timeSpan.Seconds > 0)
            parts.Add($"{timeSpan.Seconds} second{(timeSpan.Seconds > 1 ? "s" : "")}");

        // If no large amounts so far, try milliseconds.
        if (parts.Count == 0 && timeSpan.Milliseconds > 0)
            parts.Add($"{timeSpan.Milliseconds} millisecond{(timeSpan.Milliseconds > 1 ? "s" : "")}");

        // If no milliseconds, use ticks (nanoseconds).
        if (parts.Count == 0 && timeSpan.Ticks > 0)
        {
            // A tick is equal to 100 nanoseconds. While this maps well into units of time
            // such as hours and days, any periods longer than that aren't representable in
            // a succinct fashion, e.g. a month can be between 28 and 31 days, while a year
            // can contain 365 or 366 days. A decade can have between 1 and 3 leap-years,
            // depending on when you map the TimeSpan into the calendar. This is why TimeSpan
            // does not provide a "Years" property or a "Months" property.
            // Internally TimeSpan uses long (Int64) for its values, so:
            //  - TimeSpan.MaxValue = long.MaxValue
            //  - TimeSpan.MinValue = long.MinValue
            parts.Add($"{(timeSpan.Ticks * TimeSpan.TicksPerMicrosecond)} microsecond{((timeSpan.Ticks * TimeSpan.TicksPerMicrosecond) > 1 ? "s" : "")}");
        }

        // Join the sections with commas and "and" for the last one.
        if (parts.Count == 1)
            return isNegative ? $"Negative {parts[0]}" : parts[0];
        else if (parts.Count == 2)
            return isNegative ? $"Negative {string.Join(" and ", parts)}" : string.Join(" and ", parts);
        else
        {
            string lastPart = parts[parts.Count - 1];
            parts.RemoveAt(parts.Count - 1);
            return isNegative ? $"Negative " + string.Join(", ", parts) + " and " + lastPart : string.Join(", ", parts) + " and " + lastPart;
        }
    }
    #endregion
}

