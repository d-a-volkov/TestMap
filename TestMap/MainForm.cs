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

        // Сохраняем дороги как списки точек
        private List<List<PointLatLng>> roads = new List<List<PointLatLng>>();

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
                    roads.Add(points);
                }
            }

            MessageBox.Show($"Загружено {roads.Count} дорог(и).");
        }

        private void SaveRoadsAsGeoJson(string filePath)
        {
            var featureCollection = new JObject(
                new JProperty("type", "FeatureCollection"),
                new JProperty("features", new JArray(roads.Select(road => new JObject(
                    new JProperty("type", "Feature"),
                    new JProperty("geometry", new JObject(
                        new JProperty("type", "LineString"),
                        new JProperty("coordinates", new JArray(road.Select(p => new JArray(p.Lng, p.Lat))))
                    )),
                    new JProperty("properties", new JObject(
                        new JProperty("name", "road")
                    ))
                )))));

            File.WriteAllText(filePath, featureCollection.ToString(Formatting.Indented));
            MessageBox.Show($"Дороги {filePath} в формате GeoJSON");
        }


        private string jsonString;

        // Метод для чтения GeoJSON файла и загрузки дорог
        private void LoadRoadsFromGeoJson(string filePath)
        {
            jsonString = File.ReadAllText(filePath);
            JObject geoJsonObj = JObject.Parse(jsonString);

            var loadedRoads = new List<List<PointLatLng>>();

            if ((string)geoJsonObj["type"] == "FeatureCollection")
            {
                var features = (JArray)geoJsonObj["features"];
                foreach (JObject feature in features)
                {
                    if ((string)feature["geometry"]["type"] == "LineString")
                    {
                        var coordinates = (JArray)feature["geometry"]["coordinates"];
                        var road = new List<PointLatLng>();
                        foreach (JArray coord in coordinates)
                        {
                            double lng = (double)coord[0];
                            double lat = (double)coord[1];
                            road.Add(new PointLatLng(lat, lng));
                        }
                        if (road.Count > 1)
                        {
                            loadedRoads.Add(road);
                        }
                    }
                }
            }

            roads = loadedRoads;
        }

        // Обновленный метод для сохранения JSON (старый формат), если нужно
        private void SaveRoadsToJson(string filePath)
        {
            var roadsForSerialization = roads.Select(road =>
                road.Select(p => new { lat = p.Lat, lng = p.Lng }).ToList()
            ).ToList();

            string json = JsonConvert.SerializeObject(roadsForSerialization, Formatting.Indented);
            File.WriteAllText(filePath, json);

            MessageBox.Show($"Дороги сохранены в {filePath} в старом формате JSON");
        }

        private void DrawRoadsOnMap(Color color)
        {
            var overlay = new GMapOverlay("roads");

            foreach (var road in roads)
            {
                var route = new GMapRoute(road, "road_" + Guid.NewGuid().ToString())
                {
                    Stroke = new Pen(color, 1)
                };
                overlay.Routes.Add(route);
            }

            gMapControl.Overlays.Add(overlay);
            gMapControl.ZoomAndCenterMarkers(overlay.Id);
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

                if (Path.GetExtension(filePath).ToLower() == ".geojson")
                {
                    LoadRoadsFromGeoJson(filePath);
                }
                else
                {
                    // Поддержка старого формата JSON при необходимости
                    string jsonString = File.ReadAllText(filePath);
                    roads = JsonConvert.DeserializeObject<List<List<PointLatLng>>>(jsonString);
                }

                DrawRoadsOnMap(Color.Red);
            }
        }

        private void toolStripButton_Save_Click(object sender, EventArgs e)
        {
            // Фильтруем дороги по выделенной области
            var filteredRoads = roads.Where(road =>
                road.Any(p => p.Lat >= minLat && p.Lat <= maxLat && p.Lng >= minLng && p.Lng <= maxLng)
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
                                new JProperty("coordinates", new JArray(road.Select(p => new JArray(p.Lng, p.Lat))))
                            )),
                            new JProperty("properties", new JObject(
                                new JProperty("name", "filtered_road")
                            ))
                        )))));

                    File.WriteAllText(fileName, featureCollection.ToString(Formatting.Indented));
                    MessageBox.Show($"Сохранено {filteredRoads.Count} дорог(и) в {fileName} в формате GeoJSON");
                }
                else
                {
                    // Сохраняем в старом формате JSON
                    string json = JsonConvert.SerializeObject(filteredRoads.Select(r =>
                        r.Select(p => new { lat = p.Lat, lng = p.Lng }).ToList()
                    ).ToList(), Formatting.Indented);

                    File.WriteAllText(fileName, json);
                    MessageBox.Show($"Сохранено {filteredRoads.Count} дорог(и) в {fileName} в старом формате JSON");
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

            MessageBox.Show("Loading road map...");
            var roads = ReadRoads(spatial);
            var map = mapBuilder.AddRoads(roads).Build();
            MessageBox.Show("The road map has been loaded");

            //var router = new PrecomputedDijkstraRouter<Road, RoadPoint>(map, Costs.TimePriorityCost, Costs.DistanceCost, 1000D);
            var router = new DijkstraRouter<Road, RoadPoint>();

            var matcher = new Matcher<MatcherCandidate, MatcherTransition, MatcherSample>(
                map, router, Costs.TimePriorityCost, spatial);
            matcher.MaxDistance = 1000; // set maximum searching distance between two GPS points to 1000 meters.
            matcher.MaxRadius = 200.0; // sets maximum radius for candidate selection to 200 meters

            MessageBox.Show("Loading GPS samples...");
            var samples = ReadSamples().OrderBy(s => s.Time).ToList();
            MessageBox.Show("GPS samples loaded. [count={0}]", samples.Count.ToString());

            MessageBox.Show("Starting Offline map-matching...");
            OfflineMatch(matcher, samples);


            MessageBox.Show("Starting Online map-matching...");
            //Uncomment below line to see how online-matching works
            //OnlineMatch(matcher, samples);

            MessageBox.Show("All done!");
        }

        private IEnumerable<RoadInfo> ReadRoads(ISpatialOperation spatial)
        {
            var reader = new GeoJsonReader();
            var fc = reader.Read<FeatureCollection>(jsonString);

            foreach (var feature in fc.Features)
            {
                var lineGeom = feature.Geometry as ILineString;

                yield return new RoadInfo(
                    0,//Convert.ToInt64(feature.Attributes["gid"]),
                    0,//Convert.ToInt64(feature.Attributes["source"]),
                    0,//Convert.ToInt64(feature.Attributes["target"]),
                    true,//(double)feature.Attributes["reverse"] >= 0D ? false : true,
                    (short)0,
                    0f, //Convert.ToSingle(feature.Attributes["priority"]),
                    120f,
                    120f,
                    Convert.ToSingle(spatial.Length(lineGeom)),
                    lineGeom);
            }
        }

        private IEnumerable<MatcherSample> ReadSamples()
        {            
            var json = File.ReadAllText(@"samples.oneday.geojson");
            var reader = new GeoJsonReader();
            var fc = reader.Read<FeatureCollection>(json);
            var timeFormat = "yyyy-MM-dd-HH.mm.ss";
            var samples = new List<MatcherSample>();
            foreach (var i in fc.Features)
            {
                var p = i.Geometry as IPoint;
                var coord2D = new Coordinate2D(p.X, p.Y);
                var timeStr = i.Attributes["time"].ToString().Substring(0, timeFormat.Length);
                var time = DateTimeOffset.ParseExact(timeStr, timeFormat, CultureInfo.InvariantCulture);
                var longTime = time.ToUnixTimeMilliseconds();
                yield return new MatcherSample(longTime, time, coord2D);
            }
        }

        private void OfflineMatch(
            Matcher<MatcherCandidate, MatcherTransition, MatcherSample> matcher,
            IReadOnlyList<MatcherSample> samples)
        {
            var kstate = new MatcherKState();

            //Do the offline map-matching
            MessageBox.Show("Doing map-matching...");
            var startedOn = DateTime.Now;
            foreach (var sample in samples)
            {
                var vector = matcher.Execute(kstate.Vector(), kstate.Sample, sample);
                kstate.Update(vector, sample);
            }

            MessageBox.Show("Fetching map-matching results...");
            var candidatesSequence = kstate.Sequence();
            var timeElapsed = DateTime.Now - startedOn;
            //MessageBox.Show("Map-matching elapsed time: {0}, Speed={1} samples/second", timeElapsed, samples.Count / timeElapsed.TotalSeconds);
            //MessageBox.Show("Results: [count={0}]", candidatesSequence.Count());
            var csvLines = new List<string>();
            csvLines.Add("time,lng,lat,azimuth");
            int matchedCandidateCount = 0;
            foreach (var cand in candidatesSequence)
            {
                var roadId = cand.Point.Edge.RoadInfo.Id; // original road id
                var heading = cand.Point.Edge.Headeing; // heading
                var coord = cand.Point.Coordinate; // GPS position (on the road)
                csvLines.Add(string.Format("{0},{1},{2},{3}", cand.Sample.Time.ToUnixTimeSeconds(), coord.X, coord.Y, cand.Point.Azimuth));
                if (cand.HasTransition)
                {
                    var geom = cand.Transition.Route.ToGeometry(); // path geometry(LineString) from last matching candidate
                    //cand.Transition.Route.Edges // Road segments between two GPS position
                }
                matchedCandidateCount++;
            }
            //MessageBox.Show("Matched Candidates: {0}, Rate: {1}%", matchedCandidateCount, matchedCandidateCount * 100 / samples.Count());

            var csvFile = "samples.output.csv";
            MessageBox.Show("Writing output file: {0}", csvFile);
            File.WriteAllLines(csvFile, csvLines);
        }

        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            InitializeMatching();
        }
    }
}