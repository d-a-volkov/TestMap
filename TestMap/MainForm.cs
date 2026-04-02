using GeoAPI.Geometries;
using GMap.NET;
using GMap.NET.MapProviders;
using GMap.NET.WindowsForms;
using GMap.NET.WindowsForms.Markers;
using NetTopologySuite.Features;
using NetTopologySuite.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OsmSharp;
using OsmSharp.Streams;
using Sandwych.MapMatchingKit.Matching;
using Sandwych.MapMatchingKit.Roads;
using Sandwych.MapMatchingKit.Spatial;
using Sandwych.MapMatchingKit.Spatial.Geometries;
using Sandwych.MapMatchingKit.Topology;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TrackBar;

namespace TestMap
{


    public partial class MainForm : Form
    {
        private GMapControl gMapControl;

        string PbfPath;
        string DataFilePath; // Общий путь к файлу данных (GeoJSON или JSON)

        PointLatLng? selectionStart = null;
        private PointLatLng? selectionEnd = null;
        private GMapOverlay selectionOverlay = new GMapOverlay("selection");
        private GMapPolygon selectionPolygon = null;
        private bool isSelecting = false;
        double minLat;
        double maxLat;
        double minLng;
        double maxLng;

        // Сохраняем дороги как объекты с атрибутами
        private List<RoadAttributes> roads = new List<RoadAttributes>();

        public MainForm()
        {
            InitializeComponent();
            SetupMap();

            gMapControl.MouseDown += OnMapMouseDown;
            gMapControl.MouseMove += OnMapMouseMove;
            gMapControl.MouseUp += OnMapMouseUp;

            PbfPath = @"north-caucasus-fed-district-260317.osm.pbf";
            DataFilePath = @"filtered_roads.geojson"; // По умолчанию GeoJSON
        }

        private void SetupMap()
        {
            gMapControl.Manager.Mode = AccessMode.ServerOnly;
            gMapControl.MapProvider = GMap.NET.MapProviders.GoogleMapProvider.Instance;
            gMapControl.Position = new PointLatLng(42.823313, 47.660339);
            gMapControl.MinZoom = 2;
            gMapControl.MaxZoom = 20;
            gMapControl.Zoom = 12;

            gMapControl.ShowCenter = false;
            gMapControl.CanDragMap = true;
            gMapControl.DragButton = MouseButtons.Left;
            gMapControl.ShowTileGridLines = false;
            gMapControl.MarkersEnabled = true;
        }

        private void LoadRoadsFromPbf(string pbfPath)
        {
            var source = new PBFOsmStreamSource(File.OpenRead(pbfPath));
            var nodes = new Dictionary<long, Node>();

            // Сначала загружаем все Node
            foreach (var element in source.Where(e => e.Type == OsmGeoType.Node))
            {
                var node = (Node)element;
                if (node.Latitude.HasValue && node.Longitude.HasValue)
                {
                    nodes[node.Id.Value] = node;
                }
            }

            // Перезапускаем поток, чтобы читать Way
            source = new PBFOsmStreamSource(File.OpenRead(pbfPath));

            var filterSource = source.Where(e => e.Type == OsmGeoType.Way &&
                                                 e.Tags.ContainsKey("highway") &&
                                                 (
                                                    e.Tags.GetValue("highway").Contains("motorway") ||
                                                    e.Tags.GetValue("highway").Contains("trunk") ||
                                                    e.Tags.GetValue("highway").Contains("primary") ||
                                                    e.Tags.GetValue("highway").Contains("secondary") ||
                                                    e.Tags.GetValue("highway").Contains("tertiary") ||
                                                    e.Tags.GetValue("highway").Contains("unclassified") ||
                                                    e.Tags.GetValue("highway").Contains("residential")
                                                 ));


            foreach (var element in filterSource)
            {
                var way = (Way)element;
                var points = new List<PointLatLng>();

                foreach (var nodeId in way.Nodes)
                {
                    if (nodes.TryGetValue(nodeId, out var node))
                    {
                        points.Add(new PointLatLng(node.Latitude ?? 0, node.Longitude ?? 0));
                    }
                }

                if (points.Count > 1)
                {
                    // --- Вычисление атрибутов ---
                    long id = way.Id ?? -1;
                    long sourceNode = way.Nodes.Length > 0 ? way.Nodes[0] : -1;
                    long targetNode = way.Nodes.Length > 1 ? way.Nodes[way.Nodes.Length - 1] : -1;
                    bool oneWay = way.Tags.ContainsKey("oneway") && (way.Tags.GetValue("oneway") == "yes" || way.Tags.GetValue("oneway") == "true" || way.Tags.GetValue("oneway") == "1");
                    string type = way.Tags.GetValue("highway");
                    int priority = GetPriorityByHighwayType(type);

                    double maxSpeedForward = ParseMaxSpeed(way.Tags.GetValue("maxspeed"));
                    double maxSpeedBackward = oneWay ? 0 : maxSpeedForward; // Если односторонняя, то назад = 0

                    // Пример вычисления длины (в градусах, приближённо)
                    double length = CalculateApproximateLength(points);

                    var road = new RoadAttributes
                    {
                        Id = id,
                        Source = sourceNode,
                        Target = targetNode,
                        OneWay = oneWay,
                        Type = type,
                        Priority = priority,
                        MaxSpeedForward = maxSpeedForward,
                        MaxSpeedBackward = maxSpeedBackward,
                        Length = length,
                        Geometry = points
                    };

                    roads.Add(road);
                }
            }

            MessageBox.Show($"Загружено {roads.Count} дорог(и).");
        }

