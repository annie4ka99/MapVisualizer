//using UnityEngine;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Utils;
using Utils.interpolators;
using Debug = System.Diagnostics.Debug;

namespace Editor
{
    public class MapEditorWindow : EditorWindow
    {
        private const string ChooseFileButton = "select map file";
        private const string DrawMapButton = "Draw map";

        private const string ProgressBarTitle = "Draw map";
        private const string ProgressBarMessage = "Drawing map...";
            
        private string _filePath = "";
        
        private int _width = 10000;
        private int _height = 10000;

        private InterpolatorTypes _interpolatorType = InterpolatorTypes.Manhattan;
        
        private List<double> _contoursHeights;
        private List<List<(double, double)>> _contoursCoords;
        
        [MenuItem("Map/Map Editor")]
        private static void Init()
        {
            // Get existing open window or if none, make a new one:
            var window = (MapEditorWindow)EditorWindow.GetWindow(typeof(MapEditorWindow));
            window.Show();
        }

        private void OnGUI()
        {
            _width = EditorGUILayout.IntField("Width:", _width);
            _height = EditorGUILayout.IntField("Height:", _height);
            _interpolatorType = (InterpolatorTypes) EditorGUILayout.EnumPopup("Interpolator type", _interpolatorType);
            
            if (GUILayout.Button(ChooseFileButton))
            {
                _filePath = EditorUtility.OpenFilePanel("select json file with contours coordinated", 
                    "", 
                    "json");
            }
            
            EditorGUILayout.LabelField("Selected file:", _filePath);

            if (GUILayout.Button(DrawMapButton))
            {
                if (_filePath == "")
                {
                    EditorUtility.DisplayDialog("Draw map", "You must select a file first!", "OK");
                    return;
                }
                if (_width <= 0 || _height <= 0)
                {
                    EditorUtility.DisplayDialog("Draw map", "Width and height must be positive", "OK");
                    return;
                }
                ReadContours();
                DrawMap();
                EditorUtility.ClearProgressBar();
            }
        }
        
        private void ReadContours()
        {
            (_contoursHeights, _contoursCoords) = ContourLinesReader.ReadMetricContourLines(_filePath);
        }

        private void DrawMap()
        {
            ShowProgressBar(0f);
            
            if (_contoursCoords == null || _contoursHeights == null)
            {
                ReadContours();
            }
            
            var images = FindObjectsOfType<RawImage>();
            if (images.Length == 0)
            {
                EditorUtility.DisplayDialog("Error", "Didn't find any image", "OK");
                return;
            }
            
            GameObject.Find("Main Camera").transform.position = new Vector3(0, 0, 
                -(float)Math.Max(_width, _height) / 10);

            var image = images[0];

            var imageTransform = image.transform as RectTransform;
            imageTransform.sizeDelta = new Vector2(_width, _height);
            
            MapRotator.Rotate(_contoursCoords);
        
            var heightMapBuilder = new HeightMapBuilder(_width, _height);

            Interpolator interpolator = new ManhattanDistInterpolator();
            if (_interpolatorType == InterpolatorTypes.Euclidean)
                interpolator = new EuclideanDistInterpolator();
            
            var (filledHeights, filled) = heightMapBuilder.Build(_contoursHeights, _contoursCoords, interpolator,
                ShowProgressBar);

            var minHeight = _contoursHeights.Min();
            var maxHeight = _contoursHeights.Max();
            var heightScale = maxHeight - minHeight;
        
            var texture = new Texture2D(_width, _height);
            image.GetComponent<RawImage>().texture = texture;

            for (var y = 0; y < _height; y++)
            {
                for (var x = 0; x < _width; x++)
                {
                    if (filled[x, y])
                    {
                        // color contours blue
                        if (filledHeights[x, y] <= -1.0)
                        {
                            texture.SetPixel(x, y, Color.black);
                            continue;
                        }
                        var color = Color.Lerp(Color.green, Color.red, 
                            (float) ((filledHeights[x,y] - minHeight)/heightScale));
                        texture.SetPixel(x, y, color);
                    }
                    else
                    {
                        texture.SetPixel(x, y, Color.white);
                    }
                }
            }
            texture.Apply();
        }

        private static void ShowProgressBar(float progress)
        {
            EditorUtility.DisplayProgressBar(ProgressBarTitle, ProgressBarMessage, progress);
        }
    }
}