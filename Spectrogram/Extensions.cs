namespace Spectrogram
{
    public static class Extensions
    {
        public static float[] GetRow(this float[,] magnitudeGrid, int row)
        {
            return Enumerable.Range(0, magnitudeGrid.GetLength(0))
                         .Select(t => magnitudeGrid[t, row]).ToArray();
        }
    }
}
