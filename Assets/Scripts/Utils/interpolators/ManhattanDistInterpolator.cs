using System;
using System.Collections.Generic;

namespace Utils.interpolators
{
    public class ManhattanDistInterpolator : Interpolator
    {
        public void InterpolateGrid(int xSize, int ySize, int[,] contourIds, bool[,] isFilled, 
            Func<int, int, bool> outOfMapBounds, double[,] heights, double[] contourHeights,
            Action<float> updateProgressBar)
        {
            // Number of processed cells
            long progress = 0;
            
            // Total number of cells
            long totalSize = xSize * ySize;

            var progressStep = totalSize / 100;
            long curProgressSteps = 0;
 
            // Cell processing order
            var q = new Queue<(int, int)>();
            var qSet = new HashSet<(int,int)>();

            var cellStatuses = new CellStatus[xSize, ySize];
            
            // For every cell (1st closest contour id, distance) pair
            var closestContour1 = new (int, int)[xSize, ySize];
 
            // For every cell (2nd closest contour id, distance) pair
            var closestContour2 = new (int, int)[xSize, ySize];
 
            // Initialization
            for (var i = 0; i < xSize; ++i)
            {
                for (var j = 0; j < ySize; ++j)
                {
                    closestContour1[i, j] = (contourIds[i, j], 0);
                    closestContour2[i, j] = (contourIds[i, j], 0);
                    cellStatuses[i, j] = isFilled[i, j] ? CellStatus.Contour : CellStatus.Empty;
                    if (!isFilled[i, j]) continue;
                    q.Enqueue((i, j));
                    qSet.Add((i, j));
                    progress++;
                }
                if (progress <= (curProgressSteps + 1) * progressStep) continue;
                curProgressSteps = progress / progressStep;
                updateProgressBar((float) progress / totalSize);
            }

            while (q.Count > 0)
            {
                
                    progress += Process(qSet, q, closestContour1, closestContour2,
                        cellStatuses, xSize, ySize, outOfMapBounds);
                    
                if (progress <= (curProgressSteps + 1) * progressStep) continue;
                curProgressSteps = progress / progressStep;
                updateProgressBar((float) progress / totalSize);
            }

            // Evaluate heights
            for (var i = 0; i < xSize; ++i) 
            {
                for (var j = 0; j < ySize; ++j) 
                {
                    if (isFilled[i, j] || cellStatuses[i,j] == CellStatus.Empty)
                        continue;
                    
                    progress++; 

                    var (contour1, dist1) = closestContour1[i, j];

                    var (contour2, dist2) = closestContour2[i, j];
                    var height1 = contourHeights[contour1];
                    var height2 = contourHeights[contour2];

                    // If second contour didn't reach cell, then it's isolated inside a single contour and has height of this contour.
                    // Else it has weighted average height of two closest contours.
                    if (dist2 == 0)
                    {
                        heights[i, j] = height1;
                    }
                    else
                    {
                        heights[i, j] = CalculateWeightedHeight(
                            height1,
                            dist1,
                            height2,
                            dist2);
                    }
                    
                    isFilled[i, j] = true;
                }
                if (progress <= (curProgressSteps + 1) * progressStep) continue;
                curProgressSteps = progress / progressStep;
                updateProgressBar((float) progress / totalSize);
            }
        }
        

