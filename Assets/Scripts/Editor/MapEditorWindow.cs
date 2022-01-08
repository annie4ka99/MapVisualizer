using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Utils;
using Utils.interpolators;

namespace Editor
{
    public class MapEditorWindow : EditorWindow
    {
        private const string ChooseFileButton = "select map file";
        private const string DrawMapButton = "Draw map";
        private const string CreateTerrainButton = "Create terrain";

        private const string ColorTerrainToggle = "colored terrain";

        private const string MapWindowTitle = "Draw map";
        private const string TerrainWindowTitle = "Create terrain";

        private const string NoFileSelectedMessage = "You must select a file first!";
        private const string NegativeSizeMessage = "Width and height must be positive";

        private const int TerrainHeightMapRes = 4097;
        private const string TerrainParentName = "Terrain";

        private const string AssetsFolder = "Assets";
        private const string TerrainsFolder = "Terrains";
        private const string AssetExtension = ".asset";
        private const string LayerExtension = ".Terrainlayer";
        
        private string _filePath = "";

        private int _width = 8000;
        private int _height = 8000;

        private InterpolatorTypes _interpolatorType = InterpolatorTypes.Manhattan;

        private List<double> _contoursHeights;
        private List<List<(double, double)>> _contoursCoords;

        private bool _isTerrainColored;

        private bool _isBlurred;
        private int _blurRadius = 5;
        
        [MenuItem("Map/Map Editor")]
        private static void Init()
        {
            var window = (MapEditorWindow) GetWindow(typeof(MapEditorWindow));
            window.Show();
        }

        private void OnGUI()
        {
            _width = EditorGUILayout.IntField("Width:", _width);
            _height = EditorGUILayout.IntField("Height:", _height);
            _interpolatorType = (InterpolatorTypes) EditorGUILayout.EnumPopup("Interpolator type", _interpolatorType);
            
            _isBlurred = EditorGUILayout.Toggle("Blur", _isBlurred);
            if (_isBlurred)
            {
                _blurRadius = EditorGUILayout.IntField("Blur radius",_blurRadius);
            }
            

            if (GUILayout.Button(ChooseFileButton))
            {
                _filePath = EditorUtility.OpenFilePanel("select json file with contours coordinated",
                    "",
                    "json");
            }

            EditorGUILayout.LabelField("Selected file:", _filePath);
            
            _isTerrainColored = EditorGUILayout.Toggle(ColorTerrainToggle, _isTerrainColored);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(DrawMapButton))
            {
                if (_filePath == "")
                {
                    EditorUtility.DisplayDialog(MapWindowTitle, NoFileSelectedMessage, "OK");
                    return;
                }

                if (_width <= 0 || _height <= 0)
                {
                    EditorUtility.DisplayDialog(MapWindowTitle, NegativeSizeMessage, "OK");
                    return;
                }

                ShowMapProgressBar(0f);
                DrawMap();
                EditorUtility.ClearProgressBar();
            } else if (GUILayout.Button(CreateTerrainButton))
            {
                if (_filePath == "")
                {
                    EditorUtility.DisplayDialog(TerrainWindowTitle, NoFileSelectedMessage, "OK");
                    return;
                }

                if (_blurRadius < 1)
                {
                    EditorUtility.DisplayDialog(TerrainWindowTitle, "Blur radius should be > 0 ", "OK");
                    return;
                }

                if (_width <= 0 || _height <= 0)
                {
                    EditorUtility.DisplayDialog(TerrainWindowTitle, NegativeSizeMessage, "OK");
                    return;
                }

                ShowTerrainProgressBar(0f);
                CreateTerrain();
                EditorUtility.ClearProgressBar();
            }
            GUILayout.EndHorizontal();
        }

        private void ReadContours()
        {
            (_contoursHeights, _contoursCoords) = ContourLinesReader.ReadMetricContourLines(_filePath);
        }


        private void CreateTerrainTile(string tileName, int xPos, int yPos, int width, int length , float height, 
            float[,] heightMap, GameObject parent, Color[,] colorMap
            )
        {
            const int detailResolution = 1024;
            const int detailResolutionPerPatch = 32;
            const int controlTextureResolution = TerrainHeightMapRes - 1;
            const int baseTextureResolution = 1024;

            

            var terrainData = new TerrainData
            {
                name = tileName,
                baseMapResolution = baseTextureResolution,
                heightmapResolution = TerrainHeightMapRes,
                alphamapResolution = controlTextureResolution,
                size = new Vector3(width, height, length),
            };
            
            terrainData.SetDetailResolution(detailResolution, detailResolutionPerPatch);
            terrainData.SetHeights(0, 0, heightMap);
            
            
            if (_isTerrainColored)
            {
                var texture = new Texture2D(width - 1, length - 1);
                for (var y = 0; y < length - 1; y++)
                {
                    for (var x = 0; x < width - 1; x++)
                    {
                        texture.SetPixel(x, y, colorMap[x, y]);
                    }
                }

                texture.Apply();

                var terrainLayers = new TerrainLayer[1];
                terrainLayers[0] = new TerrainLayer
                {
                    diffuseTexture = texture,
                    tileSize = new Vector2(controlTextureResolution, controlTextureResolution)
                };
                
                AssetDatabase.CreateAsset(terrainLayers[0], 
                    Path.Combine(Path.Combine(AssetsFolder , TerrainsFolder), tileName + LayerExtension));
                terrainData.terrainLayers = terrainLayers;
            }

            var terrain = Terrain.CreateTerrainGameObject(terrainData);
            terrain.name = tileName;
            terrain.transform.parent = parent.transform;
            terrain.transform.position = new Vector3(xPos, 0, yPos);
            
            AssetDatabase.CreateAsset(terrainData, Path.Combine(
                Path.Combine(AssetsFolder , TerrainsFolder), 
                tileName + AssetExtension));
        }


