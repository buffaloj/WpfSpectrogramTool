
using static SpectrogramTool.Wpf.WesternNotation;

namespace SpectrogramTool.Wpf
{
    public static class Extensions
    {
        public static void AddNote(this List<NoteConfig> notes, string name)
        {
            notes.Add(new NoteConfig { Name = name });
        }
    }

    public class Instrument
    {
        public string Name { get; set; }
        public IEnumerable<NoteConfig> Notes { get; set; }
    }

    public class NoteConfig
    {
        public string Name { get; set; }
        public float Length { get; set; }
        public Notes? NoteIndex { get; set; }
        public int? Octave { get; set; }

        public IList<Frequency> Frequencies { get; set; } = new List<Frequency>();
    }

    public class Frequency
    {
        public int Index { get; set; }
        public float Freq { get; set; }
        public int? FundamentalMult { get; set; }
    }
}
