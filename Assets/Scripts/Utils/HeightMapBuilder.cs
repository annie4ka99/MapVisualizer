using System;
using System.Collections.Generic;

namespace Utils
{
    public class HeightMapBuilder
    {
        private readonly int _xSize;
        private readonly int _ySize;
        
        private bool[,] _isFilled;
        private double[,] _heights;
        private int[,] _closestContourIds;
        private double[] _contourHeights;
        
        private int[] _xLeftBounds;
        private int[] _xRightBounds;
        private int[] _yBottomBounds;
        private int[] _yUpperBounds;
        
        public HeightMapBuilder(int xSize, int ySize)
        {
            _xSize = xSize;
            _ySize = ySize;
        }
        
        
        public (double[,], bool[,]) Build(List<double> heights, 
            List<List<(double, double)>> linesCoords, 
            Interpolator interpolator, Action<float> updateProgress)
        {
            _contourHeights = heights.ToArray();
            
            var (minX, maxX, minY, maxY) = GetCoordsBounds(linesCoords);

            FillGridFromContours(minX, maxX, minY, maxY, linesCoords);

            FillAxesBounds();

            interpolator.InterpolateGrid(_xSize, _ySize, _closestContourIds, _isFilled, 
                OutOfMapBounds, _heights, _contourHeights, updateProgress);
            
            return (_heights, _isFilled);

        }
        
        private static (double, double, double, double) GetCoordsBounds(List<List<(double, double)>> linesCoords)
        {
            var minX = Double.MaxValue;
            var maxX = Double.MinValue;
            var minY = minX;
            var maxY = maxX;
            foreach (var lineCoords in linesCoords)
            {
                foreach (var (x, y) in lineCoords)
                {
                    if (x < minX)
                        minX = x;
                    if (x > maxX)
                        maxX = x;
                    if (y < minY)
                        minY = y;
                    if (y > maxY)
                        maxY = y;
                }
            }

            return (minX, maxX, minY, maxY);
        }
        
        private void FillAxesBounds()
        {
            _xLeftBounds = new int[_ySize];
            _xRightBounds = new int[_ySize];
            _yBottomBounds = new int[_xSize];
            _yUpperBounds = new int[_xSize];

            for (var i = 0; i < _ySize; ++i)
            {
                _xLeftBounds[i] = _xSize;
                _xRightBounds[i] = 0;
            }
            for (var i = 0; i < _xSize; ++i)
            {
                _yBottomBounds[i] = _ySize;
                _yUpperBounds[i] = 0;
            }

            for (var x = 0; x < _xSize; ++x)
                for (var y = 0; y < _ySize; ++y)
                {
                    if (!_isFilled[x, y]) continue;
                    if (x < _xLeftBounds[y])
                    {
                        _xLeftBounds[y] = x;
                    }
                    if (x > _xRightBounds[y])
                    {
                        _xRightBounds[y] = x;
                    }

                    if (y < _yBottomBounds[x])
                    {
                        _yBottomBounds[x] = y;
                    }

                    if (y > _yUpperBounds[x])
                    {
                        _yUpperBounds[x] = y;
                    }
                }
        }


        private bool OutOfMapBounds(int x, int y)
        {
            var outOfX = x < _xLeftBounds[y] || x > _xRightBounds[y];
            var outOfY = y < _yBottomBounds[x] || y > _yUpperBounds[x];
            return outOfX && outOfY;
        }
        
