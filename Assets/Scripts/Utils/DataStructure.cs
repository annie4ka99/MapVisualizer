using System;
using System.Collections.Generic;

namespace Utils
{
    [Serializable]
    public class ExportMap
    {
        public List<ExportLayer> Layers { get; set; }
    }

    [Serializable]
    public class ExportLayer
    {
        public string Name { get; set; }

        public List<ExportFeature> Features { get; set; }
    }

    [Serializable]
    public class ExportFeature
    {
        public string Name { get; set; }

        public string Height { get; set; } = string.Empty;

        public string Geometry { get; set; } // Изолинии в географических координатах

        public Dictionary<string, string> Fields { get; set; } = new Dictionary<string, string>(); // Поле "SC_4" - высота, может отсутствовать
    }

}


