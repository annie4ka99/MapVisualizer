using System;
using System.Collections.Generic;

namespace Utils
{
    public class MapRotator
    {
        public static void Rotate(List<List<(double, double)>> linesCoords)
        {
            var ((upX, upY), (rightX, rightY), (bottomX, bottomY), (leftX, leftY)) = GetMapBounds(linesCoords);

            var (centerX, centerY) = (bottomX, bottomY);
            var xDelta = rightX - bottomX;
            var yDelta = rightY - bottomY;
            var angleTg = yDelta / xDelta;
            var angleCtg = xDelta / yDelta;
            var angleSin = Math.Sqrt(1.0 / (1.0 + Math.Pow(angleCtg, 2)));
            var angleCos = Math.Sqrt(1.0 / (1.0 + Math.Pow(angleTg, 2)));

            foreach (var lineCoords in linesCoords)
            {
                for (var i = 0; i < lineCoords.Count; ++i)
                {
                    var (curX, curY) = lineCoords[i];
                    var x = (curX - centerX) * angleCos + (curY - centerY) * angleSin + centerX;
                    var y = -(curX - centerX) * angleSin + (curY - centerY) * angleCos + centerY;
                    lineCoords[i] = (x, y);
                }
            }
        }
        
        private static ((double, double), (double, double), (double, double), (double, double)) GetMapBounds(
            List<List<(double, double)>> linesCoords)
        {
            var leftX = Double.MaxValue;
            var leftY = 0.0;
            
            var rightX = Double.MinValue;
            var rightY = 0.0;
            
            var bottomY = leftX;
            var bottomX = 0.0;
            
            var upY = rightX;
            var upX = 0.0;
            
            foreach (var lineCoords in linesCoords)
            {
                foreach (var (x, y) in lineCoords)
                {
                    if (x < leftX)
                    {
                        leftX = x;
                        leftY = y;
                    }

                    if (x > rightX)
                    {
                        rightX = x;
                        rightY = y;
                    }

                    if (y < bottomY)
                    {
                        bottomY = y;
                        bottomX = x;
                    }

                    if (y > upY)
                    {
                        upY = y;
                        upX = x;
                    }
                }
            }

            return ((upX, upY), (rightX, rightY), (bottomX, bottomY), (leftX, leftY));
        }
    }
}