        private void FillGridFromContours(
            double minX, double maxX, double minY, double maxY,
            List<List<(double, double)>> linesCoords)
        {
            var xStep = (maxX - minX) / (_xSize);
            var yStep = (maxY - minY) / (_ySize);
            
            _isFilled = new bool[_xSize, _ySize];
            for (var i = 0; i < _xSize; ++i)
                for (var j = 0; j < _ySize; ++j)
                    _isFilled[i, j] = false;
            
            _heights = new double[_xSize,_ySize];
            _closestContourIds = new int[_xSize,_ySize];
            
            for (var lineInd = 0; lineInd < linesCoords.Count; ++lineInd)
            {
                var prevXInd = -1;
                var prevYInd = -1;

                for (var coordNum = 0; coordNum < linesCoords[lineInd].Count; coordNum++)
                {
                    var (curX, curY) = linesCoords[lineInd][coordNum];
                    
                    var curXInd = Convert.ToInt32(Math.Floor((curX - minX) / xStep));
                    if (curXInd == _xSize)
                        curXInd = _xSize - 1;
                    var curYInd = Convert.ToInt32(Math.Floor((curY - minY) / yStep));
                    if (curYInd == _ySize)
                        curYInd = _ySize - 1;
                    
                    _isFilled[curXInd,curYInd] = true;
                    _heights[curXInd, curYInd] = _contourHeights[lineInd];
                    _closestContourIds[curXInd, curYInd] = lineInd;
                    if (coordNum != 0)
                    {
                        var (prevX, prevY) = linesCoords[lineInd][coordNum - 1];
                        
                        FillGridBetweenLinesEnds(
                            minX, minY,
                            xStep, yStep,
                            prevXInd, curXInd,
                            prevYInd, curYInd,
                            prevX, curX,
                            prevY, curY,
                            lineInd, _contourHeights[lineInd]
                            );
                    }

                    prevXInd = curXInd;
                    prevYInd = curYInd;
                }
            }
        }

        private void FillGridBetweenLinesEnds(
            double minX, double minY,
            double xStep, double yStep,
            int startXInd, int endXInd, 
            int startYInd, int endYInd,
            double startX, double endX,
            double startY, double endY,
            int fillLineInd, double fillHeight)
        {
            var xIndStep = 1;
            if (startXInd > endXInd)
            {
                xIndStep = -1;
            }
            
            var yIndStep = 1;
            if (startYInd > endYInd)
            {
                yIndStep = -1;
            }

            const double eps = 0.000001;

            var fillStartXInd = Math.Min(_xSize - 1, Math.Max(0, startXInd - xIndStep));
            var fillEndXInd =  Math.Min(_xSize - 1, Math.Max(0, endXInd + xIndStep));
            var fillStartYInd =  Math.Min(_ySize - 1, Math.Max(0,  startYInd - yIndStep));
            var fillEndYInd =  Math.Min(_ySize - 1, Math.Max(0,  endYInd + yIndStep));
            

            for (var xInd = fillStartXInd; xInd != fillEndXInd; xInd += xIndStep)
            {
                for (var yInd = fillStartYInd; yInd != fillEndYInd; yInd += yIndStep)
                {
                    if (xInd == startXInd && yInd == startYInd ||
                        xInd == endXInd && yInd == endYInd)
                        continue;
                    var xLeftBound = minX + xInd * xStep;
                    var xRightBound = xLeftBound + xStep;
                    var yBottomBound = minY + yInd * yStep;
                    var yUpperBound = yBottomBound + yStep;

                    var k = (endY - startY) / (endX - startX);
                    var kInv = (endX - startX) / (endY - startY);
                    
                    var yAtLeftBound = k * (xLeftBound - startX) + startY;
                    var yAtRightBound = k * (xRightBound - startX) + startY;
                    var xAtBottomBound = kInv * (yBottomBound - startY) + startX;
                    var xAtUpperBound = kInv * (yUpperBound - startY) + startX;

                    if ((yAtLeftBound >= yBottomBound - eps && yAtLeftBound <= yUpperBound + eps) ||
                        (yAtRightBound >= yBottomBound - eps && yAtRightBound <= yUpperBound + eps) ||
                        (xAtBottomBound >= xLeftBound - eps && xAtBottomBound <= xRightBound + eps) ||
                        (xAtUpperBound >= xLeftBound - eps && xAtUpperBound <= xRightBound + eps))
                    {
                        _isFilled[xInd, yInd] = true;
                        _heights[xInd, yInd] = fillHeight;
                        _closestContourIds[xInd, yInd] = fillLineInd;
                    }
                }
            }
        }

    }
}