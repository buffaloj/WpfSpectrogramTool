using MathNet.Numerics;
using Spectrogram;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using static Spectrogram.SpectrogramProcessor;

namespace SpectrogramTool.Wpf
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : System.Windows.Window
    {
        private Instrument _instrument;
        private Note _currentNote;

        private string _wavefilePath;
        private string _instrumentConfigPath;

        public MainWindow()
        {
            InitializeComponent();

            //_instrument = LoadInstrumentConfig(_instrumentConfigPath);

            //RenderFromByteArray();
            ProcessSin();
        }

        // Determines if the Save action is allowed to run right now
        private void SaveCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = _instrument != null; // Set to false if there is nothing to save
        }

        // Executes when Ctrl+S is pressed
        private void SaveCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (_instrument == null)
                return;

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            string jsonString = JsonSerializer.Serialize(_instrument, options);
            File.WriteAllText(_instrumentConfigPath, jsonString);

            if (_currentNote == null || !_currentNote.Frequencies.Any())
                return;

            var envelopes = SpectrogramProcessor.ExtractEnvelopes(_magnitudeGrid, _currentNote.Frequencies.Select(f => new Tuple<int, float> (f.Index, f.Value)).ToList(), _currentNote.Length);
            var root = System.IO.Path.GetDirectoryName(_wavefilePath);

            var filePath = $"{root}\\{_currentNote.Name}.snd";
            SaveEnvelopes(envelopes, filePath);
        }

        private Instrument LoadInstrumentConfig(string filePath)
        {
            var jsonString = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<Instrument>(jsonString) ?? throw new JsonException("Deserialization returned null.");
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
                    string filePath = files[0];

                    // Verify it is an WAV file
                    string extension = System.IO.Path.GetExtension(filePath).ToLower();
                    string[] allowedExtensions = { ".wav", ".aiff" };

                    if (Array.Exists(allowedExtensions, ext => ext == extension))
                    {
                        ProcessWAV(filePath);

                        SetInstrument(filePath);

                        //var ext = System.IO.Path.GetExtension(filePath);
                        //var noteName = System.IO.Path.GetFileNameWithoutExtension(filePath);
                        //_currentNote = _instrument.Notes.Where(n => n.Name == noteName).FirstOrDefault();

                        if (_currentNote != null)
                            _currentNote.Length = _length;

                        DisplayNoteLines();
                    }
                    else
                    {
                        MessageBox.Show("Please drop a valid sound file.");
                    }
                }
            }
        }

        private List<Complex32[]> _complex;
        private float _maxAmplitude = 0.0f;
        private float[,] _magnitudeGrid;
        private float _length;
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

                _length = (float)samples.Length / (float)_sampleRate;
                //var envelopes = SpectrogramProcessor.ExtractEnvelopes(_magnitudeGrid, (float)samples.Length / (float)_sampleRate);

                //var name = System.IO.Path.GetDirectoryName(filePath) + System.IO.Path.DirectorySeparatorChar + (System.IO.Path.GetFileNameWithoutExtension(filePath) ?? "") + ".csv";
                //SaveEnvelopes(envelopes, name);

                ApplyBitmap(bitmap);

                _wavefilePath = filePath;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void SetInstrument(string path)
        {
            var name = System.IO.Path.GetFileNameWithoutExtension(path);
            var root = System.IO.Path.GetDirectoryName(path);
            var configFiles = Directory.GetFiles(root, "*.config");
            if (!configFiles.Any())
            {
                _instrument = null;
                _currentNote = null;
                _instrumentConfigPath = null;
            }

            foreach (string file in configFiles)
            {
                if (_instrumentConfigPath?.ToLower() == file)
                    return;

                var instrument = LoadInstrumentConfig(file);
                var note = instrument.Notes.FirstOrDefault(n => n.Name == name);
                if (note != null)
                {
                    if (_instrumentConfigPath?.ToLower() == file)
                        return;

                    _instrumentConfigPath = file;
                    _instrument = instrument;
                    _currentNote = note;
                    return;
                }
            }

            _instrument = LoadInstrumentConfig(configFiles.First());
            _currentNote = _instrument.Notes.FirstOrDefault(n => n.Name == name);
        }

        private static void SaveEnvelopes(IEnumerable<Envelope> envelopes, string filePath)
        {
            string jsonString = JsonSerializer.Serialize(envelopes);
            File.WriteAllText(filePath, jsonString);
        }

        public static Int16[] GenerateSineWave(double frequency, int sampleRate, float amplitude = 1.0f)
        {
            var numSamples = (int)((1.0 / frequency) * sampleRate * 100.0) + 1;
            numSamples += 100;
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
                var samples = GenerateSineWave(989, 44100);
                _sampleRate = 44100;

                var floatSamples = Array.ConvertAll(samples, s => (float)s / (float)short.MaxValue);

                _complex = SpectrogramProcessor.CalculateSTFT(floatSamples, 1024, 512);
                //_magnitudeGrid = SpectrogramProcessor.ProcessToLogMagnitudes(_complex);
                _magnitudeGrid = SpectrogramProcessor.ProcessToMagnitudes(_complex);
                var bitmap = SpectrogramProcessor.RenderToBitmap(_magnitudeGrid);

                var envelopes = SpectrogramProcessor.ExtractEnvelopes(_magnitudeGrid, (float)samples.Length / (float)_sampleRate);
                var filePath = "c:\\projects\\sounds\\sin.csv";
                var name = System.IO.Path.GetDirectoryName(filePath) + System.IO.Path.DirectorySeparatorChar + (System.IO.Path.GetFileNameWithoutExtension(filePath) ?? "") + ".csv";
                SaveEnvelopes(envelopes, name);

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
                noteCanvas.Visibility = Visibility.Visible;
                message.Visibility = Visibility.Collapsed;
            }
            finally
            {
                // 5. Clean up memory to avoid leaks
                DeleteObject(hBitmap);
            }
        }

        private void noteCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            DisplayNoteLines();
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

            canvas.Children.Remove(envelopeLines);
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

            AddFrequency(mousePosition);
            DisplayNoteLines();
        }

        private void myImage_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Get the precise X and Y coordinates relative to the Canvas
            var mousePosition = e.GetPosition(myImage);
            
            RemoveFrequency(mousePosition);
            DisplayNoteLines();
        }

        private List<Line>? noteLines = new List<Line>();
        private void DisplayNoteLines()
        {
            if (noteLines.Any())
            {
                foreach (var line in noteLines)
                    noteCanvas.Children.Remove(line);
                noteLines.Clear();
            }

            if (_magnitudeGrid == null || _currentNote == null || !_currentNote.Frequencies.Any())
                return;

            var numBuckets = _magnitudeGrid.GetLength(1);
            var freqStep = (_sampleRate / 2) / numBuckets;
            var step = noteCanvas.ActualHeight / numBuckets;

            foreach (var freq in _currentNote.Frequencies)
            {
                var row = noteCanvas.ActualHeight - (step * freq.Index);
                var line = new Line
                {
                    X1 = 0,
                    Y1 = row,
                    X2 = noteCanvas.ActualWidth,
                    Y2 = row,
                    Stroke = System.Windows.Media.Brushes.Yellow,
                    Opacity = 0.7,
                    StrokeThickness = 2
                };

                noteLines.Add(line);
                noteCanvas.Children.Add(line);
            }
        }

        private int _sampleRate = 44100;
        private Polyline? envelopeLines { get; set; }
        private void DisplayDetails(System.Windows.Point mousePos)
        {
            var percentX = (double)mousePos.X / (double)myImage.ActualWidth;
            var percentY = ((double)mousePos.Y / (double)myImage.ActualHeight);

            var canvasWidth = canvas.Width;

            if (envelopeLines != null)
            {
                canvas.Children.Remove(envelopeLines);
                envelopeLines = null; 
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
            envelopeLines = new Polyline();

            // 2. Set the visual properties
            envelopeLines.Stroke = System.Windows.Media.Brushes.Black;
            envelopeLines.StrokeThickness = 2;

            var step = canvas.Width / (_magnitudeGrid.GetLength(0)-1);

            var some = _magnitudeGrid.GetLength(0);
            for (int i = 0; i < _magnitudeGrid.GetLength(0); i++)
            {
                // 3. Create and add your points (X, Y)
                envelopeLines.Points.Add(new System.Windows.Point(step*i, (1.0- (_complex[i][row].Magnitude/_maxAmplitude))*canvas.Height));  
            }

            // 4. Add the polyline to your Canvas
            canvas.Children.Add(envelopeLines);
            //***
        }
        private void RemoveFrequency(System.Windows.Point mousePos)
        {
            if (_currentNote == null)
                return;

            var percentX = (double)mousePos.X / (double)myImage.ActualWidth;
            var percentY = ((double)mousePos.Y / (double)myImage.ActualHeight);

            var canvasWidth = canvas.Width;

            if (_magnitudeGrid == null)
                return;

            var row = (int)((1.0 - percentY) * (_magnitudeGrid.GetLength(1) - 1.0));

            var freq = _currentNote.Frequencies.FirstOrDefault(f => f.Index == row);
            if (freq != null)
                _currentNote.Frequencies.Remove(freq);
        }

        private void AddFrequency(System.Windows.Point mousePos)
        {
            if (_currentNote == null)
                return;

            var percentX = (double)mousePos.X / (double)myImage.ActualWidth;
            var percentY = ((double)mousePos.Y / (double)myImage.ActualHeight);

            var canvasWidth = canvas.Width;

            if (_magnitudeGrid == null)
                return;

            var column = (int)(percentX * (_magnitudeGrid.GetLength(0) - 1.0));
            var row = (int)((1.0 - percentY) * (_magnitudeGrid.GetLength(1) - 1.0));

            if (!_currentNote.Frequencies.Any(f => f.Index == row))
            {
                var numBuckets = _magnitudeGrid.GetLength(1);
                var freqStep = (_sampleRate / 2) / numBuckets;
                var frequency = row * freqStep;

                _currentNote.Frequencies.Add(new Frequency
                {
                    Index = row,
                    Value = frequency
                });
            }
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