using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;


namespace Extractor;

public class Program
{
    const string title = "Extractor";
    static int truncateLength = 72;
    static int counterExtracts = 0;
    static int counterWarnings = 0;
    static int counterDuplicates = 0;
    static CancellationTokenSource cts = new();
    static ConcurrentDictionary<ulong, string> thumbCache = new();
    static ValueStopwatch watch { get; set; }

    static void Main(string[] args)
    {
        Console.Title = title;
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        #region [Event Handling]
        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            Console.Title = $"{title} - ERROR";
            Console.CursorVisible = true;
            Console.WriteLine($"{Environment.NewLine} ⚠️ UNHANDLED EXCEPTION ⚠️ {Environment.NewLine}");
            Console.WriteLine($" 📣 {(e.ExceptionObject as Exception)?.Message}{Environment.NewLine}");
            Environment.Exit(0);
        };
        Console.CancelKeyPress += (sender, e) =>
        {
            Console.Title = $"{title} - QUIT";
            Console.CursorVisible = true;
            Console.WriteLine($"{Environment.NewLine} ⚠️ USER EXIT ⚠️ {Environment.NewLine}");
            Environment.Exit(0);
        };
        #endregion

        if (args.Length < 1)
        {
            Console.WriteLine($" ⚠️ Insufficient arguments ⚠️ {Environment.NewLine}");
            Console.WriteLine($" Usage:");
            Console.WriteLine($"    {title} <fully-qualified-folder-path>");
            Console.WriteLine($" Example:");
            Console.WriteLine($"    {title} C:\\Windows\\Temp");
        }
        else
        {
            string folderPath = args[0];
            watch = ValueStopwatch.StartNew();
            Console.Title = $"{title} - SEARCHING";
            Console.CursorVisible = false;
            CancellationTokenRegistration ctr = cts.Token.Register(() => 
            { 
                Console.WriteLine($" 📢 Token registration invoked! {Environment.NewLine}"); 
            });
            var waiter = RecurseFolders(folderPath, cts.Token).GetAwaiter();
            waiter.OnCompleted(() =>
            {
                Console.WriteLine($"{Environment.NewLine}");
                //if (tsk.IsFaulted)
                //    Console.WriteLine($" ⚠️     Task Error : {tsk.Exception?.GetBaseException().Message}");
                Console.WriteLine($" 📝    Extractions : {counterExtracts}");
                Console.WriteLine($" 📝  File Warnings : {counterWarnings}");
                Console.WriteLine($" 📝     Duplicates : {counterDuplicates}");
                Console.WriteLine($" ⏱️   Elapsed Time : {watch.GetElapsedFriendly()}");
                Console.WriteLine();
                Console.CursorVisible = true;
                Console.Title = $"{title} - FINISHED";

                // If the registration was never used we'll need to let go so it doesn't hang
                // around in memory forever. This shouldn't matter since the application will
                // be closing shortly, and the OS should clean up this handle for us.
                ctr.Dispose();
            });
            while (!waiter.IsCompleted)
            {
                var key = Console.ReadKey(true).Key;
                if (key == ConsoleKey.Escape)
                {
                    if (waiter.IsCompleted)
                        Console.WriteLine($"{Environment.NewLine} 🔔 Closing… {Environment.NewLine}");
                    else
                        Console.WriteLine($"{Environment.NewLine} 🔔 Cancellation requested… {Environment.NewLine}");
                    cts.Cancel();
                    break;
                }
                else if (!waiter.IsCompleted)
                {
                    Console.WriteLine($"{Environment.NewLine} 🔔 Process still active, press <Esc> if you wish to cancel {Environment.NewLine}");
                }
                else if (waiter.IsCompleted)
                {
                    Console.WriteLine($"{Environment.NewLine} 🔔 Closing… {Environment.NewLine}");
                }
            }
        }

