using Accord.Math;
using Accord.Video.FFMPEG;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace QtmVideoGridRender
{
    class Program
    {
        public static byte[] ImageToByte(Image img)
        {
            ImageConverter converter = new ImageConverter();
            return (byte[])converter.ConvertTo(img, typeof(byte[]));
        }

        enum TimecodePosition
        {
            TopLeft,
            Center
        }

        static string[] FindAviFilesForDifferentCameras(string directory, string filenameWithoutExtension)
        {
            var aviFilesOqus = Directory.GetFiles(directory, filenameWithoutExtension + "_Oqus*.avi");
            var aviFilesMiqus = Directory.GetFiles(directory, filenameWithoutExtension + "_Miqus*.avi");
            var aviFilesArqus = Directory.GetFiles(directory, filenameWithoutExtension + "_Arqus*.avi");
            return aviFilesOqus.Concat(aviFilesMiqus).Concat(aviFilesArqus).ToArray();
        }

        static void Main(string[] args)
        {
            try
            {
                // Do a search of all subdirectories
                foreach (var filename in Directory.EnumerateFiles(".", "*.qtm", SearchOption.AllDirectories))
                {
                    var fullpath = Path.GetFullPath(filename);
                    var directory = Path.GetDirectoryName(fullpath);
                    
                    var filenameWithoutExtension = Path.GetFileNameWithoutExtension(filename);
                    var outputfilename = filenameWithoutExtension + ".avi";
                    var outputfilepath = Path.Combine(directory, outputfilename);

                    if (!File.Exists(outputfilepath))
                    {
                        var aviFiles = FindAviFilesForDifferentCameras(directory, filenameWithoutExtension);
                        if (aviFiles.Length == 0)
                        {
                            Console.WriteLine("No avi files available for: " + filename);
                            continue;
                        }
                        Console.WriteLine("Starting to process: " + filename);
                        ProcessAViFiles(outputfilepath, aviFiles);
                    }
                    else
                    {
                        Console.WriteLine("Already existing avi file for: " + filename);
                    }
                }
                Console.WriteLine("Finished processing. Press key to close");
                Console.ReadKey();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        class AviFileInfo : IDisposable
        {
            public int hour;
            public int minute;
            public int second;
            public int frame;
            public VideoFileReader reader;
            public string filename;
            public DateTime time;
            public double framecounter;
            public double timecodeFrequency;
            private bool disposedValue;

            protected virtual void Dispose(bool disposing)
            {
                if (!disposedValue)
                {
                    if (disposing)
                    {
                        // Dispose managed state (managed objects)
                    }

                    if (reader != null)
                    {
                        reader.Dispose();
                        reader = null;
                    }

                    disposedValue = true;
                }
            }

            ~AviFileInfo()
            {
                // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
                Dispose(disposing: false);
            }

            public void Dispose()
            {
                // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
                Dispose(disposing: true);
                GC.SuppressFinalize(this);
            }
        }

        /// <summary>
        /// Resize the image to the specified width and height.
        /// </summary>
        /// <param name="image">The image to resize.</param>
        /// <param name="width">The width to resize to.</param>
        /// <param name="height">The height to resize to.</param>
        /// <returns>The resized image.</returns>
        public static Bitmap ResizeImage(Image image, int width, int height)
        {
            var destRect = new Rectangle(0, 0, width, height);
            var destImage = new Bitmap(width, height);

            destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            using (var graphics = Graphics.FromImage(destImage))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                using (var wrapMode = new ImageAttributes())
                {
                    wrapMode.SetWrapMode(WrapMode.TileFlipXY);
                    graphics.DrawImage(image, destRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, wrapMode);
                }
            }

            return destImage;
        }
        private static void ProcessAViFiles(string outputfilename, string[] aviFiles)
        {
            Size largestResolution = new Size();
            long maxLength = 0;
            double maxFrameRate = double.MinValue;
            double minFrameRate = double.MaxValue;
            int minBitRate = int.MaxValue;
            int maxBitRate = int.MinValue;
            List<AviFileInfo> afis = new List<AviFileInfo>();
            int timecodeFoundIndex = -1;

            Font timecodeTextFont = new Font("Tahoma", 70);
            TimecodePosition timecodePosition = TimecodePosition.Center;

            foreach (var file in aviFiles)
            {
                try
                {
                    AviFileInfo afi = new AviFileInfo();
                    using (var tagFile = TagLib.File.Create(file))
                    {
                        if (tagFile != null)
                        {
                            int hour = 0;
                            int minute = 0;
                            int second = 0;
                            int frame = 0;
                            var tags = tagFile.Tag;
                            if (!string.IsNullOrEmpty(tags.Timecode))
                            {
                                try
                                {
                                    Console.WriteLine("Reading timecode (" + tags.Timecode + ") tag in: " + file);
                                    var parts = tags.Timecode.Split(':');
                                    if (parts.Length > 0)
                                        hour = int.Parse(parts[0]);
                                    if (parts.Length > 1)
                                        minute = int.Parse(parts[1]);
                                    if (parts.Length > 2)
                                        second = int.Parse(parts[2]);
                                    if (parts.Length > 3)
                                        frame = int.Parse(parts[3]);
                                    timecodeFoundIndex = afis.Count;
                                }
                                catch(Exception)
                                {
                                    hour = 0;
                                    minute = 0;
                                    second = 0;
                                    frame = 0;
                                }
                            }
                            else
                            {
                                Console.WriteLine("No timecode found in: " + file);
                            }
                            afi.hour = hour;
                            afi.minute = minute;
                            afi.second = second;
                            afi.frame = frame;
                            afi.time = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, afi.hour, afi.minute, afi.second);

                            double timecodeFrequency = 30;
                            if (!string.IsNullOrEmpty(tags.TimecodeFrequency))
                            {
                                Console.WriteLine("Reading timecode frequency (" + tags.TimecodeFrequency + ") tag in: " + file);
                                try
                                {
                                    timecodeFrequency = double.Parse(tags.TimecodeFrequency);
                                }
                                catch (Exception)
                                {
                                }
                            }
                            else
                            {
                                Console.WriteLine("No timecode frequency found in: " + file);
                            }

                            afi.timecodeFrequency = timecodeFrequency;
                            afi.filename = file;
                        }
                    }
                    var reader = new VideoFileReader();
                    reader.Open(file);
                    largestResolution.Width = Math.Max(largestResolution.Width, reader.Width);
                    largestResolution.Height = Math.Max(largestResolution.Height, reader.Height);
                    minBitRate = Math.Min(minBitRate, reader.BitRate);
                    maxBitRate = Math.Max(maxBitRate, reader.BitRate);

                    maxLength = Math.Max(maxLength, reader.FrameCount);
                    minFrameRate = Math.Min(minFrameRate, reader.FrameRate.ToDouble());
                    maxFrameRate = Math.Max(maxFrameRate, reader.FrameRate.ToDouble());

                    afi.reader = reader;
                    afis.Add(afi);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Could not open avi file (" + e.Message + ") : " + file);
                }
            }

            try
            {

                // timestamp from first found timestamp video (or read from qtm file?), fixed timestamp frequency...
                double timestampFrequency = Math.Min(maxFrameRate, 30);
                DateTime timestamp = afis[0].time;
                int timestampFrame = afis[0].frame;
                if (timecodeFoundIndex >= 0)
                {
                    timestampFrequency = afis[timecodeFoundIndex].timecodeFrequency;
                    timestamp = afis[timecodeFoundIndex].time;
                    timestampFrame = afis[timecodeFoundIndex].frame;
                }

                double timestampCounter = 0;

                // Determine how many rows and columns it should contain
                Size gridSize = GetOutputGridSizes(aviFiles.Length);

                // TODO::: Be able to specify output size and resize all bitmaps accordingly.
                Size outputSize = new Size(largestResolution.Width * gridSize.Width, largestResolution.Height * gridSize.Height);

                Console.WriteLine("Writing file: " + outputfilename + " " + outputSize.Width.ToString() + "x" + outputSize.Height.ToString() + " Frequency: " + maxFrameRate + " Bitrate: " + minBitRate.ToString());


                var writer = new VideoFileWriter();
                writer.Width = largestResolution.Width * gridSize.Width;
                writer.Height = largestResolution.Height * gridSize.Height;
                writer.FrameRate = new Rational(maxFrameRate);
                writer.BitRate = minBitRate;
                writer.VideoCodec = VideoCodec.Mpeg4;// H264;
                writer.AudioCodec = AudioCodec.None;
                writer.Open(outputfilename);


                Bitmap[] bitmaps = new Bitmap[afis.Count];

                var bigBitmap = new Bitmap(outputSize.Width, outputSize.Height);
                Graphics graphicsContext = Graphics.FromImage(bigBitmap);

                int frameNumberToWrite = 0;
                while (frameNumberToWrite++ < maxLength)
                {
                    Console.WriteLine("Writing frame: " + frameNumberToWrite.ToString() + "/" + maxLength.ToString());

                    int afiIndex = 0;
                    foreach (var afi in afis)
                    {
                        // If readers have different framerates we need to interpolate lower framerate to the highest.
                        var framestep = afi.reader.FrameRate.ToDouble() / maxFrameRate;
                        afi.framecounter += framestep;
                        Bitmap bitmap = null;
                        if (afi.framecounter >= 1)
                        {
                            if (bitmaps[afiIndex] != null)
                                bitmaps[afiIndex].Dispose();

                            try
                            {
                                bitmap = afi.reader.ReadVideoFrame();
                                //bitmap.Save(afi.filename + "_" + frameNumberToWrite.ToString() + ".bmp");
                            }
                            catch (Exception)
                            {
                                // No frames left?
                            }

                            afi.framecounter -= 1;
                        }
                        else
                        {
                            if (bitmaps[afiIndex] == null)
                            {
                                try
                                {
                                    bitmap = afi.reader.ReadVideoFrame();
                                }
                                catch (Exception)
                                {
                                    // No frames left?
                                }
                            }
                            else
                                bitmap = bitmaps[afiIndex];
                        }

                        bitmaps[afiIndex] = bitmap;
                        afiIndex++;
                    }

                    {
                        int coordx = 0;
                        int coordy = 0;
                        int indexOnLine = 1;
                        for (int index = 0; index < afis.Count; index++)
                        {
                            if (bitmaps[index] != null)
                            {
                                graphicsContext.DrawImage(bitmaps[index], coordx, coordy);
                            }
                            coordx += afis[index].reader.Width;
                            if (indexOnLine++ >= gridSize.Width)
                            {
                                indexOnLine = 0;
                                coordx = 0;
                                coordy += afis[index].reader.Height;
                            }
                        }

                        graphicsContext.SmoothingMode = SmoothingMode.AntiAlias;
                        graphicsContext.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        graphicsContext.PixelOffsetMode = PixelOffsetMode.HighQuality;


                        string timeToWrite = (timecodeFoundIndex >= 0) ? string.Format($"{timestamp.ToString("HH:mm:ss")}.{timestampFrame:D2}") : string.Format($"{timestamp.ToString("HH:mm:ss")}");
                        var size = graphicsContext.MeasureString(timeToWrite, timecodeTextFont);
                        RectangleF rect = new RectangleF(10, 10, size.Width, size.Height);
                        if (timecodePosition == TimecodePosition.Center)
                        {
                            rect.X = (outputSize.Width / 2) - (size.Width / 2);
                            rect.Y = (outputSize.Height / 2) - (size.Height / 2);
                            rect.Width = size.Width;
                            rect.Height = size.Height;
                        }
                        graphicsContext.FillRectangle(Brushes.Black, rect);
                        graphicsContext.DrawString(timeToWrite, timecodeTextFont, Brushes.White, rect);

                        graphicsContext.Flush();
                    }

                    var timestampStep = timestampFrequency / maxFrameRate;
                    timestampCounter += timestampStep;
                    if (timestampCounter >= 1)
                    {
                        timestampFrame++;
                        timestampCounter -= 1;
                    }
                    if (timestampFrame >= timestampFrequency)
                    {
                        timestampFrame = 0;
                        timestamp = timestamp.AddSeconds(1);
                    }

                    writer.WriteVideoFrame(bigBitmap);
                    //bigBitmap.Save(outputfilename + "_" + frameNumberToWrite.ToString() + ".bmp");
                }
                writer.Close();
                graphicsContext.Dispose();
                bigBitmap.Dispose();
                writer.Dispose();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            foreach(var afi in afis)
            {
                afi.Dispose();
            }
            afis.Clear();
        }

        /// <summary>
        /// Returns a grid size depending on number of files
        /// </summary>
        /// <param name="numberOfFiles"></param>
        /// <returns></returns>
        private static Size GetOutputGridSizes(int numberOfFiles)
        {
            int countOnX;
            int countOnY;
            switch (numberOfFiles)
            {
                case 1:
                    countOnX = 1;
                    countOnY = 1;
                    break;
                case 2:
                    countOnX = 2;
                    countOnY = 1;
                    break;
                case 3:
                    countOnX = 3;
                    countOnY = 1;
                    break;
                case 4:
                    countOnX = 2;
                    countOnY = 2;
                    break;
                case 5:
                case 6:
                    countOnX = 3;
                    countOnY = 2;
                    break;
                case 7:
                case 8:
                    countOnX = 4;
                    countOnY = 2;
                    break;
                case 9:
                    countOnX = 3;
                    countOnY = 3;
                    break;
                case 10:
                    countOnX = 5;
                    countOnY = 2;
                    break;
                case 11:
                case 12:
                    countOnX = 4;
                    countOnY = 3;
                    break;
                case 15:
                    countOnX = 5;
                    countOnY = 3;
                    break;
                case 13:
                case 14:
                case 16:
                    countOnX = 4;
                    countOnY = 4;
                    break;
                case 17:
                case 18:
                case 19:
                case 20:
                    countOnX = 5;
                    countOnY = 4;
                    break;
                default:
                    countOnX = countOnY = (int)Math.Ceiling(numberOfFiles / 2.0);
                    break;
            }
            return new Size(countOnX, countOnY);
        }
    }
}