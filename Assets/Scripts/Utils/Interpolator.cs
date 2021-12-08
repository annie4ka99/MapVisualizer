using System;

namespace Utils
{
    public interface Interpolator
    {
        void InterpolateGrid(int xSize, int ySize, int[,] closestContourIds, bool[,] isFilled, 
            Func<int, int, bool> outOfMapBounds, double[,] heights, double[] contourHeights);
    }
}