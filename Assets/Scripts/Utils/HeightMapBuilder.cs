using System;
using System.Collections.Generic;

namespace Utils
{
    public class HeightMapBuilder
    {
        private readonly int _xSize;
        private readonly int _ySize;
        
        private bool[,] _filled;
        private double[,] _filledHeights;
        private int[,] _filledLinesInds;
        private double[] _heights;
        
        
        public HeightMapBuilder(int xSize, int ySize)
        {
            _xSize = xSize;
            _ySize = ySize;
        }
        
        
        public (double[,], bool[,]) Build(List<double> heights, List<List<(double, double)>> linesCoords)
        {
            _heights = heights.ToArray();
            
            var (minX, maxX, minY, maxY) = GetCoordsBounds(linesCoords);

            FillGridFromContours(minX, maxX, minY, maxY, linesCoords);

            return (_filledHeights, _filled);

        }
        
        private (double, double, double, double) GetCoordsBounds(List<List<(double, double)>> linesCoords)
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


        private void FillGridFromContours(
            double minX, double maxX, double minY, double maxY,
            List<List<(double, double)>> linesCoords)
        {
            var xStep = (maxX - minX) / (_xSize);
            var yStep = (maxY - minY) / (_ySize);
            
            _filled = new bool[_xSize, _ySize];
            for (var i = 0; i < _xSize; ++i)
                for (var j = 0; j < _ySize; ++j)
                    _filled[i, j] = false;
            
            _filledHeights = new double[_xSize,_ySize];
            _filledLinesInds = new int[_xSize,_ySize];
            
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
                    
                    _filled[curXInd,curYInd] = true;
                    _filledHeights[curXInd, curYInd] = _heights[lineInd];
                    _filledLinesInds[curXInd, curYInd] = lineInd;
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
                            lineInd, _heights[lineInd]
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
                        _filled[xInd, yInd] = true;
                        _filledHeights[xInd, yInd] = fillHeight;
                        _filledLinesInds[xInd, yInd] = fillLineInd;
                    }
                }
            }
        }
        
        /* unfinished method, not used */
        private void InterpolateGrid()
        {
            var gridStack = new Dictionary<int, (int, int)>[_xSize, _ySize];
            for (var i = 0; i < _xSize; ++i)
            {
                for (var j = 0; j < _ySize; ++j)
                {
                    gridStack[i, j] = new Dictionary<int, (int, int)>();
                }
            }
            
            for (var i = 0; i < _xSize; ++i)
            {
                for (var j = 0; j < _ySize; ++j)
                {
                    if (_filled[i, j])
                    {
                        FillAround(gridStack, 
                            i, j, 
                            _filledLinesInds[i, j], _filledHeights[i, j],
                            0, 0);    
                    }
                }
            }


            bool allFilled = false;
            while (!allFilled)
            {
                for (var i = 0; i < _xSize; ++i)
                {
                    for (var j = 0; j < _ySize; ++j)
                    {
                        if (!_filled[i, j])
                        {
                            
                        }
                    }
                }
                
            }
        }

        /* unfinished method, not used */
        private void FillAround(Dictionary<int, (int, int)>[,] gridStack,
            int x,
            int y,
            int lineInd,
            double height,
            int forwardSteps,
            int diagSteps
        )
        {
            var leftBound = Math.Max(x - 1, 0);
            var rightBound = Math.Min(x + 1, _xSize - 1);

            var bottomBound = Math.Max(y - 1, 0);
            var upperBound = Math.Max(y + 1, _ySize - 1);
            
            for (int i = leftBound; i <= rightBound; i+=1)
            {
                for (int j = bottomBound; j <= upperBound; j+=1)
                {
                    if (i == x && j == y)
                        continue;
                    var curStack = gridStack[i,j];
                    if (curStack.Count < 2)
                    {
                        if (curStack.ContainsKey(lineInd))
                        {
                            var (curForwardStep, curDiagStep) = curStack[lineInd];
                        }
                        else
                        {
                            
                        }
                    }
                }
            }
        }
    }
}