using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Utils;

public class ImageBuilder : MonoBehaviour
{
    void Start()
    {
        const int width = 10000;
        const int height = 10000;
        
        var contourLines = Utils.ContourLinesReader.ReadMetricContourLines(0);
        var (heights, linesCoords) = contourLines;
        var heightMapBuilder = new HeightMapBuilder(width, height);
        var (filledHeights, filled) = heightMapBuilder.Build(heights, linesCoords);

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
                    var color = Color.Lerp(Color.green, Color.red, 
                        (float) (filledHeights[x,y]/heightScale));
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