        private int GetPriorityByHighwayType(string highwayType)
        {
            // Простое сопоставление типа дороги приоритету
            switch (highwayType?.ToLower())
            {
                case "motorway": return 1;
                case "trunk": return 2;
                case "primary": return 3;
                case "secondary": return 4;
                case "tertiary": return 5;
                case "unclassified": return 6;
                case "residential": return 7;
                default: return 10; // lowest priority
            }
        }

        private double ParseMaxSpeed(string maxSpeedTag)
        {
            if (string.IsNullOrEmpty(maxSpeedTag)) return 0;

            // Попробуем извлечь число из строки (например, "50 km/h" -> 50)
            var parts = maxSpeedTag.Split(' ');
            if (double.TryParse(parts[0], out double speed))
            {
                return speed;
            }
            return 0;
        }

        private double CalculateApproximateLength(List<PointLatLng> points)
        {
            // Простое вычисление длины линии (в градусах, приближённо)
            if (points.Count < 2) return 0;

            double totalLength = 0;
            for (int i = 1; i < points.Count; i++)
            {
                var p1 = points[i - 1];
                var p2 = points[i];
                // Простое евклидово расстояние между двумя точками (в градусах)
                totalLength += Math.Sqrt(Math.Pow(p2.Lat - p1.Lat, 2) + Math.Pow(p2.Lng - p1.Lng, 2));
            }
            return totalLength;
        }

        private void SaveRoadsAsGeoJson(string filePath)
        {
            var featureCollection = new JObject(
                new JProperty("type", "FeatureCollection"),
                new JProperty("features", new JArray(roads.Select(road => new JObject(
                    new JProperty("type", "Feature"),
                    new JProperty("geometry", new JObject(
                        new JProperty("type", "LineString"),
                        new JProperty("coordinates", new JArray(road.Geometry.Select(p => new JArray(p.Lng, p.Lat))))
                    )),
                    new JProperty("properties", new JObject(
                        new JProperty("id", road.Id),
                        new JProperty("source", road.Source),
                        new JProperty("target", road.Target),
                        new JProperty("reverse", road.Reverse),
                        new JProperty("oneway", road.OneWay),
                        new JProperty("type", road.Type),
                        new JProperty("priority", road.Priority),
                        new JProperty("maxspeedForward", road.MaxSpeedForward),
                        new JProperty("maxspeedBackward", road.MaxSpeedBackward),
                        new JProperty("length", road.Length)
                    ))
                )))));

            File.WriteAllText(filePath, featureCollection.ToString(Formatting.Indented));
            MessageBox.Show($"Дороги сохранены в {filePath} в формате GeoJSON");
        }


        private string jsonString;

