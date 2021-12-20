using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
//using UnityEngine;
using DotSpatial.Projections;

namespace Utils
{

    public class ContourLinesReader
    {
        private static List<List<(double, double)>> ParseContourLinesCoords(string contoursMultilineString)
        {
            var contourLinesCoords = new List<List<(double, double)>>();
            
            var r = new Regex(@"multilinestring\s+\(\s*(\(\s*([^)]+)\)\s*\,?\s*)+\s*\)\s*", 
                RegexOptions.IgnoreCase);
            var m = r.Match(contoursMultilineString);
            foreach (Capture capture in m.Groups[2].Captures)
            {
                var coordinateStrings = capture.Value
                    .Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries);
                var xy = new List<(double, double)>();
                foreach (var coordinateString in coordinateStrings)
                {
                    var coords = coordinateString
                        .Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries);
                    xy.Add((
                        double.Parse(coords[0], CultureInfo.InvariantCulture), 
                        double.Parse(coords[1], CultureInfo.InvariantCulture)));
                }
                contourLinesCoords.Add(xy);
            }

            return contourLinesCoords;
        }

        /*private static string ReadMapPart(string directoryPath, int mapPartNum)
        {
            var files = Directory.GetFiles(directoryPath, "*.json");
            var file = files[mapPartNum];
            string content;
            using (var reader = new StreamReader(file))
            {
                 content = reader.ReadToEnd();
            }
            return content;
        }*/
        private static string ReadMapPart(string filePath)
        {
            string content;
            using (var reader = new StreamReader(filePath))
            {
                content = reader.ReadToEnd();
            }
            return content;
        }

        private static void ProjectFromGeoToMetric(List<(double, double)> coords)
        {
            var xy = new double[coords.Count * 2];
            for (var i = 0; i < coords.Count; ++i)
            {
                var (x, y) = coords[i];
                xy[2 * i] = x;
                xy[2 * i + 1] = y;
            }
            
            const string mapProj4 = "+proj=longlat +datum=WGS84 +no_defs";
            const string sceneProj4 = "+proj=sterea +lat_0=0.0 +lon_0=0.0 +k=1 +x_0=0 +y_0=0 +datum=WGS84 +units=m +no_defs";
            var mapProjectionInfo = ProjectionInfo.FromProj4String(mapProj4);
            var sceneProjectionInfo = ProjectionInfo.FromProj4String(sceneProj4);
            Reproject.ReprojectPoints(xy, new double[1], mapProjectionInfo, sceneProjectionInfo, 
                0, xy.Length / 2);
            
            for (var i = 0; i < coords.Count; ++i)
            {
                coords[i] = (xy[2 * i], xy[2 * i + 1]);
            }

        }

        private static Dictionary<string, (List<double>, List<List<(double, double)>>)> GetContourLines(string json)
        {
            var contourLines = new Dictionary<string, (List<double>, List<List<(double, double)>>)>();
            
//          var map = JsonUtility.FromJson<ExportMap>(json);
            var map = JsonConvert.DeserializeObject<ExportMap>(json);
            foreach (var layer in map.Layers)
            {
                if (!contourLines.ContainsKey(layer.Name))
                    contourLines.Add(layer.Name, (new List<double>(), new List<List<(double, double)>>()));
                
                var features = layer.Features;
                foreach (var feature in features)
                {
                    var height = feature.Fields["SC_4"];
                    if (height == "")
                        continue;
                    var linesCoords = ParseContourLinesCoords(feature.Geometry);
                    foreach (var lineCoords in linesCoords)
                    {
                        var (heights, lines) = contourLines[layer.Name];
                        heights.Add(double.Parse(height, CultureInfo.InvariantCulture));
                        lines.Add(lineCoords);
                    }
                }
            }
            

            return contourLines;
        }
        
        public static (List<double>, List<List<(double, double)>>) ReadMetricContourLines(string filePath)
        {
            var mapPart = ReadMapPart(filePath);
//            var mapPart = ReadMapPart("Assets/Data/map/", mapPartNum);
         
            var contourLines = GetContourLines(mapPart);
            foreach (var layerName in contourLines.Keys)
            {
                var (_, lines) = contourLines[layerName];
                foreach (var line in lines)
                {
                    ProjectFromGeoToMetric(line);
                }

            }
            return contourLines["Relief"];
        }
    }

}

