using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using GMap.NET;
using GMap.NET.MapProviders;
using GMap.NET.WindowsForms;
using GMap.NET.WindowsForms.Markers;
using Newtonsoft.Json;
using OsmSharp;
using OsmSharp.Streams;
using QuickGraph;

namespace TestMap
{
    public partial class MainForm : Form
    {
        private GMapControl gMapControl;

        string PbfPath;
        string JsonPath;

        PointLatLng? selectionStart = null;
        private PointLatLng? selectionEnd = null;
        private GMapOverlay selectionOverlay = new GMapOverlay("selection");
        private GMapPolygon selectionPolygon = null;
        private bool isSelecting = false;
        double minLat;
        double maxLat;
        double minLng;
        double maxLng;

        // Не будем использовать граф, если не нужен для поиска маршрута
        //private BidirectionalGraph<PointLatLng, Edge<PointLatLng>> graph = new BidirectionalGraph<PointLatLng, Edge<PointLatLng>>();

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
            JsonPath = @"filtered_roads.json";
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
            var source = new PBFOsmStreamSource(System.IO.File.OpenRead(pbfPath));
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
            source = new PBFOsmStreamSource(System.IO.File.OpenRead(pbfPath));

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

        private void SaveRoadsToJson(string filePath)
        {
            var roadsForSerialization = roads.Select(road =>
                road.Select(p => new { lat = p.Lat, lng = p.Lng }).ToList()
            ).ToList();

            string json = JsonConvert.SerializeObject(roadsForSerialization, Formatting.Indented);
            System.IO.File.WriteAllText(filePath, json);

            MessageBox.Show($"Дороги сохранены в {filePath}");
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
            SaveRoadsToJson(JsonPath);
        }

        private void toolStripButton_Clear_Click(object sender, EventArgs e)
        {
            ClearMap();
        }

        private void ClearMap()
        {
            gMapControl.Overlays.Clear();

            
            selectionOverlay.Polygons.Clear();
            //gMapControl.Overlays.Remove(selectionOverlay);
            
            gMapControl.Refresh();
        }

        private void toolStripButton_Load_Click(object sender, EventArgs e)
        {
            openFileDialog1.ShowDialog();
            JsonPath = openFileDialog1.FileName;
            string jsonString = System.IO.File.ReadAllText(JsonPath);

            ClearMap();
            roads.Clear();
            roads = JsonConvert.DeserializeObject<List<List<PointLatLng>>>(jsonString);

            DrawRoadsOnMap(Color.Red);
        }

        private void toolStripButton_Save_Click(object sender, EventArgs e)
        {
            // Фильтруем дороги
            var filteredRoads = roads.Where(road =>
                road.Any(p => p.Lat >= minLat && p.Lat <= maxLat && p.Lng >= minLng && p.Lng <= maxLng)
            ).ToList();

            // Сохраняем отфильтрованные дороги
            string json = JsonConvert.SerializeObject(filteredRoads.Select(r =>
                r.Select(p => new { lat = p.Lat, lng = p.Lng }).ToList()
            ).ToList(), Formatting.Indented);

            saveFileDialog1.Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*";
            if (saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                System.IO.File.WriteAllText(saveFileDialog1.FileName, json);
                MessageBox.Show($"Сохранено {filteredRoads.Count} дорог(и) в {saveFileDialog1.FileName}");
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
    }
}