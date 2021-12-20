using System;
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
        private const string DrawMapButton = "Create terrain";

        private const string ProgressBarTitle = "Create terrain";
        private const string ProgressBarMessage = "Creating terrain...";

        private const int TerrainHeightMapRes = 4097;
        private const string TerrainParentName = "Terrain";

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
            var window = (MapEditorWindow) GetWindow(typeof(MapEditorWindow));
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
//                DrawMap();
                CreateTerrain();
                EditorUtility.ClearProgressBar();
            }
        }

        private void ReadContours()
        {
            (_contoursHeights, _contoursCoords) = ContourLinesReader.ReadMetricContourLines(_filePath);
        }


        private static void CreateTerrainTile(string tileName, int xPos, int yPos, float width, float length , float height, 
            float[,] heightMap, GameObject parent
            )
        {
            const int detailResolution = 1024;
            const int detailResolutionPerPatch = 32;
            const int controlTextureResolution = 512;
            const int baseTextureResolution = 1024;

            var terrainData = new TerrainData
            {
                name = tileName,
                size = new Vector3(width / 128f, height, length / 128f),
                baseMapResolution = baseTextureResolution,
                heightmapResolution = TerrainHeightMapRes,
                alphamapResolution = controlTextureResolution
            };
            terrainData.SetDetailResolution(detailResolution, detailResolutionPerPatch);
            terrainData.SetHeights(0, 0, heightMap);


            var terrain = Terrain.CreateTerrainGameObject(terrainData);
            terrain.name = tileName;
            terrain.transform.parent = parent.transform;
            terrain.transform.position = new Vector3(xPos, 0, yPos);
            
            AssetDatabase.CreateAsset(terrainData, "Assets/Terrains/" + tileName + ".asset");
        }


        private static void DeletePrevTerrain()
        {
            var terrain = GameObject.Find(TerrainParentName);
            if (terrain != null)
                DestroyImmediate(terrain);

            string[] unusedFolder = { "Assets/Terrains" };
            foreach (var asset in AssetDatabase.FindAssets("", unusedFolder))
            {
                var path = AssetDatabase.GUIDToAssetPath(asset);
                AssetDatabase.DeleteAsset(path);
            }
        }

        private void CreateTerrain()
        {
            DeletePrevTerrain();

            ShowProgressBar(0f);

            if (_contoursCoords == null || _contoursHeights == null)
            {
                ReadContours();
            }

            MapRotator.Rotate(_contoursCoords);

            var heightMapBuilder = new HeightMapBuilder(_width, _height);
            var interpolator = GetInterpolator();

            var (filledHeights, filled) = heightMapBuilder.Build(_contoursHeights, _contoursCoords, interpolator,
                ShowProgressBar);

            var minHeight = _contoursHeights.Min();
            var maxHeight = _contoursHeights.Max();

            var heightScale = maxHeight - minHeight;
            
            
            var parent = new GameObject(TerrainParentName);
            parent.transform.position = new Vector3(0, 0, 0);

            var width = TerrainHeightMapRes;
            var length =TerrainHeightMapRes;
            var height = (float)maxHeight;
            
            var tilesNumInAxis = Math.Max(_height, _width) / TerrainHeightMapRes + 1;
            for (var xTileId = 0; xTileId < tilesNumInAxis; ++xTileId)
            {
                for (var yTileId = 0; yTileId < tilesNumInAxis; ++yTileId)
                {
                    var heightMap = new float[length, width];
                    for (var y = 0; y < length; y++)
                    {
                        for (var x = 0; x < width; x++)
                        {
                            var curX = xTileId * width + x;
                            var curY = yTileId * length + y;
                            if (curX >= _width || curY >= _height || !filled[curX, curY])
                            {
                                heightMap[y, x] = 0f;
                            } else {
                                heightMap[y, x] = (float) ((filledHeights[curX, curY] - minHeight) / heightScale);
                            }
                        }
                    }
                    CreateTerrainTile("terrain_" + xTileId +"_"+ yTileId, 
                        xTileId * width, yTileId * length, 
                        width, length, height, heightMap, parent);
                }
            }
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
                -(float) Math.Max(_width, _height) / 10);

            var image = images[0];

            var imageTransform = image.transform as RectTransform;
            imageTransform.sizeDelta = new Vector2(_width, _height);

            MapRotator.Rotate(_contoursCoords);

            var heightMapBuilder = new HeightMapBuilder(_width, _height);

            var interpolator = GetInterpolator();

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
                        if (filledHeights[x, y] == -1.0)
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

        private static void ShowProgressBar(float progress)
        {
            EditorUtility.DisplayProgressBar(ProgressBarTitle, ProgressBarMessage, progress);
        }
    }
}