        private static void DeletePrevAssets()
        {
            var terrain = GameObject.Find(TerrainParentName);
            if (terrain != null)
                DestroyImmediate(terrain);

            var terrainsPath = Path.Combine(AssetsFolder, TerrainsFolder);
            if (!AssetDatabase.IsValidFolder(terrainsPath))
            {
                AssetDatabase.CreateFolder(AssetsFolder, TerrainsFolder);
                return;
            }

            string[] unusedFolder = { terrainsPath };
            foreach (var asset in AssetDatabase.FindAssets("", unusedFolder))
            {
                var path = AssetDatabase.GUIDToAssetPath(asset);
                AssetDatabase.DeleteAsset(path);
            }
        }
        
        private static void GaussianBlur(double[] scl, double[] tcl, int w, int h, int r) {
            var bxs = BoxesForGauss(r, 3);
            BoxBlur (scl, tcl, w, h, (bxs[0]-1)/2, 0.0f);
            BoxBlur (tcl, scl, w, h, (bxs[1]-1)/2, 1.0f/3);
            BoxBlur (scl, tcl, w, h, (bxs[2]-1)/2, 2.0f/3);
        }
        private static void BoxBlur(double[] scl, double[] tcl, int w, int h, int r, float progressAdd)
        {
            long progress = 0;
            var totalSteps = scl.Length;
            var progressStep = totalSteps / 100;
            long curProgressSteps = 0;
            
            for (var i = 0; i < h; i++)
            {
                for (var j = 0; j < w; j++)
                {
                    var val = 0.0;
                    for (var iy = i - r; iy < i + r + 1; iy++)
                    for (var ix = j - r; ix < j + r + 1; ix++)
                    {
                        var x = Math.Min(w - 1, Math.Max(0, ix));
                        var y = Math.Min(h - 1, Math.Max(0, iy));
                        val += scl[y * w + x];
                    }

                    tcl[i * w + j] = val / ((r + r + 1) * (r + r + 1));
                    progress++;
                }
                if (progress <= (curProgressSteps + 1) * progressStep) continue;
                curProgressSteps = progress / progressStep;
                ShowBlurTerrainProgressBar(((float) progress / totalSteps) * (1.0f/3) + progressAdd);
            }
        }
        
        private static int[] BoxesForGauss(double sigma, int n)  // standard deviation, number of boxes
        {
            var wIdeal = Math.Sqrt((12 * sigma*sigma / n) + 1);  // Ideal averaging filter width 
            var wl = (int)Math.Floor(wIdeal);  
            if (wl % 2==0) 
                wl--;
            var wu = wl+2;
            var mIdeal = (12*sigma*sigma - n*wl*wl - 4*n*wl - 3*n)/(-4*wl - 4);
            var m = (int)Math.Round(mIdeal);
            // var sigmaActual = Math.sqrt( (m*wl*wl + (n-m)*wu*wu - n)/12 );
				
            var sizes = new int[n];  
            for(var i = 0; i < n; i++) 
                sizes[i] = i < m ? wl : wu;
            return sizes;
        }
        
        private double[,] BlurHeightMap(bool[,] filled, double[,] filledHeights)
        {
            var newHeights = new double[_width, _height];
            var tcl = new double[_width * _height];
            var scl = new double[_width * _height];
            for (var y = 0; y < _height; ++y)
            {
                for (var x = 0; x < _width; ++x)
                {
                    if (!filled[x, y])
                    {
                        scl[y * _width + x] = 0.0;
                    }
                    else
                    {
                        scl[y * _width + x] = filledHeights[x, y];
                    }
                }
            }

            GaussianBlur(scl, tcl, _width, _height, _blurRadius);
            
            for (var y = 0; y < _height; ++y)
            {
                for (var x = 0; x < _width; ++x)
                {
                    if (!filled[x, y]) continue;
                    newHeights[x, y] = tcl[y * _width + x];
                }
            }

            return newHeights;
        }

