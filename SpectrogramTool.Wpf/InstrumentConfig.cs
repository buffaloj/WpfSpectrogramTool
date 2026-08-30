
namespace SpectrogramTool.Wpf
{
    public static class Extensions
    {
        public static void AddNote(this List<Note> notes, string name)
        {
            notes.Add(new Note { Name = name });
        }
    }

    public class Instrument
    {
        public string Name { get; set; }
        public IEnumerable<Note> Notes { get; set; }
    }

    public class Note
    {
        public string Name { get; set; }
        public float Length { get; set; }
        public IList<Frequency> Frequencies { get; set; } = new List<Frequency>();
    }

    public class Frequency
    {
        public int Index { get; set; }
        public float Value { get; set; }
    }
}