        //Console.WriteLine($"{Environment.NewLine}–– press a key to exit ––");
        //_ = Console.ReadKey(true).Key;
    }

    static async Task<bool> RecurseFolders(string folderPath, CancellationToken token)
    {
        bool success = true;
        try
        {
            StorageFolder folder = await StorageFolder.GetFolderFromPathAsync(folderPath);

            if (folder != null)
            {
                Console.WriteLine($"🔎 Parsing folder '{folder.Name}'");
                try
                {
                    // Get the files in this folder.
                    IReadOnlyList<StorageFile> files = await folder.GetFilesAsync();
                    foreach (StorageFile file in files)
                    {
                        if (token.IsCancellationRequested)
                        {
                            Console.WriteLine($"🔔 Cancellation requested. Exiting directory search.");
                            return false;
                        }

                        try
                        {
                            Console.WriteLine($"🔎 Analyzing '{file.Path.Truncate(truncateLength)}'   ");
                            SaveThumbnailAsPngAsync(file, $"{file.Name.Replace(file.FileType, ".png")}").Wait();
                        }
                        catch (Exception) { }
                    }

                    // Recurse sub-directories.
                    IReadOnlyList<StorageFolder> subDirs = await folder.GetFoldersAsync();
                    if (subDirs.Count != 0)
                    {
                        GetDirectories(subDirs, token);
                    }
                }
                catch (Exception ex)
                {
                    success = false;
                    Console.WriteLine($"⚠️ {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            success = false;
            Console.WriteLine($"⚠️ RecurseFolders: {ex.Message}");
        }
        return success;
    }

    static async void GetDirectories(IReadOnlyList<StorageFolder> folders, CancellationToken token)
    {
        foreach (StorageFolder folder in folders)
        {
            if (token.IsCancellationRequested)
            {
                Console.WriteLine($"🔔 Cancellation requested. Exiting directory search.");
                break;
            }

            try
            {
                // Get the files in this folder.
                IReadOnlyList<StorageFile> files = await folder.GetFilesAsync();
                foreach (StorageFile file in files)
                {
                    if (token.IsCancellationRequested)
                    {
                        Console.WriteLine($"🔔 Cancellation requested. Exiting directory search.");
                        break;
                    }

                    try
                    {
                        Console.WriteLine($"🔎 Analyzing '{file.Path.Truncate(truncateLength)}'   ");
                        SaveThumbnailAsPngAsync(file, $"{file.Name.Replace(file.FileType, ".png")}").Wait();
                    }
                    catch (Exception) { }
                }

                // Recurse this folder to get sub-folder info.
                IReadOnlyList<StorageFolder> subDirs = await folder.GetFoldersAsync();
                if (subDirs.Count != 0)
                {
                    GetDirectories(subDirs, token);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ GetDirectories: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Gets the thumbnail associated with the <paramref name="thumbnailFilePath"/> and saves it as 
    /// a PNG file with the name <paramref name="outputName"/> in the app's local execution folder.
    /// </summary>
    public static async Task SaveThumbnailAsPngAsync(StorageFile file, string outputName)
    {
        //var file = await StorageFile.GetFileFromPathAsync(givenStringFilePath);

        // Retrieve the thumbnail image for file
        var thumbnail = await file.GetThumbnailAsync(ThumbnailMode.SingleItem);

        // Make sure the file has a thumbnail associated with it.
        if (thumbnail != null)
        {
            try
            {
                Console.WriteLine($"   ✅ Thumbnail size will be {thumbnail.OriginalWidth}x{thumbnail.OriginalHeight}");

                #region [Check for duplicate]
                var key = thumbnail.Size + (ulong)thumbnail.OriginalWidth + (ulong)thumbnail.OriginalHeight;
                if (thumbCache.ContainsKey(key)) // Skip if we've already created this thumbnail.
                {
                    counterDuplicates++;
                    return;
                }
                else
                    thumbCache[key] = outputName;
                #endregion

                #region [Create a decoder for the thumbnail]
                //var stream = RandomAccessStreamReference.CreateFromFile(file);
                //var streamWithContent = await stream.OpenReadAsync();
                var st = thumbnail.AsStreamForRead();
                var decoder = await BitmapDecoder.CreateAsync(st.AsRandomAccessStream());
                var softwareBitmap = await decoder.GetSoftwareBitmapAsync();

                //StorageFile.CreateStreamedFileAsync("thumbnail.png", async (request) =>
                //{
                //    using (var stream = request.AsStreamForWrite())
                //    {
                //        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
                //        encoder.SetSoftwareBitmap(softwareBitmap);
                //        await encoder.FlushAsync();
                //    }
                //}, null);
                #endregion

                try { Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "Thumbnails")); }
                catch (Exception) { }

                #region [Create temp file to save thumbnail]
                StorageFolder folder = await StorageFolder.GetFolderFromPathAsync(Path.Combine(AppContext.BaseDirectory, "Thumbnails")); // don't use "Windows.Storage.ApplicationData.Current.LocalFolder";
                using (var str = await folder.OpenStreamForWriteAsync(outputName, CreationCollisionOption.ReplaceExisting))
                {
                    str.Write(new byte[0], 0, 0);
                }
                StorageFile saveFile = await folder.GetFileAsync(outputName);
                #endregion

                #region [Save the new PNG file]
                //var saveFile = await StorageFile.GetFileFromPathAsync("D:\\ThumbnailOutput.png");
                using (var saveStream = await saveFile.OpenAsync(FileAccessMode.ReadWrite))
                {
                    var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, saveStream);
                    encoder.SetSoftwareBitmap(softwareBitmap);
                    await encoder.FlushAsync();
                }
                #endregion

                await Task.Delay(10);
                counterExtracts++;
            }
            catch (COMException ex) when (ex.ErrorCode == -2003292336)
            {
                Debug.WriteLine($"[WARNING] The error likely occurred during the creation of the BitmapDecoder or the retrieval of the stream.");
                Debug.WriteLine($"[WARNING] The COMException suggests that the file or thumbnail might not be in the expected format, or may be corrupted.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] SaveThumbnailAsPngAsync: {ex.Message}");
            }
        }
        else
        {
            Console.WriteLine($" 🔔 File has no thumbnail associated '{file.Name}'");
        }
    }

    /// <summary>
    /// A general file parsing function.
    /// </summary>
    /// <param name="file">the <see cref="StorageFile"/> to search</param>
    /// <param name="searchPattern">"microsoft azure"</param>
    public static async void SearchFile(StorageFile file, string searchPattern)
    {
        if (file != null)
        {
            try
            {
                // Check the MIME type of the file.
                if (!string.IsNullOrEmpty(file.ContentType) && 
                    file.ContentType.StartsWith("image/") || 
                    file.ContentType.StartsWith("audio/") ||
                    file.ContentType.StartsWith("video/") ||
                    file.ContentType.StartsWith("application/"))
                {
                    Debug.WriteLine($"🔔 Skipping unsearchable file '{file.Name}' with content type '{file.ContentType}'");
                    //await SaveThumbnailAsPngAsync($"{file.Path}", $"{file.Name.Replace(file.FileType, ".png")}");
                    return;

                    /*
                    [Text Files]
                    ------------------------------------------
                    text/plain - Plain text files (e.g., .txt)
                    text/html - HTML files (e.g., .html, .htm)
                    text/xml - XML files (e.g., .xml)
                    text/css - Cascading Style Sheets (e.g., .css)
                    text/javascript - JavaScript files (e.g., .js)

                    [Image Files]
                    ------------------------------------------
                    image/jpeg - JPEG images (e.g., .jpg, .jpeg)
                    image/png - PNG images (e.g., .png)
                    image/gif - GIF images (e.g., .gif)
                    image/bmp - Bitmap images (e.g., .bmp)
                    image/svg+xml - SVG images (e.g., .svg)

                    [Audio Files]
                    ------------------------------------------
                    audio/mpeg - MP3 audio files (e.g., .mp3)
                    audio/wav - WAV audio files (e.g., .wav)
                    audio/ogg - OGG audio files (e.g., .ogg)

                    [Video Files]
                    ------------------------------------------
                    video/mp4 - MP4 video files (e.g., .mp4)
                    video/x-msvideo - AVI video files (e.g., .avi)
                    video/x-matroska - MKV video files (e.g., .mkv)

                    [Document Files]
                    ------------------------------------------
                    application/pdf - PDF files (e.g., .pdf)
                    application/msword - Microsoft Word documents (e.g., .doc, .docx)
                    application/vnd.openxmlformats-officedocument.wordprocessingml.document - Word documents (e.g., .docx)
                    application/vnd.ms-excel - Microsoft Excel files (e.g., .xls, .xlsx)
                    application/vnd.openxmlformats-officedocument.spreadsheetml.sheet - Excel files (e.g., .xlsx)

                    [Compressed Files]
                    ------------------------------------------
                    application/zip - ZIP files (e.g., .zip)
                    application/x-rar-compressed - RAR files (e.g., .rar)
                    */
                }
                else if(!string.IsNullOrEmpty(file.ContentType))
                {
                    //Debug.WriteLine($"[INFO] Content type is '{file.ContentType}' with folder ID '{file.FolderRelativeId}'");
                    //BasicProperties? props = file.ContentType.Equals("text/plain") ? await file.GetBasicPropertiesAsync() : null;
                }

                BasicProperties? props = await file.GetBasicPropertiesAsync();

                if (props != null && props.Size == 0)
                {
                    Debug.WriteLine($"🔔 Skipping empty file '{file.Name}'");
                    return;
                }
                else if (props != null && props.Size > 0)
                {
                    Console.Write($"🔎 Scanning {props.Size.ToFileSize()} file '{file.Path.Truncate(truncateLength)}'     ");
                    Console.SetCursorPosition(0, Console.CursorTop);
                }
                else
                {
                    Console.Write($"🔎 Scanning file '{file.Path.Truncate(truncateLength)}'     ");
                    Console.SetCursorPosition(0, Console.CursorTop);
                }

                string pattern = "(\\S+\\s+){0}\\S*" + searchPattern + "\\S*(\\s+\\S+){0}";
                var tokens = searchPattern.Split(" ", StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length == 2)
                {
                    pattern = $"\\b(?:{tokens[0]}|{tokens[1]})\\b(?:.*?\\b(?:{tokens[0]}|{tokens[1]})\\b)?";
                }
                else if (tokens.Length == 3)
                {
                    pattern = $"\\b(?:{tokens[0]}|{tokens[1]}|{tokens[2]})\\b(?:.*?\\b(?:{tokens[0]}|{tokens[1]}|{tokens[2]})\\b)?";
                }
                else if (tokens.Length == 4)
                {
                    pattern = $"\\b(?:{tokens[0]}|{tokens[1]}|{tokens[2]}|{tokens[3]})\\b(?:.*?\\b(?:{tokens[0]}|{tokens[1]}|{tokens[2]}|{tokens[3]})\\b)?";
                }

                try
                {
                    string text = await FileIO.ReadTextAsync(file, Windows.Storage.Streams.UnicodeEncoding.Utf8);
                    Regex regex = new Regex(pattern);
                    MatchCollection matches = regex.Matches(text);
                    foreach (Match match in matches)
                    {
                        Console.WriteLine($"{Environment.NewLine}   ✅ Pos: {match.Index,-4} Match: {match.Value}");
                    }
                }
                catch (System.Runtime.InteropServices.COMException ex)  // No mapping for the Unicode character exists in the target multi-byte code page.
                {
                    counterWarnings++;

                    if (ex.Message.StartsWith("No mapping for the Unicode character exists"))
                    {
                        string text = await FileIO.ReadTextAsync(file, Windows.Storage.Streams.UnicodeEncoding.Utf16LE);
                        Regex regex = new Regex(pattern);
                        MatchCollection matches = regex.Matches(text);
                        foreach (Match match in matches)
                        {
                            Console.WriteLine($"{Environment.NewLine}   ✅ Pos: {match.Index,-4} Match: {match.Value}");
                        }
                    }
                    else // could be BigEndian or some other BOM format
                    {
                        Console.WriteLine($"{Environment.NewLine} ❌ SearchFile: {ex.Message}");
                    }
                }
            }
            catch (System.Runtime.InteropServices.COMException ex)
            {
                counterWarnings++;
                Debug.WriteLine($"[WARNING] {ex.Message}");
            }
            catch (Exception ex)
            {
                counterWarnings++;
                Console.WriteLine($"⚠️ SearchFile: {ex.Message}");
            }
        }
    }

    #region [Experimentation]
    /// <summary>
    /// You can use the <see cref="StorageFile.CreateStreamedFileAsync(string, StreamedFileDataRequestedHandler, IRandomAccessStreamReference)"/>
    /// method to create a virtual StorageFile. You give it a name, a delegate, and an optional thumbnail. 
    /// When somebody tries to access the contents of the virtual StorageFile, your delegate will be invoked, 
    /// and its job is to fill the provided output stream with data.
    /// </summary>
    public static async Task<StorageFile?> ConvertFileAsync(StorageFile originalFile)
    {
        byte[] data = Encoding.UTF8.GetBytes("example file contents");
        IBuffer buffer = data.AsBuffer();

        try
        {
            var thumb = await GetFileThumbnailStreamReferenceAsync(originalFile);

            // The temp file will normally appear here "C:\Users\AccountName\AppData\Local\Temp\sample.txt"
            return await StorageFile.CreateStreamedFileAsync("sample.txt",
               async (request) =>
               {
                   using (request)
                   {
                       await request.WriteAsync(buffer);
                   }
               }, thumb);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WARNING] ConvertFileAsync: {ex.Message}");
        }
        return null;
    }

    /// <summary>
    /// Gets <see cref="IRandomAccessStreamReference"/> for a file thumbnail. Can be used in tandem with 
    /// <see cref="StorageFile.CreateStreamedFileAsync(string, StreamedFileDataRequestedHandler, IRandomAccessStreamReference)"/>.
    /// </summary>
    public static async Task<IRandomAccessStreamReference?> GetFileThumbnailStreamReferenceAsync(StorageFile file)
    {
        if (file != null)
        {
            try
            {   // Get the thumbnail
                var thumbnail = await file.GetThumbnailAsync(ThumbnailMode.SingleItem);
                if (thumbnail != null)
                {
                    Debug.WriteLine($"[WARNING] Thumbnail size is {thumbnail.Size.ToFileSize()}");
                    // Create IRandomAccessStreamReference from the thumbnail
                    return RandomAccessStreamReference.CreateFromFile(file);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WARNING] GetFileThumbnailStreamReferenceAsync: {ex.Message}");
            }
        }
        return null; // Return null if no file or thumbnail is found
    }

    public static async void CreateStreamedFile()
    {
        // Create a streamed file.
        StorageFile file = await StorageFile.CreateStreamedFileAsync("file.txt", StreamedFileWriter, null);

        // Prepare to copy the file (don't use "ApplicationData.Current.LocalFolder" with console app)
        StorageFolder localFolder = await StorageFolder.GetFolderFromPathAsync(AppContext.BaseDirectory);
        string newName = "copied_file.txt";

        // Copy the streamed file. At this point, the data is streamed into the source file.
        await file.CopyAsync(localFolder, newName, NameCollisionOption.ReplaceExisting);
    }

    public static async void StreamedFileWriter(StreamedFileDataRequest request)
    {
        try
        {
            using (var stream = request.AsStreamForWrite())
            using (var streamWriter = new StreamWriter(stream))
            {
                for (int l = 0; l < 50; l++)
                {
                    await streamWriter.WriteLineAsync($"Data line #{l}.");
                }
            }
            request.Dispose();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WARNING] StreamedFileWriter: {ex.Message}");
            request.FailAndClose(StreamedFileFailureMode.Incomplete);
        }
    }

    //public async Task SaveThumbnailAsPngAsync(StorageFile source, StorageFile output)
    //{
    //    var thumbnail = await source.GetThumbnailAsync(Windows.Storage.FileProperties.ThumbnailMode.SingleItem);
    //    if (thumbnail != null)
    //    {
    //        // This would only work for  Microsoft.UI.Xaml.Control:
    //        var renderTargetBitmap = new RenderTargetBitmap();
    //        await renderTargetBitmap.RenderAsync(thumbnail);
    //        if (output != null)
    //        {
    //            using (var stream = await output.OpenAsync(FileAccessMode.ReadWrite))
    //            {
    //                var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
    //                encoder.SetSoftwareBitmap(await SoftwareBitmap.CreateCopyFromBuffer(renderTargetBitmap.GetPixels(), BitmapPixelFormat.Bgra8, renderTargetBitmap.PixelWidth, renderTargetBitmap.PixelHeight));
    //                await encoder.FlushAsync();
    //            }
    //        }
    //    }
    //}

    #endregion
}