        // Метод для чтения GeoJSON файла и загрузки дорог
        private void LoadRoadsFromGeoJson(string filePath)
        {
            jsonString = File.ReadAllText(filePath);
            JObject geoJsonObj = JObject.Parse(jsonString);

            var loadedRoads = new List<RoadAttributes>();

            if ((string)geoJsonObj["type"] == "FeatureCollection")
            {
                var features = (JArray)geoJsonObj["features"];
                foreach (JObject feature in features)
                {
                    if ((string)feature["geometry"]["type"] == "LineString")
                    {
                        var coordinates = (JArray)feature["geometry"]["coordinates"];
                        var geometry = new List<PointLatLng>();
                        foreach (JArray coord in coordinates)
                        {
                            double lng = (double)coord[0];
                            double lat = (double)coord[1];
                            geometry.Add(new PointLatLng(lat, lng));
                        }

                        // Загружаем атрибуты
                        var props = (JObject)feature["properties"];
                        var road = new RoadAttributes
                        {
                            Id = props.ContainsKey("id") ? (long)props["id"] : -1,
                            Source = props.ContainsKey("source") ? (long)props["source"] : -1,
                            Target = props.ContainsKey("target") ? (long)props["target"] : -1,
                            Reverse = props.ContainsKey("reverse") ? (double)props["reverse"] : -1,
                            OneWay = props.ContainsKey("oneway") ? (bool)props["oneway"] : false,
                            Type = props.ContainsKey("type") ? (string)props["type"] : "",
                            Priority = props.ContainsKey("priority") ? (int)props["priority"] : 0,
                            MaxSpeedForward = props.ContainsKey("maxspeedForward") ? (double)props["maxspeedForward"] : 0,
                            MaxSpeedBackward = props.ContainsKey("maxspeedBackward") ? (double)props["maxspeedBackward"] : 0,
                            Length = props.ContainsKey("length") ? (double)props["length"] : 0,
                            Geometry = geometry
                        };

                        if (road.Geometry.Count > 1)
                        {
                            loadedRoads.Add(road);
                        }
                    }
                }
            }

            roads = loadedRoads;
        }


        private void DrawRoadsOnMap(Color color)
        {
            var overlay = new GMapOverlay("roads");

            foreach (var road in roads)
            {
                var route = new GMapRoute(road.Geometry, $"road_{road.Id}")
                {
                    Stroke = new Pen(color, 1)
                };
                overlay.Routes.Add(route);
            }

            gMapControl.Overlays.Add(overlay);
            //gMapControl.ZoomAndCenterMarkers(overlay.Id);
            gMapControl.Refresh();
        }


        private void DrawPoinOnMap(List<PointLatLng> points, GMarkerGoogleType metka)
        {
            var pointsOverlay = new GMapOverlay("points");

            foreach (var point in points)
            {
                var marker = new GMarkerGoogle(point, metka) // Можно выбрать другой тип маркера
                {
                    ToolTipText = $"Point: {point.Lat}, {point.Lng}",
                    // Изменяем цвет маркера путем создания Bitmap или использования готового изображения может быть сложно,
                    // поэтому часто используются стандартные типы или кастомные изображения.
                    // Для простоты можно использовать один цвет, но GMarkerGoogle не меняет цвет через свойства напрямую.
                    // Альтернатива - рисовать кружок.
                };

                // Альтернатива: создание кастомного маркера с цветом
                //var customMarker = new GMapMarkerGoogleRotated(point) // Используйте GMapMarker, если GMapMarkerGoogleRotated недоступен
                //{
                //    ToolTipText = $"Point: {point.Lat}, {point.Lng}"
                //};

                // Но самый простой способ изменить внешний вид - использовать Overlay.Polygons или Overlay.Markers с кастомным рендерингом.
                // Для простоты используем стандартный маркер и добавим его.
                pointsOverlay.Markers.Add(marker);
            }

            // Более подходящий способ для отображения множества точек - это создание GMapRoute с очень короткими сегментами или просто список маркеров.
            // Но так как маркеры могут перекрываться, лучше использовать кастомный GMapPolygon или GMapRoute с толщиной 0 и заливкой.
            // Самый простой способ - использовать GMapRoute с минимальной длиной.

            // Альтернативный способ - создать кастомный маркер с цветом
            //foreach (var point in points)
            //{
            //    pointsOverlay.Markers.Add(new CustomColoredMarker(point, color));
            //}

            gMapControl.Overlays.Add(pointsOverlay);
            //gMapControl.ZoomAndCenterMarkers(pointsOverlay.Id);
            gMapControl.Refresh();
        }


