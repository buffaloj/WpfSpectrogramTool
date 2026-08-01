using MathNet.Numerics;
using Spectrogram;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace SpectrogramTool.Wpf
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : System.Windows.Window
    {
        public MainWindow()
        {
            InitializeComponent();

            //RenderFromByteArray();
            ProcessSin();
        }

        public void RenderFromByteArray()
        {
            int width = 64;
            int height = 64;
            int stride = width * 4; // 4 bytes per pixel for BGRA32
            byte[] pixels = new byte[height * stride];

            // Fill array with a solid custom color pattern (e.g., green)
            for (int i = 0; i < pixels.Length; i += 4)
            {
                pixels[i] = 0;     // B
                pixels[i + 1] = 255; // G
                pixels[i + 2] = 0;   // R
                pixels[i + 3] = 255; // A
            }

            // Create the BitmapSource
            BitmapSource bitmap = BitmapSource.Create(
                width, height, 96, 96,
                PixelFormats.Bgra32, null,
                pixels, stride
            );

            myImage.Source = bitmap;

            RenderOptions.SetBitmapScalingMode(myImage, BitmapScalingMode.NearestNeighbor);

            // myImage.RenderOptions.
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            // Check if the dragged data contains file paths
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // Cast the data to an array of strings (file paths)
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

                if (files.Length > 0)
                {
                    string imagePath = files[0];

                    // Verify it is an image file
                    string extension = System.IO.Path.GetExtension(imagePath).ToLower();
                    string[] allowedExtensions = { ".wav" };

                    if (Array.Exists(allowedExtensions, ext => ext == extension))
                    {
                        ProcessWAV(imagePath);
                    }
                    else
                    {
                        MessageBox.Show("Please drop a valid image file.");
                    }
                }
            }
        }

        private List<Complex32[]> _complex;
        private float _maxAmplitude = 0.0f;
        private float[,] _magnitudeGrid;
        private void ProcessWAV(string filePath)
        {
            try
            {
                var wavFile = LoadWavSamples(filePath);
                _sampleRate = wavFile.SampleRate;

                var orgSamples = (wavFile.Channels == 1) ? wavFile.Samples : ConvertStereoToMono(wavFile.Samples);

                short[] samples = new short[orgSamples.Length / 2];
                // Copy byte data block directly into the short array
                Buffer.BlockCopy(orgSamples, 0, samples, 0, orgSamples.Length);

                var floatSamples = Array.ConvertAll(samples, s => (float)s / (float)short.MaxValue);

                //_complex = SpectrogramProcessor.CalculateSTFT(floatSamples, 1024, 512);
                _complex = SpectrogramProcessor.CalculateSTFT(floatSamples, 1024, 512);
                //_magnitudeGrid = SpectrogramProcessor.ProcessToLogMagnitudes(_complex);
                _magnitudeGrid = SpectrogramProcessor.ProcessToMagnitudes(_complex);
                var bitmap = SpectrogramProcessor.RenderToBitmap(_magnitudeGrid);

                _maxAmplitude = _complex.Max(a => a.Max(v => v.Magnitude));

                ApplyBitmap(bitmap);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        public static Int16[] GenerateSineWave(double frequency, int sampleRate, float amplitude = 1.0f)
        {
            var numSamples = (int)((1.0 / frequency) * sampleRate * 100.0) + 1;
            var buffer = new short[numSamples];
            for (int i = 0; i < buffer.Length; i++)
            {
                // Calculate the time 't' for the current sample 'i'
                double t = (double)i / sampleRate;

                // Calculate the value of the sine wave at time 't'
                buffer[i] = (Int16)(amplitude * Math.Sin(2.0 * Math.PI * frequency * t) * Int16.MaxValue);
            }

            return buffer;
        }

        private void ProcessSin()
        {
            try
            {
                var samples = GenerateSineWave(1000, 44100);
                _sampleRate = 44100;

                var floatSamples = Array.ConvertAll(samples, s => (float)s / (float)short.MaxValue);

                _complex = SpectrogramProcessor.CalculateSTFT(floatSamples, 1024, 512);
                //_magnitudeGrid = SpectrogramProcessor.ProcessToLogMagnitudes(_complex);
                _magnitudeGrid = SpectrogramProcessor.ProcessToMagnitudes(_complex);
                var bitmap = SpectrogramProcessor.RenderToBitmap(_magnitudeGrid);

                _maxAmplitude = _complex.Max(a => a.Max(v => v.Magnitude));

                ApplyBitmap(bitmap);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
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

        private void ApplyBitmap(Bitmap bitmap)
        {
            IntPtr hBitmap = bitmap.GetHbitmap();
            try
            {
                // 2. Convert the handle to a WPF BitmapSource
                BitmapSource bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap,
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());

                // 3. Freeze the source to improve UI performance
                bitmapSource.Freeze();

                // 4. Assign it to your WPF Image control
                myImage.Source = bitmapSource;

                myImage.Visibility = Visibility.Visible;
                canvas.Visibility = Visibility.Visible;
                message.Visibility = Visibility.Collapsed;
            }
            finally
            {
                // 5. Clean up memory to avoid leaks
                DeleteObject(hBitmap);
            }
        }

        private void canvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
        }

        // Fires once when the mouse pointer enters the boundaries of the Canvas
        private void image_MouseEnter(object sender, MouseEventArgs e)
        {
            canvas.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 240, 0));

            var mousePosition = e.GetPosition(myImage);
            
            this.Title = $"X: {mousePosition.X}, Y: {mousePosition.Y}";

            DisplayDetails(mousePosition);
        }

        // Fires once when the mouse pointer moves completely out of the Canvas
        private void image_MouseLeave(object sender, MouseEventArgs e)
        {
            // Example action: Reset background and title when mouse leaves
            canvas.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 0, 0));
            this.Title = "";

            canvas.Children.Remove(lines);
        }

        private void image_MouseMove(object sender, MouseEventArgs e)
        {
            // Get the precise X and Y coordinates relative to the Canvas
            var mousePosition = e.GetPosition(myImage);

            //this.Title = $"X: {mousePosition.X}, Y: {mousePosition.Y}";
            

            DisplayDetails(mousePosition);
        }

        private void myImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Get the precise X and Y coordinates relative to the Canvas
            var mousePosition = e.GetPosition(myImage);
            LogDetails(mousePosition);
        }

        private int _sampleRate = 44100;
        private Polyline? lines { get; set; }
        private void DisplayDetails(System.Windows.Point mousePos)
        {
            var percentX = (double)mousePos.X / (double)myImage.ActualWidth;
            var percentY = ((double)mousePos.Y / (double)myImage.ActualHeight);

            var canvasWidth = canvas.Width;

            if (lines != null)
            {
                canvas.Children.Remove(lines);
                lines = null; 
            }

            if (_magnitudeGrid == null)
                return;

            var column = (int)(percentX * (_magnitudeGrid.GetLength(0) - 1.0));
            var row = (int)((1.0-percentY) * (_magnitudeGrid.GetLength(1) - 1.0));

            var numBuckets = _magnitudeGrid.GetLength(1);
            var freqStep = (_sampleRate / 2) / numBuckets;
            var frequency = row * freqStep;

            this.Title = $"Row: {row}, Freq: {frequency}, Power: {_magnitudeGrid[column, row]}, Mag: {_complex[column][row].Magnitude}";

            //***
            // 1. Create a new Polyline instance
            lines = new Polyline();

            // 2. Set the visual properties
            lines.Stroke = System.Windows.Media.Brushes.Black;
            lines.StrokeThickness = 2;

            var step = canvas.Width / (_magnitudeGrid.GetLength(0)-1);

            var some = _magnitudeGrid.GetLength(0);
            for (int i = 0; i < _magnitudeGrid.GetLength(0); i++)
            {
                // 3. Create and add your points (X, Y)
                lines.Points.Add(new System.Windows.Point(step*i, (1.0- (_complex[i][row].Magnitude/_maxAmplitude))*canvas.Height));  
            }

            // 4. Add the polyline to your Canvas
            canvas.Children.Add(lines);
            //***
        }

        private void LogDetails(System.Windows.Point mousePos)
        {
            var percentX = (double)mousePos.X / (double)myImage.ActualWidth;
            var percentY = ((double)mousePos.Y / (double)myImage.ActualHeight);

            var canvasWidth = canvas.Width;

            if (_magnitudeGrid == null)
                return;

            var column = (int)(percentX * (_magnitudeGrid.GetLength(0) - 1.0));
            var row = (int)((1.0 - percentY) * (_magnitudeGrid.GetLength(1) - 1.0));

            var step = canvas.Width / (_magnitudeGrid.GetLength(0) - 1);

            var values = new float[_magnitudeGrid.GetLength(0)];
            for (int i = 0; i < _magnitudeGrid.GetLength(0); i++)
            {
                // 3. Create and add your points (X, Y)
                values[i] = _complex[i][row].Magnitude / _maxAmplitude;
            }

            var numBuckets = _magnitudeGrid.GetLength(1);
            var freqStep = (_sampleRate / 2) / numBuckets;
            var frequency = row * freqStep;

            var message = string.Join(",", values.Select(f => $"{f}f"));
            Debug.WriteLine($"freq:{frequency}, envelope:{message}");
        }

        private AudioSamples LoadWavSamples(string filename)
        {
            var wavPath = System.IO.Path.Combine(Directory.GetCurrentDirectory(), filename);
            var wavFile = File.ReadAllBytes(wavPath);

            ValidateWavFile(wavFile, filename);

            // Assuming standard 44-byte WAV header, data starts at index 44
            var samples = new byte[wavFile.Length - 44];
            Array.Copy(wavFile, 44, samples, 0, samples.Length);

            return new AudioSamples
            {
                Samples = samples,
                Channels = BitConverter.ToInt16(wavFile, 22),
                SampleRate = BitConverter.ToInt32(wavFile, 24),
                BitsPerSample = BitConverter.ToInt16(wavFile, 34),
            };
        }

        private void ValidateWavFile(byte[] bytes, string filename)
        {
            if (bytes.Length <= 44)
                throw new Exception($"{filename} is not a valid WAV file.");

            if (bytes[0] != 'R' || bytes[1] != 'I' || bytes[2] != 'F' || bytes[3] != 'F')
                throw new Exception($"{filename} is not a valid WAV file.");

            if (bytes[8] != 'W' || bytes[9] != 'A' || bytes[10] != 'V' || bytes[11] != 'E')
                throw new Exception($"{filename} is not a valid WAV file.");
        }

        // Add this Win32 API declaration to your class
        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);
    }
}