        private void CreateTerrain()
        {
            DeletePrevAssets();
            ReadContours();

            MapRotator.Rotate(_contoursCoords);

            var heightMapBuilder = new HeightMapBuilder(_width, _height);
            var interpolator = GetInterpolator();

            var (filledHeights, filled, isContour) = heightMapBuilder.Build(_contoursHeights, _contoursCoords, interpolator,
                ShowTerrainProgressBar);
            EditorUtility.ClearProgressBar();
            ShowBlurTerrainProgressBar(0.0f);

            if (_isBlurred)
            {
                filledHeights = BlurHeightMap(filled, filledHeights);
            }
            
            var minHeight = _contoursHeights.Min();
            var maxHeight = _contoursHeights.Max();

            var heightScale = maxHeight - minHeight;

            var colors = new Color[_width, _height];
            for (var y = 0; y < _height; y++)
            {
                for (var x = 0; x < _width; x++)
                {
                    if (filled[x, y])
                    {
                        if (isContour[x, y])
                        {
                            colors[x, y] = Color.black;
                        }
                        else
                        {
                            colors[x, y] = Color.Lerp(Color.green, Color.red,
                                (float) ((filledHeights[x, y] - minHeight) / heightScale));
                        }
                    }
                    else
                    {
                        colors[x, y] = Color.white;
                    }
                }
            }

            var parent = new GameObject(TerrainParentName);
            parent.transform.position = new Vector3(0, 0, 0);

            const int tileWidth = TerrainHeightMapRes;
            const int tileLength = TerrainHeightMapRes;
            var height = (float)maxHeight;
            
            var tilesNumInXAxis = 1 + (int)Math.Ceiling((double)(_width - tileWidth) / (tileWidth - 1));
            var tilesNumInYAxis = 1 + (int)Math.Ceiling((double)(_height - tileLength) / (tileLength - 1));
            for (var xTileId = 0; xTileId < tilesNumInXAxis; ++xTileId)
            {
                for (var yTileId = 0; yTileId < tilesNumInYAxis; ++yTileId)
                {
//                    if (xTileId == tilesNumInXAxis - 1)
//                        tileWidth = _width - ((tileWidth - 1) * xTileId);
//                    if (yTileId == tilesNumInYAxis - 1)
//                        tileLength = _height - ((tileLength - 1) * yTileId);
                    var heightMap = new float[tileLength, tileWidth];
                    var colorMap = new Color[tileWidth, tileLength];
                    for (var y = 0; y < tileLength; y++)
                    {
                        for (var x = 0; x < tileWidth; x++)
                        {
                            var curX = xTileId * (tileWidth - 1) + x ;
                            var curY = yTileId * (tileLength - 1) + y;
                            if (curX >= _width || curY >= _height || !filled[curX, curY])
                            {
                                heightMap[y,x] = 0f;
                                colorMap[x,y] = Color.white;
                            } else {
                                heightMap[y,x] = (float) ((filledHeights[curX, curY] - minHeight) / heightScale);
                                colorMap[x,y] = colors[x, y];
                            }
                        }
                    }
                    CreateTerrainTile("terrain_" + xTileId +"_"+ yTileId, 
                        xTileId * tileWidth, yTileId * tileLength, 
                        tileWidth, tileLength, height, heightMap, parent, colorMap);
                }
            }
        }


        private void DrawMap()
        {
            ReadContours();
            
            var images = FindObjectsOfType<RawImage>();
            if (images.Length == 0)
            {
                EditorUtility.DisplayDialog("Error", "Didn't find any image", "OK");
                return;
            }

            GameObject.Find("Main Camera").transform.position = new Vector3(0, 0,
                -(float) Math.Max(_width, _height) / 10);

            var image = images[0];

            var imageTransform = image.transform as RectTransform;
            if (imageTransform == null)
            {
                EditorUtility.DisplayDialog("Error", "No transform component for image", "OK");
                return;
            }
            imageTransform.sizeDelta = new Vector2(_width, _height);

            MapRotator.Rotate(_contoursCoords);

            var heightMapBuilder = new HeightMapBuilder(_width, _height);

            var interpolator = GetInterpolator();

            var (filledHeights, filled, isContour) = heightMapBuilder.Build(_contoursHeights, _contoursCoords, interpolator,
                ShowMapProgressBar);

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
                        if (isContour[x, y])
                        {
                            texture.SetPixel(x, y, Color.black);
                        }
                        else
                        {
                            var color = Color.Lerp(Color.green, Color.red,
                                (float) ((filledHeights[x, y] - minHeight) / heightScale));
                            texture.SetPixel(x, y, color);
                        }
                    }
                    else
                    {
                        texture.SetPixel(x, y, Color.white);
                    }
                }
            }

            texture.Apply();
        }

        private Interpolator GetInterpolator()
        {
            if (_interpolatorType == InterpolatorTypes.Euclidean)
                return new EuclideanDistInterpolator();
            return new ManhattanDistInterpolator();
        }

        private static void ShowTerrainProgressBar(float progress)
        {
            EditorUtility.DisplayProgressBar(TerrainWindowTitle, "Interpolating terrain...", progress);
        }

        private static void ShowMapProgressBar(float progress)
        {
            EditorUtility.DisplayProgressBar(MapWindowTitle, "Drawing map...", progress);
        }

        private static void ShowBlurTerrainProgressBar(float progress)
        {
            EditorUtility.DisplayProgressBar("Blur terrain", "Blurring terrain...", progress);
        }
    }
}