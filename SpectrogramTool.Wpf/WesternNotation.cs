using System.Text.RegularExpressions;

namespace SpectrogramTool.Wpf
{
    public static class WesternNotation
    {
        public static int NotesPerOctave = Enum.GetNames(typeof(Notes)).Length;

        public enum Notes
        {
            C = 0,
            Db,
            D,
            Eb,
            E,
            F,
            Gb,
            G,
            Ab,
            A,
            Bb,
            B
        }

        public static int N(Notes note, int octave) => (int)note + (octave *  NotesPerOctave);

        public static class Pitch
        {
            public static double Stuttgart = 440;
            public static double Baroque = 415;
            public static double Classical = 430;
            public static double Modern = 444;
            public static double Scientific = 432;
        }

        static int N0 = N(Notes.A, 4);

        public static double F(Notes note, int octave, double pitch) => pitch * Math.Pow(2, (double)(N(note, octave) - N0) / (double)NotesPerOctave);

        public static Tuple<Notes, int> ToWesternNoteIndex(this string name)
        {
            string pattern = @"^(?<note>[A-G][b#]?)(?<octave>\d+)$";

            Match match = Regex.Match(name, pattern);

            if (match.Success)
            {
                string noteName = match.Groups["note"].Value;
                string octaveName = match.Groups["octave"].Value;

                if (!Enum.TryParse(noteName, out Notes note) || !int.TryParse(octaveName, out var octave))
                    return null;

                return new Tuple<Notes, int>(note, octave);
            }

            return null;
        }

        public static int? GetFundamentalMult(double freq, Notes note, int octave, double basisPitch)
        {
            var fundamentalFreq = F(note, octave, basisPitch);
            if (freq <= 0.0)
                return null;

            if (freq < fundamentalFreq)
                return 1;

            var some =  (int)Math.Round(freq / fundamentalFreq, 0);
            return some;
        }
    }

}
