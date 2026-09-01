using MathNet.Numerics;
using MathNet.Numerics.IntegralTransforms;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace Spectrogram
{
    public static class SpectrogramProcessor
    {
        public static Bitmap CreateSpectrogramBitmap(float[] signal)
        {
            // Example usage loop
            var result = CalculateSTFT(signal, 1024, 512);
            var magnitudeGrid = ProcessToLogMagnitudes(result);
            var bitmap = RenderToBitmap(magnitudeGrid);
            return bitmap;
        }

        public static List<Complex32[]> CalculateSTFT(float[] signal, int frameSize, int hopSize)
        {
            List<Complex32[]> spectrogram = new List<Complex32[]>();

            // 1. Generate a Hann Window to smooth frame edges
            //var window = Window.Hann(frameSize);
            var window = Array.ConvertAll(Window.Hann(frameSize), x => (float)x);

            // 2. Slide the window across the signal
            for (int start = 0; start <= signal.Length - frameSize; start += hopSize)
            {
                // Create a frame buffer for Complex numbers
                Complex32[] frame = new Complex32[frameSize];

                // 3. Copy signal chunk and apply the window function
                for (int i = 0; i < frameSize; i++)
                {
                    var realPart = signal[start + i] * window[i];
                    frame[i] = new Complex32(realPart, 0f);
                }

                // 4. Compute the Forward FFT in-place
                Fourier.Forward(frame, FourierOptions.Matlab);

                // 5. Store the spectrum chunk
                spectrogram.Add(frame);
            }

            return spectrogram;
        }

        public static float[,] ProcessToMagnitudes(List<Complex32[]> stftOutput)
        {
            int timeSlots = stftOutput.Count;
            // We only need the first half (up to Nyquist frequency) because real signals are symmetric
            int freqBins = stftOutput[0].Length / 2;

            float[,] grid = new float[timeSlots, freqBins];
            var maxAmplitude = stftOutput.Max(a => a.Max(v => v.Magnitude));

            for (int t = 0; t < timeSlots; t++)
            {
                for (int f = 0; f < freqBins; f++)
                {
                    grid[t, f] = stftOutput[t][f].Magnitude / maxAmplitude;
                }
            }

            return grid;
        }

        public static float[,] ProcessToLogMagnitudes(List<Complex32[]> stftOutput)
        {
            int timeSlots = stftOutput.Count;
            // We only need the first half (up to Nyquist frequency) because real signals are symmetric
            int freqBins = stftOutput[0].Length / 2;

            float[,] grid = new float[timeSlots, freqBins];
            float minDb = -80f; // Silence threshold 
            float maxDb = 0f;   // Maximum expected loudness

            for (int t = 0; t < timeSlots; t++)
            {
                for (int f = 0; f < freqBins; f++)
                {
                    float magnitude = stftOutput[t][f].Magnitude;

                    // Convert to Decibels
                    float db = 20f * MathF.Log10(magnitude + 1e-6f);

                    // Normalize the dB value between 0.0f (silent) and 1.0f (loudest)
                    float normalized = (db - minDb) / (maxDb - minDb);
                    grid[t, f] = Math.Clamp(normalized, 0f, 1f);
                }
            }
            return grid;
        }

        public static Bitmap CreateBitmapFromGrid(float[,] magnitudeGrid)
        {
            int width = magnitudeGrid.GetLength(0);  // Time axis
            int height = magnitudeGrid.GetLength(1); // Frequency axis

            // 1. Create a 24-bit RGB Bitmap
            Bitmap bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            // Lock the bitmap memory for direct fast pixel writing
            BitmapData bitmapData = bitmap.LockBits(
                new Rectangle(0, 0, width, height),
                ImageLockMode.WriteOnly,
                bitmap.PixelFormat);

            int stride = bitmapData.Stride;
            IntPtr scan0 = bitmapData.Scan0;

            unsafe
            {
                byte* p = (byte*)(void*)scan0;

                //for (int y = 0; y < height/2; y++)
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        // Invert Y axis: Low audio frequencies go at the bottom of the image
                        //float intensity = magnitudeGrid[x, height - 1 - y];
                        float intensity = magnitudeGrid[x, height - 1 - y];

                        // Calculate color channels (Classic Jet / Heat Map)
                        byte r = 0, g = 0, b = 0;
                        //if (intensity < 0.33f)
                        //{
                        //    b = (byte)(intensity / 0.33f * 255);
                        //}
                        //else if (intensity < 0.66f)
                        //{
                        //    g = (byte)((intensity - 0.33f) / 0.33f * 255);
                        //    b = (byte)(255 - g);
                        //}
                        //else
                        //{
                        //    r = (byte)((intensity - 0.66f) / 0.34f * 255);
                        //    g = (byte)(255 - r);
                        //}
                        r = Brighten(intensity);
                        g = Brighten(intensity);
                        b = Brighten(intensity);

                        // Calculate memory position. Windows Bitmaps store pixels as BGR
                        int index = (y * stride) + (x * 3);
                        p[index] = b; // Blue
                        p[index + 1] = g; // Green
                        p[index + 2] = r; // Red
                    }
                }
            }

            // Unlock the memory buffer
            bitmap.UnlockBits(bitmapData);

            return bitmap;
        }

        private static byte Brighten(float intensity)
        {
            var value = (int)(255.0 * intensity * 2.0);
            return (value > 255) ? (byte)255 : (byte)value;
        }

        public static Bitmap RenderToBitmap(float[,] magnitudeGrid)
        {
            int originalWidth = magnitudeGrid.GetLength(0);
            int height = magnitudeGrid.GetLength(1);

            // Target a square image where Width == Height
            int targetWidth = height;

            // 1. Generate the original un-stretched bitmap using the fast LockBits code
            //using (Bitmap originalBitmap = CreateBitmapFromGrid(magnitudeGrid))
            {
                Bitmap originalBitmap = CreateBitmapFromGrid(magnitudeGrid);
                return originalBitmap;
                // 2. Create a new blank square canvas
                Bitmap stretchedBitmap = new Bitmap(targetWidth, height, PixelFormat.Format24bppRgb);
                
                // 3. Use Graphics to draw and stretch the image
                using (Graphics g = Graphics.FromImage(stretchedBitmap))
                {
                    // Set high quality scaling so the pixels don't look blurry or blocky
                    //g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.InterpolationMode = InterpolationMode.NearestNeighbor;
                    //g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.PixelOffsetMode = PixelOffsetMode.None;

                    // Draw the old image into the new square boundaries
                    g.DrawImage(originalBitmap, 0, 0, targetWidth, height);
                }

                return stretchedBitmap;
            }
        }

        public static byte[] ConvertStereoToMono(byte[] stereoData)
        {
            // A stereo array has 2 channels. Mono will use half the bytes.
            byte[] monoData = new byte[stereoData.Length / 2];

            // A stereo frame is 4 bytes (2 bytes for Left, 2 bytes for Right).
            for (int i = 0; i < stereoData.Length; i += 4)
            {
                // 1. Reconstruct 16-bit signed integers for Left and Right
                short left = (short)((stereoData[i + 1] << 8) | (stereoData[i] & 0xff));
                short right = (short)((stereoData[i + 3] << 8) | (stereoData[i + 2] & 0xff));

                // 2. Average the two channels to create the mono sample
                short monoSample = (short)((left + right) / 2);

                // 3. Convert the mono sample back to 2 bytes and store it
                int targetIndex = i / 2;
                monoData[targetIndex] = (byte)(monoSample & 0xff);       // Low Byte
                monoData[targetIndex + 1] = (byte)((monoSample >> 8) & 0xff); // High Byte
            }

            return monoData;
        }
    }
}