        private void toolStripButton_New_Click(object sender, EventArgs e)
        {
            LoadRoadsFromPbf(PbfPath);
            DrawRoadsOnMap(Color.Blue);
            SaveRoadsAsGeoJson(DataFilePath); // Сохраняем как GeoJSON
        }

        private void toolStripButton_Clear_Click(object sender, EventArgs e)
        {
            ClearMap();
        }

        private void ClearMap()
        {
            gMapControl.Overlays.Clear();
            selectionOverlay.Polygons.Clear();
            gMapControl.Refresh();
        }

        private void toolStripButton_Load_Click(object sender, EventArgs e)
        {
            openFileDialog1.Filter = "GeoJSON files (*.geojson)|*.geojson|JSON files (*.json)|*.json|All files (*.*)|*.*";
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                string filePath = openFileDialog1.FileName;
                ClearMap();
                roads.Clear();

                LoadRoadsFromGeoJson(filePath);

                DrawRoadsOnMap(Color.Red);
            }
        }

        private void toolStripButton_Save_Click(object sender, EventArgs e)
        {
            // Фильтруем дороги по выделенной области
            var filteredRoads = roads.Where(road =>
                road.Geometry.Any(p => p.Lat >= minLat && p.Lat <= maxLat && p.Lng >= minLng && p.Lng <= maxLng)
            ).ToList();

            saveFileDialog1.Filter = "GeoJSON files (*.geojson)|*.geojson|JSON files (*.json)|*.json|All files (*.*)|*.*";
            if (saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                string fileName = saveFileDialog1.FileName;
                string extension = Path.GetExtension(fileName).ToLower();

                if (extension == ".geojson")
                {
                    // Сохраняем как GeoJSON FeatureCollection
                    var featureCollection = new JObject(
                        new JProperty("type", "FeatureCollection"),
                        new JProperty("features", new JArray(filteredRoads.Select(road => new JObject(
                            new JProperty("type", "Feature"),
                            new JProperty("geometry", new JObject(
                                new JProperty("type", "LineString"),
                                new JProperty("coordinates", new JArray(road.Geometry.Select(p => new JArray(p.Lng, p.Lat))))
                            )),
                            new JProperty("properties", new JObject(
                                new JProperty("id", road.Id),
                                new JProperty("source", road.Source),
                                new JProperty("target", road.Target),
                                new JProperty("reverse", road.Reverse),
                                new JProperty("oneway", road.OneWay),
                                new JProperty("type", road.Type),
                                new JProperty("priority", road.Priority),
                                new JProperty("maxspeedForward", road.MaxSpeedForward),
                                new JProperty("maxspeedBackward", road.MaxSpeedBackward),
                                new JProperty("length", road.Length)
                            ))
                        )))));

                    File.WriteAllText(fileName, featureCollection.ToString(Formatting.Indented));
                    MessageBox.Show($"Сохранено {filteredRoads.Count} дорог(и) в {fileName} в формате GeoJSON");
                }
                else
                {
                    // Сохраняем в старом формате JSON с атрибутами
                    var roadsForSerialization = filteredRoads.Select(road => new
                    {
                        id = road.Id,
                        source = road.Source,
                        target = road.Target,
                        oneway = road.OneWay,
                        type = road.Type,
                        priority = road.Priority,
                        maxspeedForward = road.MaxSpeedForward,
                        maxspeedBackward = road.MaxSpeedBackward,
                        length = road.Length,
                        geometry = road.Geometry.Select(p => new { lat = p.Lat, lng = p.Lng }).ToList()
                    }).ToList();

                    string json = JsonConvert.SerializeObject(roadsForSerialization, Formatting.Indented);
                    File.WriteAllText(fileName, json);
                    MessageBox.Show($"Сохранено {filteredRoads.Count} дорог(и) в {fileName} в формате JSON");
                }
            }
        }


