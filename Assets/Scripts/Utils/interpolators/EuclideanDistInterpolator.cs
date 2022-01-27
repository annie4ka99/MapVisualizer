using System;
using System.Collections.Generic;
using System.Linq;

namespace Utils.interpolators
{
    public class EuclideanDistInterpolator : Interpolator
    {
        public void InterpolateGrid(int xSize, int ySize, int[,] contourIds, bool[,] isFilled, 
            Func<int, int, bool> outOfMapBounds, double[,] heights, double[] contourHeights,
            Action<float> updateProgressBar)
        {
            const int maxContoursNum = 3;
            
            long progress = 0;
            long totalSize = xSize * ySize * maxContoursNum;

            var progressStep = totalSize / 100;
            long curProgressSteps = 0;
            
            
            var gridStack = new Dictionary<int, (int, int)>[xSize, ySize];
            var processedOnPrevStep = new HashSet<(int,int)>();
            
            for (var i = 0; i < xSize; ++i)
            {
                for (var j = 0; j < ySize; ++j)
                {
                    if (isFilled[i, j])
                    {
                        gridStack[i, j] = null;
                    }
                    else
                    {
                        gridStack[i, j] = new Dictionary<int, (int, int)>(maxContoursNum);
                    }
                }
            }

            

            for (var i = 0; i < xSize; ++i)
            {
                for (var j = 0; j < ySize; ++j)
                {
                    if (!isFilled[i, j]) continue;
                    FillAround(gridStack,
                        processedOnPrevStep,
                        i, j, 
                        contourIds[i, j],
                        0, 0,
                        xSize, ySize,
                        isFilled,
                        outOfMapBounds,
                        maxContoursNum);
                    progress++;
                }
                if (progress <= (curProgressSteps + 1) * progressStep) continue;
                curProgressSteps = progress / progressStep;
                updateProgressBar((float) progress / totalSize);
            }

            while (processedOnPrevStep.Count != 0)
            {

                var processedOnCurStep = new HashSet<(int, int)>();

                foreach (var (i,j) in processedOnPrevStep)
                {
                    var curStack = gridStack[i,j];
               
                    foreach (var lId in curStack.Keys)
                    {
                        var (curFwdSteps, curDiagSteps) = curStack[lId];
                        var filledNum = FillAround(gridStack,
                            processedOnCurStep,
                            i, j,
                            lId,
                            curFwdSteps, curDiagSteps,
                            xSize, ySize,
                            isFilled,
                            outOfMapBounds,
                            maxContoursNum);

                        progress += filledNum;
                    }
                    
                    
                    if (progress <= (curProgressSteps + 1) * progressStep) continue;
                    curProgressSteps = progress / progressStep;
                    updateProgressBar((float) progress / totalSize);
                }
                    
                processedOnPrevStep = processedOnCurStep;
            }

            for (var i = 0; i < xSize; ++i)
            {
                for (var j = 0; j < ySize; ++j)
                {
                    if (isFilled[i, j] || outOfMapBounds(i,j)) continue;
                    
                    var curStack = gridStack[i, j];
                    var lineIds = curStack.Keys.ToArray();
                    
                    if (lineIds.Length == 0) continue;
                    
                    isFilled[i, j] = true;
                    
                    switch (lineIds.Length)
                    {
                        case 1:
                            heights[i, j] = contourHeights[lineIds[0]];
                            break;
                        case 2:
                        {
                            var line1 = lineIds[0];
                            var line2 = lineIds[1];
                            var (line1FwdSteps, line1DiagSteps) = curStack[line1];
                            var (line2FwdSteps, line2DiagSteps) = curStack[line2];
                            var dist1 = CalculateDist(line1FwdSteps, line1DiagSteps);
                            var dist2 = CalculateDist(line2FwdSteps, line2DiagSteps);
                            heights[i, j] = CalculateWeightedHeight(
                                contourHeights[line1], dist1,
                                contourHeights[line2], dist2);
                            break;
                        }
                        default:
                            heights[i, j] = CalculateWeightedHeightFromDict(lineIds, curStack.Values.ToArray(),
                                contourHeights);
                            break;
                    }
                }
            }
        }

        private static int FillAround(Dictionary<int, (int, int)>[,] gridStack,
            HashSet<(int,int)> processed,
            int x,
            int y,
            int lineInd,
            int fwdSteps,
            int diagSteps,
            int xSize,
            int ySize,
            bool[,] filled,
            Func<int, int, bool> outOfMapBounds,
            int maxContoursNum
        )
        {
            var filledNum = 0;
            
            for (var i = x - 1; i <= x + 1; i+=1)
            {
                for (var j = y - 1; j <= y + 1; j+=1)
                {
                    if (i < 0 || i >= xSize ||
                        j < 0 || j >= ySize || 
                        (i == x && j == y) || 
                        filled[i, j] 
                        || outOfMapBounds(i, j)
                        ) continue;
                    
                    var curStack = gridStack[i,j];
                    var newFwdSteps = fwdSteps + (i == x || j == y ? 1 : 0);
                    var newDiagSteps = diagSteps + (i == x || j == y ? 0 : 1);
                    var newDist = CalculateDist(newFwdSteps, newDiagSteps);

                    if (curStack.Count == maxContoursNum)
                        filledNum++;
                    
                    if (curStack.ContainsKey(lineInd))
                    {
                        var (curForwardSteps, curDiagSteps) = curStack[lineInd];
                        var curDist = CalculateDist(curForwardSteps, curDiagSteps);

                        if (!(newDist < curDist)) continue;
                        gridStack[i, j][lineInd] = (newFwdSteps, newDiagSteps);
                        processed.Add((i,j));
                    }
                    else
                    {
                        if (curStack.Count < maxContoursNum)
                        {
                            gridStack[i, j][lineInd] = (newFwdSteps, newDiagSteps);
                            processed.Add((i,j));
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

                            if (!(newDist < maxDist)) continue;
                            gridStack[i, j].Remove(lineIdWithMaxDist);
                            gridStack[i, j][lineInd] = (newFwdSteps, newDiagSteps);
                            processed.Add((i,j));
                        }
                    }
                    
                }
            }

            return filledNum;
        }

        private static double CalculateDist(int fwdSteps, int diagSteps)
        {
            return fwdSteps + diagSteps * Math.Sqrt(2);
        }
        
        private static double CalculateWeightedHeight(double height1, double dist1, double height2, double dist2)
        {
            return height1 + (dist1 / (dist1 + dist2)) * (height2 - height1);
        }
        
        private static double CalculateWeightedHeightFromDict(int[] lineIds, (int, int)[] steps,
            double[] contourHeights)
        {
            double sumDist = 0;
            double height = 0;
            double weightsSum = 0;
            var contoursNum = lineIds.Length;
            var distances = new double[contoursNum];

            for (var i = 0; i < contoursNum; ++i)
            {
                distances[i] = CalculateDist(steps[i].Item1, steps[i].Item2);
                sumDist += distances[i];
            }

            for (var i = 0; i < contoursNum; ++i)
            {
                var weight = sumDist / distances[i];
                height += contourHeights[lineIds[i]] * weight;
                weightsSum += weight;
            }

            return height / weightsSum;
        }
    }
}