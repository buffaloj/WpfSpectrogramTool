namespace SpectrogramTool.Wpf
{
    public class EnvelopeSound
    {
        public int? NoteIndex { get; set; }
        public int? Octave { get; set; }
        public IEnumerable<Envelope> Envelopes { get; set; }
    }

    public class Envelope
    {
        public float? Frequency { get; set; }
        public int? FundamentalMult { get; set; }
        public float Length { get; set; }
        public float[] Amplitudes { get; set; }
    }
}