        // Process adjacent cells. Returns number of filled cells.
        private static int Process(HashSet<(int, int)> qSet,
            Queue<(int, int)> q, 
            (int, int)[,] closestContours1, (int, int)[,] closestContours2, 
            CellStatus[,] cellStatuses, int xSize, int ySize, Func<int, int, bool> outOfMapBounds)
        {
            var progress = 0;
 
            var (curX, curY) = q.Dequeue();
            qSet.Remove((curX, curY));
            
            var curStatus = cellStatuses[curX, curY];
            
            var (contour1, distance1) = closestContours1[curX, curY];
            var (contour2, distance2) = closestContours2[curX, curY];
            // Adjacent cells coordinates
            var cells = new List<(int, int)>
            {
                (curX + 1, curY),
                (curX - 1, curY),
                (curX, curY + 1),
                (curX, curY - 1)
            };
            
            foreach (var (x, y) in cells)
            {
                if (x < 0 || x >= xSize || y < 0 || y >= ySize 
                    || cellStatuses[x,y] == CellStatus.Contour
                    || (outOfMapBounds(x, y))) continue;
                var cellStatus = cellStatuses[x, y];
                var (cellContour1, cellDistance1) = closestContours1[x, y];
                var (cellContour2, cellDistance2) = closestContours2[x, y];

                if (cellStatus == CellStatus.Done)
                {
                    ChangeContours(x, y, cellContour1, cellContour2, cellDistance1, cellDistance2, 
                        contour1, distance1 + 1,
                        closestContours1, closestContours2, qSet, q);
                    if (curStatus == CellStatus.Done)
                    {
                        ChangeContours(x, y, cellContour1, cellContour2, cellDistance1, cellDistance2, 
                            contour2, distance2 + 1,
                            closestContours1, closestContours2, qSet, q);
                    }
                    continue;
                }
                
                if (curStatus == CellStatus.Wait || curStatus == CellStatus.Contour)
                {
                    
                    if (cellStatus == CellStatus.Empty)
                    {
                        closestContours1[x, y] = (contour1, distance1 + 1);
                        cellStatuses[x, y] = CellStatus.Wait;
                        if (qSet.Contains((x, y))) continue;
                        qSet.Add((x, y));
                        q.Enqueue((x, y));
                    }
                    else if (cellContour1 != contour1)
                    {
                        closestContours2[x, y] = (contour1, distance1 + 1);
                        cellStatuses[x, y] = CellStatus.Done;
                        progress++;
                        if (qSet.Contains((x, y))) continue;
                        qSet.Add((x, y));
                        q.Enqueue((x, y));
                    }
                    else if (distance1 + 1 < cellDistance1) {
                        closestContours1[x, y] = (contour1, distance1 + 1);
                        if (qSet.Contains((x, y))) continue;
                        qSet.Add((x, y));
                        q.Enqueue((x, y));
                    }
                }
                else
                {
                    if (cellStatus == CellStatus.Empty)
                    {
                        closestContours1[x, y] = (contour1, distance1 + 1);
                        closestContours2[x, y] = (contour2, distance2 + 1);
                        cellStatuses[x, y] = CellStatus.Done;
                        progress++;
                        if (qSet.Contains((x, y))) continue;
                        qSet.Add((x, y));
                        q.Enqueue((x, y));
                    }
                    else
                    {
                        if (cellContour1 == contour1 && distance1 + 1 < cellDistance1)
                        {
                            closestContours1[x, y] = (contour1, distance1 + 1);
                            if (!qSet.Contains((x, y)))
                            {
                                qSet.Add((x, y));
                                q.Enqueue((x, y));
                            }
                        }
                        if (cellContour1 == contour2 && distance2 + 1 < cellDistance1)
                        {
                            closestContours1[x, y] = (contour2, distance2 + 1);
                            if (!qSet.Contains((x, y)))
                            {
                                qSet.Add((x, y));
                                q.Enqueue((x, y));
                            }
                        }
                        if (cellContour1 != contour1 && (cellContour1 == contour2 || distance1 <= distance2))
                        {
                            closestContours2[x, y] = (contour1, distance1 + 1);
                            cellStatuses[x, y] = CellStatus.Done;
                            progress++;
                            if (qSet.Contains((x, y))) continue;
                            qSet.Add((x, y));
                            q.Enqueue((x, y));
                        }
                        else if (cellContour1 != contour2)
                        {
                            closestContours2[x, y] = (contour2, distance2 + 1);
                            cellStatuses[x, y] = CellStatus.Done;
                            progress++;
                            if (qSet.Contains((x, y))) continue;
                            qSet.Add((x, y));
                            q.Enqueue((x, y));
                        }
                    }
                }
            }
 
            return progress;
        }
        
        private static double CalculateWeightedHeight(double height1, double dist1, double height2, double dist2)
        {
            return height1 + (dist1 / (dist1 + dist2)) * (height2 - height1);
        }


        private static void ChangeContours(int x, int y,
            int cellContour1, int cellContour2, 
            int cellDistance1, int cellDistance2,
            int newContour, int newDist,
            (int, int)[,] closestContours1, (int, int)[,] closestContours2,
            HashSet<(int, int)> qSet, Queue<(int,int)> q
            )
        {
            if (cellContour1 == newContour && newDist < cellDistance1 ||
                cellContour1 != newContour && cellContour2 != newContour && 
                newDist < cellDistance1 && cellDistance1 >= cellDistance2)
            {
                closestContours1[x, y] = (newContour, newDist);
                if (qSet.Contains((x, y))) return;
                qSet.Add((x, y));
                q.Enqueue((x, y));
            } else if (cellContour2 == newContour && newDist < cellDistance2 ||
                       cellContour1 != newContour && cellContour2 != newContour && 
                       newDist < cellDistance2 && cellDistance1 < cellDistance2)
            {
                closestContours2[x, y] = (newContour, newDist);
                if (qSet.Contains((x, y))) return;
                qSet.Add((x, y));
                q.Enqueue((x, y));
            }
        }
    }
}