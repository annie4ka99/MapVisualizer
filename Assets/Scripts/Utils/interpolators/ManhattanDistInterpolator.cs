using System;
using System.Collections.Generic;
using UnityEngine;

namespace Utils.interpolators
{
    public class ManhattanDistInterpolator : Interpolator
    {
        public void InterpolateGrid(int xSize, int ySize, int[,] closestContourIds, bool[,] isFilled, 
            Func<int, int, bool> outOfMapBounds, double[,] heights, double[] contourHeights)
        {
            // Number of processed cells
            long progress = 0;
            
            // Total number of cells
            long totalSize = xSize * ySize;
 
            // Cell processing order
            var q = new Queue<(int, int)>();
 
            // FIRST TRAVERSAL
            // For every cell (closest contour id, distance) pair
            var closestContour1 = new (int, int)[xSize, ySize];
            // For every cell contains 'true' if there is final answer for it in closestContourIds1
            var cellStatuses = new CellStatus[xSize, ySize];
 
            // SECOND TRAVERSAL
            // For every cell (closest contour id, distance) pair
            var closestContour2 = new (int, int)[xSize, ySize];
            // For every cell contains 'true' if there is final answer for it in closestContourIds2
 
            // Initialization
            for (var i = 0; i < xSize; ++i)
            {
                for (var j = 0; j < ySize; ++j)
                {
                    closestContour1[i, j] = (closestContourIds[i, j], 0);
                    closestContour2[i, j] = (closestContourIds[i, j], 0);
                    cellStatuses[i, j] = isFilled[i, j] ? CellStatus.Done : CellStatus.Empty;
                    if (isFilled[i, j]) 
                    {
                        q.Enqueue((i, j));
                        progress++;
                    }
                    if (outOfMapBounds(i, j))
                    {
                        cellStatuses[i, j] = CellStatus.Done;
                        progress++;
                    }
                }
            }


            while (q.Count > 0)
            {
                progress += Process(q, closestContour1, closestContour2, 
                    cellStatuses, xSize, ySize);
            }
            
            // Evaluate heights
            
            Debug.Log("Evaluating heights");
            Console.WriteLine("Evaluating heights");
            for (var i = 0; i < xSize; ++i) 
            {
                for (var j = 0; j < ySize; ++j) 
                {
                   // -1 is special height value for contour cells to colour them blue
                    if (isFilled[i, j])
                    {
                        heights[i, j] = -1;
                        continue;
                    }
                    if (outOfMapBounds(i, j) || isFilled[i, j]) continue;
                    isFilled[i, j] = true;
                    
                    // If second traversal didn't reach cell, then it's isolated inside a single contour and has height of this contour.
                    // Else it has weighted average height of two closest contours.
                    if (closestContour2[i, j].Item2 == 0)
                    {
                        heights[i, j] = contourHeights[closestContour1[i, j].Item1];
                    }
                    else 
                    {
                        heights[i, j] = CalculateWeightedHeight(
                            contourHeights[closestContour1[i, j].Item1],
                            closestContour1[i, j].Item2,
                            contourHeights[closestContour2[i, j].Item1],
                            closestContour2[i, j].Item2);
                    }
                }
            }
        }

        // Process adjacent cells. Filters data that can be written to a cell. Returns number of filled cells.
        private static int Process(Queue<(int, int)> q, (int, int)[,] closestContours1, (int, int)[,] closestContours2, 
            CellStatus[,] cellStatuses, int xSize, int ySize)
        {
            var progress = 0;
 
            var (curX, curY) = q.Dequeue();
            var curStatus = cellStatuses[curX, curY];
            
            var (contour1, distance1) = closestContours1[curX, curY];
            var (contour2, distance2) = closestContours2[curX, curY];
            // Adjacent cells coordinates
            var cells = new List<(int, int)>()
            {
                (curX + 1, curY),
                (curX - 1, curY),
                (curX, curY + 1),
                (curX, curY - 1)
            };
 
            foreach (var (x, y) in cells)
            {
                if (x >= 0 && x < xSize && y >= 0 && y < ySize && cellStatuses[x, y] != CellStatus.Done)
                {
                    var cellStatus = cellStatuses[x, y];
                    if (curStatus == CellStatus.Wait)
                    {
                        if (cellStatus == CellStatus.Empty)
                        {
                            closestContours1[x, y] = (contour1, distance1 + 1);
                            cellStatuses[x, y] = CellStatus.Wait;
                            q.Enqueue((x, y));
                            progress++;
                        }
                        else if (closestContours1[x, y].Item1 != contour1)
                        {
                            closestContours2[x, y] = (contour1, distance1 + 1);
                            cellStatuses[x, y] = CellStatus.Done;
                            q.Enqueue((x, y));
                            progress++;
                        }
                    }
                    else
                    {
                        if (cellStatus == CellStatus.Empty)
                        {
                            closestContours1[x, y] = (contour1, distance1 + 1);
                            closestContours2[x, y] = (contour2, distance2 + 1);
                            cellStatuses[x, y] = CellStatus.Done;
                            q.Enqueue((x, y));
                            progress++;
                        }
                        else
                        {
                            var (cellContour, _) = closestContours1[x, y];
                            if (cellContour != contour1)
                            {
                                closestContours2[x, y] = (contour1, distance1 + 1);
                                cellStatuses[x, y] = CellStatus.Done;
                                q.Enqueue((x, y));
                                progress++;
                            }
                            else if (cellContour != contour2)
                            {
                                closestContours2[x, y] = (contour2, distance2 + 1);
                                cellStatuses[x, y] = CellStatus.Done;
                                q.Enqueue((x, y));
                                progress++;
                            }
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
    }
}