using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Utils;
using Utils.interpolators;

public class ImageBuilder : MonoBehaviour
{
    void Start()
    {
        const int width = 10000;
        const int height = 10000;
        
        var contourLines = Utils.ContourLinesReader.ReadMetricContourLines(0);
        var (heights, linesCoords) = contourLines;

        MapRotator.Rotate(linesCoords);
        
        var heightMapBuilder = new HeightMapBuilder(width, height);
        
//        Interpolator interpolator = new EuclideanDistInterpolator();
        Interpolator interpolator = new ManhattanDistInterpolator();
        var (filledHeights, filled) = heightMapBuilder.Build(heights, linesCoords, interpolator);

        var minHeight = heights.Min();
        var maxHeight = heights.Max();
        var heightScale = maxHeight - minHeight;
        
        Texture2D texture = new Texture2D(width, height);
        GetComponent<Image>().material.mainTexture = texture;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (filled[x, y])
                {
                    Color color;
                    // color contours blue
                    if (filledHeights[x, y] == -1.0)
                    {
                        color = Color.blue;
                        texture.SetPixel(x, y, color);
                        continue;
                    }
                    color = Color.Lerp(Color.green, Color.red, 
                        (float) ((filledHeights[x,y] - minHeight)/heightScale));
                    texture.SetPixel(x, y, color);
                }
                else
                {
                    var color = Color.white;
                    texture.SetPixel(x, y, color);
                }
            }
        }
        texture.Apply();
    }
}
