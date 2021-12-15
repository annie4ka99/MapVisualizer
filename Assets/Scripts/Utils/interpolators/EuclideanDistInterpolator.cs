using System;
using System.Collections.Generic;
using UnityEngine;

namespace Utils.interpolators
{
    public class EuclideanDistInterpolator : Interpolator
    {
        public void InterpolateGrid(int xSize, int ySize, int[,] contourIds, bool[,] isFilled, 
            Func<int, int, bool> outOfMapBounds, double[,] heights, double[] contourHeights,
            Action<float> updateProgressBar)
        {
            long progress = 0;
            long totalSize = xSize * ySize;

            var progressStep = totalSize / 100;
            long curProgressSteps = 0;
            
            var gridStack = new Dictionary<int, (int, int)>[xSize, ySize];
            for (var i = 0; i < xSize; ++i)
            {
                for (var j = 0; j < ySize; ++j)
                {
                    gridStack[i, j] = new Dictionary<int, (int, int)>();
                }
            }
            
            var processedOnPrevStep = new HashSet<(int,int)>();

            var changed = 0;
            
            for (var i = 0; i < xSize; ++i)
            {
                for (var j = 0; j < ySize; ++j)
                {
                    if (isFilled[i, j])
                    {
                        var (changedNum, _) = FillAround(gridStack,
                            processedOnPrevStep,
                            i, j, 
                            contourIds[i, j],
                            0, 0,
                            xSize, ySize,
                            isFilled,
                            outOfMapBounds);
                        changed += changedNum;
                        progress++;
                        heights[i, j] = -1;
                    }
                }
                if (progress <= (curProgressSteps + 1) * progressStep) continue;
                curProgressSteps = progress / progressStep;
                updateProgressBar((float) progress / totalSize);
            }

            while (changed != 0)
            {
//                Debug.Log((changed));
//                Console.WriteLine(changed);
                
                var processedOnCurStep = new HashSet<(int, int)>();
                changed = 0;

                foreach (var (i,j) in processedOnPrevStep)
                {
                    var curStack = gridStack[i,j];
                    
                    var lineIdWithMinDist = -1;
                    var fwdStepsWithMinDist = 0;
                    var diagStepsWithMinDist = 0;
                    var minDist = Double.MaxValue;
                    foreach (var lId in curStack.Keys)
                    {
                        var (curFwdSteps, curDiagSteps) = curStack[lId];
                        var curDist = CalculateDist(curFwdSteps, curDiagSteps);
                        if (!(curDist < minDist)) continue;
                        minDist = curDist;
                        lineIdWithMinDist = lId;
                        fwdStepsWithMinDist = curFwdSteps;
                        diagStepsWithMinDist = curDiagSteps;
                    }
                    
                    var (changedNum, filledNum) = FillAround(gridStack,
                        processedOnCurStep,
                        i, j,
                        lineIdWithMinDist,
                        fwdStepsWithMinDist, diagStepsWithMinDist,
                        xSize, ySize,
                        isFilled,
                        outOfMapBounds);

                    changed += changedNum;
                    progress += filledNum;
                    if (progress <= (curProgressSteps + 1) * progressStep) continue;
                    curProgressSteps = progress / progressStep;
                    updateProgressBar((float) progress / totalSize);
                }
                    
                processedOnPrevStep = new HashSet<(int, int)>(processedOnCurStep);
            }

            for (var i = 0; i < xSize; ++i)
            {
                for (var j = 0; j < ySize; ++j)
                {
                    if (isFilled[i, j]) continue;
                    
                    var curStack = gridStack[i, j];
                    var lineIds = new List<int>(curStack.Keys);
                    if (lineIds.Count == 0) continue;
                    
                    var line1 = lineIds[0];
                    var line2 = lineIds.Count == 1 ? line1 : lineIds[1];
                    var height1 = contourHeights[line1];
                    var height2 = contourHeights[line2];
                    
                    if (outOfMapBounds(i, j) && height1 <= 0.0 &&  height2 <= 0.0)
                        continue;
                    
                    var (line1FwdSteps, line1DiagSteps) = curStack[line1];
                    var (line2FwdSteps, line2DiagSteps) = curStack[line2];
                    var dist1 = CalculateDist(line1FwdSteps, line1DiagSteps);
                    var dist2 = CalculateDist(line2FwdSteps, line2DiagSteps);
                    var height = CalculateWeightedHeight(
                        contourHeights[line1], dist1,
                        contourHeights[line2], dist2);
                    isFilled[i, j] = true;
                    heights[i, j] = height;
                }
            }
        }

        private (int, int) FillAround(Dictionary<int, (int, int)>[,] gridStack,
            HashSet<(int,int)> processed,
            int x,
            int y,
            int lineInd,
            int fwdSteps,
            int diagSteps,
            int xSize,
            int ySize,
            bool[,] filled,
            Func<int, int, bool> outOfMapBounds
        )
        {
            var changed = 0;
            var filledNum = 0;
            
            var leftBound = Math.Max(x - 1, 0);
            var rightBound = Math.Min(x + 1, xSize - 1);

            var bottomBound = Math.Max(y - 1, 0);
            var upperBound = Math.Min(y + 1, ySize - 1);
            
            for (var i = leftBound; i <= rightBound; i+=1)
            {
                for (var j = bottomBound; j <= upperBound; j+=1)
                {
                    if ((i == x && j == y) || filled[i, j] 
//                                           || outOfMapBounds(i,j)
                                           )
                        continue;
                    
                    var curStack = gridStack[i,j];
                    var newFwdSteps = fwdSteps + (i == x || j == y ? 1 : 0);
                    var newDiagSteps = diagSteps + (i == x || j == y ? 0 : 1);
                    var newDist = CalculateDist(newFwdSteps, newDiagSteps);

                    if (curStack.Count > 1)
                        filledNum++;
                    
                    if (curStack.ContainsKey(lineInd))
                    {
                        var (curForwardSteps, curDiagSteps) = curStack[lineInd];
                        var curDist = CalculateDist(curForwardSteps, curDiagSteps);
                       
                        
                        if (newDist < curDist)
                        {
                            gridStack[i, j][lineInd] = (newFwdSteps, newDiagSteps);
                            processed.Add((i,j));
                            changed += 1;
                        }
                    }
                    else
                    {
                        if (curStack.Count < 2)
                        {
                            gridStack[i, j][lineInd] = (newFwdSteps, newDiagSteps);
                            processed.Add((i,j));
                            changed += 1;
                        }
                        else
                        {
                            var lineIdWithMaxDist = -1;
                            var maxDist = Double.MinValue;
                            foreach (var lId in curStack.Keys)
                            {
                                var (curForwardSteps, curDiagSteps) = curStack[lId];
                                var curDist = CalculateDist(curForwardSteps, curDiagSteps);
                                if (!(curDist > maxDist)) continue;
                                maxDist = curDist;
                                lineIdWithMaxDist = lId;
                            }

                            if (newDist < maxDist)
                            {
                                gridStack[i, j][lineIdWithMaxDist] = (newFwdSteps, newDiagSteps);
                                processed.Add((i,j));
                                changed += 1;
                            }
                        }
                    }
                    
                }
            }

            return (changed, filledNum);
        }

        private static double CalculateDist(int fwdSteps, int diagSteps)
        {
            return fwdSteps + diagSteps * Math.Sqrt(2);
        }
        
        private static double CalculateWeightedHeight(double height1, double dist1, double height2, double dist2)
        {
            return height1 + (dist1 / (dist1 + dist2)) * (height2 - height1);
        }
    }
}