        private void OnMapMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                isSelecting = true;
                selectionStart = gMapControl.FromLocalToLatLng(e.X, e.Y);
            }
        }

        private void OnMapMouseMove(object sender, MouseEventArgs e)
        {
            if (isSelecting && selectionStart.HasValue)
            {
                selectionEnd = gMapControl.FromLocalToLatLng(e.X, e.Y);

                if (selectionPolygon != null)
                {
                    selectionOverlay.Polygons.Clear();
                }

                var rectPoints = GetRectanglePoints(selectionStart.Value, selectionEnd.Value);
                selectionPolygon = new GMapPolygon(rectPoints, "selectionRect")
                {
                    Stroke = new Pen(Color.Red, 2),
                    Fill = new SolidBrush(Color.Transparent)
                };

                selectionOverlay.Polygons.Add(selectionPolygon);
                gMapControl.Overlays.Add(selectionOverlay);
                gMapControl.Refresh();
            }
        }

        private void OnMapMouseUp(object sender, MouseEventArgs e)
        {
            if (isSelecting && e.Button == MouseButtons.Right && selectionStart.HasValue && selectionEnd.HasValue)
            {
                minLat = Math.Min(selectionStart.Value.Lat, selectionEnd.Value.Lat);
                maxLat = Math.Max(selectionStart.Value.Lat, selectionEnd.Value.Lat);
                minLng = Math.Min(selectionStart.Value.Lng, selectionEnd.Value.Lng);
                maxLng = Math.Max(selectionStart.Value.Lng, selectionEnd.Value.Lng);

                isSelecting = false;
            }
        }

        private List<PointLatLng> GetRectanglePoints(PointLatLng start, PointLatLng end)
        {
            return new List<PointLatLng>
            {
                new PointLatLng(start.Lat, start.Lng),
                new PointLatLng(start.Lat, end.Lng),
                new PointLatLng(end.Lat, end.Lng),
                new PointLatLng(end.Lat, start.Lng),
                new PointLatLng(start.Lat, start.Lng) // Замыкаем полигон
            };
        }

        public void InitializeMatching()
        {
            var spatial = new GeographySpatialOperation();
            var mapBuilder = new RoadMapBuilder(spatial);

            //MessageBox.Show("Loading road map...");
            var roads = ReadRoads(spatial);
            var map = mapBuilder.AddRoads(roads).Build();
            //MessageBox.Show("The road map has been loaded");

            //var router = new PrecomputedDijkstraRouter<Road, RoadPoint>(map, Costs.TimePriorityCost, Costs.DistanceCost, 1000D);
            var router = new DijkstraRouter<Road, RoadPoint>();

            var matcher = new Matcher<MatcherCandidate, MatcherTransition, MatcherSample>(map, router, Costs.TimePriorityCost, spatial);
            matcher.MaxDistance = 100; // set maximum searching distance between two GPS points to 100 meters.
            matcher.MaxRadius = 20.0; // sets maximum radius for candidate selection to 20 meters

            //MessageBox.Show("Loading GPS samples...");
            var samples = ReadSamples().ToList();//.OrderBy(s => s.Time).ToList();
            //MessageBox.Show($"GPS samples loaded. [count={samples.Count}]");

            //MessageBox.Show("Starting Offline map-matching...");
            OfflineMatch(matcher, samples);


            //MessageBox.Show("Starting Online map-matching...");
            //Uncomment below line to see how online - matching works
            //OnlineMatch(matcher, samples);

            MessageBox.Show("All done!");
        }

        private IEnumerable<RoadInfo> ReadRoads(ISpatialOperation spatial)
        {
            if (jsonString == null)
            {
                MessageBox.Show("No road data loaded. Please load a GeoJSON file with roads first.");
                return Enumerable.Empty<RoadInfo>();
            }
            var reader = new GeoJsonReader();
            var fc = reader.Read<FeatureCollection>(jsonString);

            List<RoadInfo> roadsInfo = new List<RoadInfo>();
            foreach (var feature in fc.Features)
            {
                var lineGeom = feature.Geometry as ILineString;
                roadsInfo.Add(new RoadInfo(
                    Convert.ToInt64(feature.Attributes["id"]),
                    Convert.ToInt64(feature.Attributes["source"]),
                    Convert.ToInt64(feature.Attributes["target"]),
                    (double)feature.Attributes["reverse"] >= 0D ? false : true,
                    (short)0,
                    Convert.ToSingle(feature.Attributes["priority"]),
                    120f,
                    120f,
                    Convert.ToSingle(spatial.Length(lineGeom)),
                    lineGeom)
                    );
            }
            return roadsInfo;
        }

        private IEnumerable<MatcherSample> ReadSamples()
        {
            var txt = File.ReadAllLines(@"dataset.log");


            var samples = new List<MatcherSample>();
            List<MatcherSample> matcherSamples = new List<MatcherSample>();
            foreach (var i in txt)
            {
                var str = i.Split(';');
                if (!double.TryParse(str[2].Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double lat)) continue;
                if (!double.TryParse(str[1].Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double lng)) continue;

                var coord2D = new Coordinate2D(lat, lng);

                var timeFormats = new[] { "HH:mm:ss.fff", "HH:mm:ss" };
                if (!DateTimeOffset.TryParseExact(str[0], timeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
                    continue;

                var longTime = time.ToUnixTimeMilliseconds();
                matcherSamples.Add(new MatcherSample(longTime, time, coord2D));
            }
            return matcherSamples;
        }

        private void OfflineMatch(Matcher<MatcherCandidate, MatcherTransition, MatcherSample> matcher, IReadOnlyList<MatcherSample> samples)
        {
            var kstate = new MatcherKState();

            //Do the offline map-matching
            //MessageBox.Show("Doing map-matching...");
            var startedOn = DateTime.MinValue;
            foreach (var sample in samples)
            {
                var vector = matcher.Execute(kstate.Vector(), kstate.Sample, sample);
                kstate.Update(vector, sample);
            }

            //MessageBox.Show("Fetching map-matching results...");
            var candidatesSequence = kstate.Sequence();
            var timeElapsed = DateTime.Now - startedOn;
            MessageBox.Show($"Map-matching elapsed time: {timeElapsed}, Speed={samples.Count / timeElapsed.TotalSeconds} samples/second");
            MessageBox.Show($"Results: [count={candidatesSequence.Count()}]");
            var csvLines = new List<string>();
            csvLines.Add("time;lat;lng");
            int matchedCandidateCount = 0;
            foreach (var cand in candidatesSequence)
            {
                var roadId = cand.Point.Edge.RoadInfo.Id; // original road id
                var heading = cand.Point.Edge.Headeing; // heading
                var coord = cand.Point.Coordinate; // GPS position (on the road)

                csvLines.Add(string.Format("{0};{1};{2}", cand.Sample.Time.ToUnixTimeSeconds(), coord.Y, coord.X));
                if (cand.HasTransition)
                {
                    var geom = cand.Transition.Route.ToGeometry(); // path geometry(LineString) from last matching candidate
                    //cand.Transition.Route.Edges // Road segments between two GPS position
                }
                matchedCandidateCount++;
            }
            //MessageBox.Show("Matched Candidates: {0}, Rate: {1}%", matchedCandidateCount, matchedCandidateCount * 100 / samples.Count());

            var csvFile = "samples.output.csv";
            MessageBox.Show($"Writing output file: {csvFile}");
            File.WriteAllLines(csvFile, csvLines);
        }

        private void toolStripButton_Matcher_Click(object sender, EventArgs e)
        {
            InitializeMatching();
        }

        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            var track = File.ReadAllLines(@"dataset.log");
            
            List<PointLatLng> points = new List<PointLatLng>();
            foreach (var item in track)
            {
                var str = item.Split(';');
                if (!double.TryParse(str[1].Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double lat)) continue;
                if (!double.TryParse(str[2].Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double lng)) continue;
                points.Add(new PointLatLng(lat, lng));
            }
            DrawPoinOnMap(points, GMarkerGoogleType.green_small);
        }

        private void toolStripButton2_Click(object sender, EventArgs e)
        {
            var track = File.ReadAllLines(@"samples.output.csv");

            List<PointLatLng> points = new List<PointLatLng>();
            foreach (var item in track)
            {
                var str = item.Split(';');
                if (!double.TryParse(str[1].Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double lat)) continue;
                if (!double.TryParse(str[2].Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double lng)) continue;
                points.Add(new PointLatLng(lat, lng));
            }
            DrawPoinOnMap(points, GMarkerGoogleType.orange_small);
        }
    }
}