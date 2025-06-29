using UnityEngine;
using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine.UIElements;

namespace ME.Monitoring {

    public class Line : VisualElement {

        private Group group;
        private float radius = 3f;
        private float radiusStart = 8f;
        public float thickness = 1f;
        public float minStraightLength = 8f;
        public float dashLength = 4f;
        public float dashSpacing = 6f;
        private float dashOffset = 0f;
        private float dashSpeed = 40f;

        private Vector2 currentStart;
        private Vector2 currentEnd;
        private float positionLerpSpeed = 10f;
        private float lastUpdateTime;

        public Line(Group group) {
            this.group = group;
        }

        public void Initialize() {

            var initialTarget = this.group.GetTargetPosition();
            var initialPos = this.group.GetNearestCorner(initialTarget);
            this.currentStart = initialPos;
            this.currentEnd = initialTarget;

            this.generateVisualContent += this.GenerateVisual;

        }

        private void GenerateVisual(MeshGenerationContext context) {
            var painter = context.painter2D;
            painter.strokeColor = Color.white;
            painter.lineWidth = this.thickness;

            {
                float currentTime = Time.realtimeSinceStartup;
                float deltaTime = currentTime - this.lastUpdateTime;
                this.lastUpdateTime = currentTime;

                this.dashOffset += this.dashSpeed * deltaTime;
                float cycleLength = this.dashLength + this.dashSpacing;
                if (this.dashOffset > cycleLength) this.dashOffset -= cycleLength;

                Vector2 targetPos = this.group.GetTargetPosition();
                Vector2 nearestCorner = this.group.GetNearestCorner(targetPos);

                this.currentStart = Vector2.Lerp(this.currentStart, nearestCorner, this.positionLerpSpeed * deltaTime);
                this.currentEnd = Vector2.Lerp(this.currentEnd, targetPos, this.positionLerpSpeed * deltaTime);
            }

            var start = this.currentStart;
            var end = this.currentEnd;

            Vector2 corner;
            var dx = end.x - start.x;
            var dy = end.y - start.y;

            var verticalFirst = Mathf.Abs(dy) > Mathf.Abs(dx);

            if (verticalFirst) {
                var signY = Mathf.Sign(dy);
                var signX = Mathf.Sign(dx);

                var diagLength = Mathf.Min(Mathf.Abs(dx), Mathf.Abs(dy));
                var straightY = dy - signY * diagLength;

                if (Mathf.Abs(straightY) < this.minStraightLength) {
                    straightY = signY * this.minStraightLength;
                }

                corner = new Vector2(start.x, start.y + straightY);
            } else {
                var signX = Mathf.Sign(dx);
                var signY = Mathf.Sign(dy);

                var diagLength = Mathf.Min(Mathf.Abs(dx), Mathf.Abs(dy));
                var straightX = dx - signX * diagLength;

                if (Mathf.Abs(straightX) < this.minStraightLength) {
                    straightX = signX * this.minStraightLength;
                }

                corner = new Vector2(start.x + straightX, start.y);
            }

            this.DrawDashedLine(painter, start, corner, this.dashLength, this.dashSpacing, this.dashOffset);
            this.DrawDashedLine(painter, corner, end, this.dashLength, this.dashSpacing, this.dashOffset);

            context.painter2D.BeginPath();
            context.painter2D.fillColor = Color.white;
            context.painter2D.Arc(end, this.radius, 0f, 360f);
            context.painter2D.Fill();

            context.painter2D.BeginPath();
            context.painter2D.fillColor = Color.black;
            context.painter2D.Arc(start, this.radiusStart, 0f, 360f);
            context.painter2D.Fill();
        }

        private void DrawDashedLine(Painter2D painter, Vector2 from, Vector2 to, float dashLength, float spacing, float offset) {
            var dir = (to - from).normalized;
            var length = Vector2.Distance(from, to);
            var cycleLength = dashLength + spacing;

            var total = -offset;

            while (total < length) {
                var startSegment = Mathf.Max(total, 0f);
                var segmentLength = Mathf.Min(dashLength, length - startSegment);

                if (segmentLength > 0) {
                    var p1 = from + dir * startSegment;
                    var p2 = p1 + dir * segmentLength;

                    painter.BeginPath();
                    painter.MoveTo(p1);
                    painter.LineTo(p2);
                    painter.Stroke();
                }

                total += cycleLength;
            }
        }

        public void UpdateParent() {
            this.group.parent.Add(this);
            this.SendToBack();
            this.MarkDirtyRepaint();
        }

    }

    public class Group : VisualElement {

        public GeoMap.ServerInfo serverInfo;
        private Camera cameraObj;
        private readonly Line line;
        public readonly City city;

        public Group(GeoMap.ServerInfo serverInfo, Camera cameraObj) {
            this.serverInfo = serverInfo;
            this.cameraObj = cameraObj;
            this.line = new Line(this);
            this.city = new City(this, serverInfo, cameraObj);
        }

        public void Initialize() {
            this.line.Initialize();
            this.city.Initialize();
        }

        public void MarkDirty() {
            this.line.MarkDirtyRepaint();
            this.city.MarkDirtyRepaint();
        }

        public void UpdateLine() {
            this.line.UpdateParent();
            this.city.UpdateParent();
        }

        public Vector2 GetNearestCorner(Vector2 targetPoint) {
            var rect = this.worldBound;
            var corner1 = rect.min;
            var corner2 = rect.max;
            var corner3 = new Vector2(rect.min.x, rect.max.y);
            var corner4 = new Vector2(rect.max.x, rect.min.y);
            var d1 = (corner1 - targetPoint).sqrMagnitude;
            var d2 = (corner2 - targetPoint).sqrMagnitude;
            var d3 = (corner3 - targetPoint).sqrMagnitude;
            var d4 = (corner4 - targetPoint).sqrMagnitude;
            if (d1 < d2 && d1 < d3 && d1 < d4) {
                return corner1;
            }

            if (d2 < d1 && d2 < d3 && d2 < d4) {
                return corner2;
            }

            if (d3 < d2 && d3 < d4 && d3 < d1) {
                return corner3;
            }

            if (d4 < d2 && d4 < d3 && d4 < d1) {
                return corner4;
            }

            return corner1;
        }

        public Vector2 GetTargetPosition() {
            return RuntimePanelUtils.CameraTransformWorldToPanel(this.panel, this.serverInfo.position, this.cameraObj);
        }

    }

    public class DirectLine : VisualElement {

        private VisualElement container;
        private Vector3 from;

        public DirectLine() {
            this.generateVisualContent += this.GenerateVisual;
        }

        public void Set(Vector3 from, VisualElement container) {
            this.container = container;
            this.from = from;
        }
        
        private void GenerateVisual(MeshGenerationContext context) {
            var pos = this.from;
            var targetPos = new Rect(this.container.resolvedStyle.left, this.container.resolvedStyle.top, this.container.resolvedStyle.width, this.container.resolvedStyle.height);
            var bounds = new Bounds(targetPos.center, targetPos.size);
            var painter = context.painter2D;
            painter.lineWidth = 1f;
            painter.strokeColor = this.container.resolvedStyle.color;
            painter.BeginPath();
            painter.MoveTo(pos);
            painter.LineTo(bounds.ClosestPoint(pos));
            painter.Stroke();

        }

    }
    
    public class City : VisualElement {

        private Camera cameraObj;
        private GeoMap.ServerInfo serverInfo;
        private Transform tr;
        private VisualElement group;
        private VisualElement container;
        private Label regionName;
        private Label city;
        private DirectLine directLine;

        public City(VisualElement group, GeoMap.ServerInfo serverInfo, Camera cameraObj) {
            this.group = group;
            this.serverInfo = serverInfo;
            this.cameraObj = cameraObj;
        }

        public City(VisualElement group, Transform tr, GeoMap.LocationGeoData geoData, Camera cameraObj) {
            this.group = group;
            this.tr = tr;
            this.serverInfo = new GeoMap.ServerInfo() { geo = geoData };
            this.cameraObj = cameraObj;
        }

        public void SetInfo(GeoMap.LocationGeoData location, Transform tr) {
            this.serverInfo.geo = location;
            this.tr = tr;
            this.UpdateInfo();
        }

        private void UpdateInfo() {
            this.regionName.text = this.serverInfo.geo.regionName;
            this.city.text = this.serverInfo.geo.city;
        }

        public Rect GetRect() {
            return new Rect(this.GetPosition(), new Vector2(this.container.resolvedStyle.width, this.container.resolvedStyle.height));
        }

        public void SetRect(Rect bestRect) {
            this.container.style.left = bestRect.x;
            this.container.style.top = bestRect.y;
        }

        public Vector2 GetPosition() {
            if (this.tr != null) return RuntimePanelUtils.CameraTransformWorldToPanel(this.panel, this.tr.position, this.cameraObj);
            return RuntimePanelUtils.CameraTransformWorldToPanel(this.panel, this.serverInfo.position, this.cameraObj);
        }

        public void Initialize() {
            var container = new VisualElement();
            this.Add(container);
            this.container = container;
            container.AddToClassList("city-container");
            var regionName = new Label(this.serverInfo.geo.regionName);
            this.regionName = regionName;
            regionName.AddToClassList("region-name");
            container.Add(regionName);
            var city = new Label(this.serverInfo.geo.city);
            this.city = city;
            city.AddToClassList("city");
            container.Add(city);
            this.directLine = new DirectLine();
        }

        public void UpdateParent() {
            this.group.parent.Add(this);
            this.group.parent.Add(this.directLine);
            this.directLine.Set(this.GetPosition(), this.container);
            this.directLine.SendToBack();
            this.directLine.MarkDirtyRepaint();
            this.SendToBack();
            this.MarkDirtyRepaint();
        }

    }

    public static class TraceRoute {

        private const string Data = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        private static System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>> cache = new();

        public static async Task<System.Collections.Generic.List<string>> RunTraceRouteAsync(string host) {
            string command;
            string args;

            #if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            command = "tracert";
            args = host;
            #elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX
            command = "traceroute";
            args = host;
            #else
            Debug.LogError("Unsupported platform for traceroute");
            return new List<string>();
            #endif

            var hopIPs = new System.Collections.Generic.List<string>();
            var lines = new System.Collections.Generic.List<string>();

            var psi = new System.Diagnostics.ProcessStartInfo {
                FileName = command,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8
            };

            try {
                using (var process = new System.Diagnostics.Process { StartInfo = psi, EnableRaisingEvents = true }) {
                    var tcs = new TaskCompletionSource<bool>();

                    process.OutputDataReceived += (s, e) => {
                        if (!string.IsNullOrEmpty(e.Data)) {
                            lines.Add(e.Data);
                        }
                    };

                    process.Exited += (s, e) => tcs.TrySetResult(true);

                    process.Start();
                    process.BeginOutputReadLine();

                    await tcs.Task;

                    // Парсинг IP-адресов
                    System.Text.RegularExpressions.Regex ipRegex = new System.Text.RegularExpressions.Regex(@"\b(?:\d{1,3}\.){3}\d{1,3}\b");
                    foreach (string line in lines) {
                        var match = ipRegex.Match(line);
                        if (match.Success) {
                            string ip = match.Value;
                            if (!hopIPs.Contains(ip)) {
                                hopIPs.Add(ip);
                            }
                        }
                    }

                    return hopIPs;
                }
            } catch (Exception ex) {
                Debug.LogError("Failed to run traceroute: " + ex.Message);
                return new System.Collections.Generic.List<string>();
            }
        }
        
        public static async Task<System.Collections.Generic.List<string>> GetTraceRoute(string hostNameOrAddress) {
            if (cache.TryGetValue(hostNameOrAddress, out var res) == true) {
                return res;
            }

            res = await RunTraceRouteAsync(hostNameOrAddress);//GetTraceRoute_INTERNAL(hostNameOrAddress);
            cache.Add(hostNameOrAddress, res);
            return res;
        }

        private static async Task<System.Collections.Generic.List<System.Net.IPAddress>> GetTraceRoute_INTERNAL(string hostNameOrAddress) {
            const int timeout = 10000;
            const int maxTTL = 30;
            const int bufferSize = 32;

            var buffer = new byte[bufferSize];
            new System.Random().NextBytes(buffer);

            var result = new System.Collections.Generic.List<System.Net.IPAddress>();
            using (var pinger = new System.Net.NetworkInformation.Ping()) {
                for (var ttl = 1; ttl <= maxTTL; ttl++) {
                    var options = new System.Net.NetworkInformation.PingOptions(ttl, true);
                    var reply = await pinger.SendPingAsync(hostNameOrAddress, timeout, buffer, options);

                    // we've found a route at this ttl
                    if (reply.Status == System.Net.NetworkInformation.IPStatus.Success || reply.Status == System.Net.NetworkInformation.IPStatus.TtlExpired) {
                        result.Add(reply.Address);
                    }

                    // if we reach a status other than expired or timed out, we're done searching or there has been an error
                    if (reply.Status != System.Net.NetworkInformation.IPStatus.TtlExpired && reply.Status != System.Net.NetworkInformation.IPStatus.TimedOut) {
                        break;
                    }
                }
            }

            return result;
        }

    }
    
    public class GeoMap : MonoBehaviour {

        public Transform cameraTr;
        public Camera cameraObj;
        public float cameraBoundsDelta = 30f;
        public float moveSpeed = 1f;
        public float uiFollowSpeed = 10f;
        public int width;
        public int height;
        private readonly ParticleSystem.Particle[] particles = new ParticleSystem.Particle[10000];
        public ParticleSystem particleSystem;
        public int particlesPerMeter = 1;
        public float particlesDense = 10f;
        public float particlesSpeed = 2f;
        public AnimationCurve curve;
        public float curveHeight = 3f;
        private int emittedParticles;

        public class City {

            public string city;
            public string land;
            public double latitude;
            public double longitude;
            public float population;
            public string country;

        }

        [System.Serializable]
        public struct LocationGeoData {

            public string name;
            public string status; // "success"
            public string message;
            public double lat;
            public double lon;
            public string regionName;
            public string city;
            
        }

        public class ServerInfo {

            public Status status;
            public LocationGeoData geo;
            public Vector3 position;

            public int particlesIndex;
            public int particlesCount;
            public float offset;

        }

        private readonly Vector3[] cameraCorners = new Vector3[4];
        private System.Collections.Generic.List<City> cities;
        private readonly System.Collections.Generic.List<Matrix4x4> citiesMatrix = new();
        private readonly System.Collections.Generic.List<Matrix4x4> serversAwaitMatrix = new();
        private readonly System.Collections.Generic.List<Matrix4x4> serversSuccessMatrix = new();
        private readonly System.Collections.Generic.List<Matrix4x4> serversFailedMatrix = new();
        private readonly System.Collections.Generic.List<Matrix4x4> serversWarningMatrix = new();
        public GameObject serverPrefab;
        public Material serverAwaitMaterial;
        public Material serverSuccessMaterial;
        public Material serverWarningMaterial;
        public Material serverFailedMaterial;
        public GameObject cityPrefab;
        public Transform locationPrefab;
        private Transform currentLocation;
        private LocationGeoData location;
        private readonly System.Collections.Generic.Dictionary<string, ServerInfo> servers = new();
        private readonly System.Collections.Generic.List<Task<LocationGeoData>> serversAwaitList = new();

        public async void Start() {
            if (this.cities == null) {
                this.cities = LoadCities("Assets/WorldMap/coords.csv");
            }

            foreach (var city in this.cities) {
                this.citiesMatrix.Add(Matrix4x4.TRS(this.GetPosition(city.latitude, city.longitude), this.cityPrefab.transform.rotation, this.cityPrefab.transform.localScale));
            }

            this.currentLocation = Instantiate(this.locationPrefab, new Vector3(0f, 0f, 0f), this.locationPrefab.rotation);
            await this.GetMyLocation();
        }

        public static System.Collections.Generic.List<City> LoadCities(string csvPath) {
            var list = new System.Collections.Generic.List<City>();
            foreach (var line in System.IO.File.ReadLines(csvPath).Skip(1)) {
                var parts = line.Split(',');
                try {
                    list.Add(new City {
                        city = parts[0],
                        land = parts[1],
                        latitude = double.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture),
                        longitude = double.Parse(parts[3], System.Globalization.CultureInfo.InvariantCulture),
                        population = float.Parse(parts[4], System.Globalization.CultureInfo.InvariantCulture),
                        country = parts[5],
                    });
                } catch (Exception e) {
                    Debug.Log(e.Message);
                }
            }

            return list;
        }

        private Vector3 GetPosition(double latitude, double longitude) {
            (var lat, var lon) = LatLonToNormalized(latitude, longitude);
            return new Vector3((float)lat * this.width, 0f, (float)lon * this.height);
        }

        public async Task GetMyLocation() {
            #if UNITY_ANDROID && !UNITY_EDITOR
        if (UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.FineLocation) == false) {
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.FineLocation);
        }
        if (Input.location.status == LocationServiceStatus.Stopped) Input.location.Start();
            #elif UNITY_IOS && !UNITY_EDITOR
        if (Input.location.status == LocationServiceStatus.Stopped) Input.location.Start();
            #else
            this.location = await this.GetLocation(string.Empty);
            #endif
        }

        private static readonly System.Collections.Generic.Dictionary<string, LocationGeoData> cacheLocations = new System.Collections.Generic.Dictionary<string, LocationGeoData>();
        public async Task<LocationGeoData> GetLocation(string ipAddress) {
            if (cacheLocations.TryGetValue(ipAddress, out var locationData) == true) return locationData;
            var req = UnityEngine.Networking.UnityWebRequest.Get($"http://ip-api.com/json{(string.IsNullOrEmpty(ipAddress) == false ? $"/{ipAddress}" : string.Empty)}");
            var op = req.SendWebRequest();
            while (op.isDone == false) {
                await System.Threading.Tasks.Task.Yield();
            }

            {
                if (req.result == UnityEngine.Networking.UnityWebRequest.Result.Success) {
                    try {
                        var data = JsonUtility.FromJson<LocationGeoData>(req.downloadHandler.text);
                        data.name = ipAddress;
                        cacheLocations.Add(ipAddress, data);
                        return data;
                    } catch (System.Exception ex) {
                        Debug.LogError($"Could not get geo data: {ex}");
                    }
                }
            }
            req.Dispose();
            return new LocationGeoData() { name = ipAddress };
        }

        public ServerInfo AddServer(System.Net.IPHostEntry host, Status status) {
            var ip = host.AddressList[0].ToString();
            if (this.servers.TryGetValue(ip, out var geo) == false) {
                geo = new ServerInfo() {
                    status = status,
                };
                this.servers.Add(ip, geo);
                this.serversAwaitList.Add(this.GetLocation(ip));
            }

            return geo;
        }

        private void AddServerToGroup(ServerInfo info) {
            var element = this.serversElements.First(x => ((System.Collections.Generic.KeyValuePair<string, ServerInfo>)x.userData).Value == info);
            if (this.groups.TryGetValue(info.position, out var list) == false) {
                var group = new Group(info, this.cameraObj);
                group.AddToClassList("geo-group");
                group.AddToClassList("servers");
                element.parent.Add(group);
                group.Add(element);
                group.Initialize();
                group.UpdateLine();
                {
                    var item = this.mainScreen.GetItem(info);
                    group.Add(item);
                }
                list = new System.Collections.Generic.List<((VisualElement element, Group group) visual, ServerInfo serverInfo)>();
                list.Add(((element, group), info));
                this.groups.Add(info.position, list);
            } else {
                var group = list[0].visual.group;
                group.Add(element);
                {
                    var item = this.mainScreen.GetItem(info);
                    group.Add(item);
                }
                list.Add(((element, group), info));
            }
        }

        private readonly System.Collections.Generic.List<VisualElement> serversElements = new();
        private MainScreen mainScreen;

        public void Draw(VisualElement root, MainScreen mainScreen) {
            this.mainScreen = mainScreen;
            root.Clear();
            this.root = root;
            
            this.currentLocationCity = new ME.Monitoring.City(root, this.currentLocation, this.location, this.cameraObj);
            this.root.Add(this.currentLocationCity);
            this.currentLocationCity.Initialize();
            this.currentLocationCity.UpdateParent();
            
            this.serversElements.Clear();

            foreach (var item in this.servers) {
                //var info = item.Value;
                var element = new VisualElement();
                element.userData = item;
                this.serversElements.Add(element);
                element.AddToClassList("server-geo-info");
                root.Add(element);
                var label = new Label(item.Key);
                element.Add(label);
            }

        }

        public class Request {

            public UnityEngine.Networking.UnityWebRequest[] requests;
            public Image image;
            private bool isSuccess;

            public void Update() {

                if (this.requests == null) return;

                var failedCount = 0;
                for (var index = 0; index < this.requests.Length; ++index) {
                    var request = this.requests[index];
                    if (request.isDone == true && request.result == UnityEngine.Networking.UnityWebRequest.Result.Success) {
                        try {
                            var icoBytes = request.downloadHandler.data;
                            var tex = IcoConverter.Convert(icoBytes);
                            if (tex != null) {
                                this.image.image = tex;
                                this.image.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.Flex);
                                this.isSuccess = true;
                                Debug.Log("Success: " + this.image.image);
                            } else {
                                throw new Exception("LoadImage failed for request: " + request.url);
                            }
                        } catch (Exception ex) {
                            Debug.LogError(ex);
                            ++failedCount;
                            this.image.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
                        }
                    } else if (request.isDone == true) {
                        ++failedCount;
                    }
                }

                if (failedCount == this.requests.Length) {
                    this.requests = null;
                    this.image.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
                } else if (this.isSuccess == false) {
                    this.image.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
                } else {
                    this.requests = null;
                }

            }

        }
        private readonly System.Collections.Generic.List<Request> waitRequests = new System.Collections.Generic.List<Request>();
        public void LoadIcon(Image image, Status status) {
            var requests = status.GetIconRequest();
            this.waitRequests.Add(new Request() {
                requests = requests,
                image = image,
            });
        }

        public float baseRadius = 50f;
        public float radiusStep = 20f;
        public float serverRadius = 50f;
        public float baseRadiusCity = 0f;
        public float radiusStepCity = 20f;
        private readonly System.Collections.Generic.List<(Rect rect, VisualElement element)> placedRects = new();
        private readonly System.Collections.Generic.Dictionary<Vector3, System.Collections.Generic.List<((VisualElement element, Group group) visual, ServerInfo serverInfo)>> groups = new();
        private VisualElement root;
        private ME.Monitoring.City currentLocationCity;
        
        private void UpdateCity(ME.Monitoring.City element) {
            var panelSize = element.parent.localBound;
            var rect = element.GetRect();

            var elementSize = rect.size;
            var baseRadius = this.baseRadiusCity;
            var radiusStep = this.radiusStepCity;
            var angleSteps = 12;
            var maxRings = 5;

            var foundPlacement = false;
            var bestRect = new Rect(rect.x, rect.y, elementSize.x, elementSize.y);

            for (var ring = 0; ring < maxRings && !foundPlacement; ring++) {
                var radius = baseRadius + ring * radiusStep;
                for (var step = 0; step < angleSteps; step++) {
                    var angle = 360f / angleSteps * step;
                    var rad = angle * Mathf.Deg2Rad;
                    var dx = Mathf.Cos(rad) * radius;
                    var dy = Mathf.Sin(rad) * radius;

                    var tryRect = new Rect(
                        rect.x + dx - elementSize.x / 2f,
                        rect.y + dy - elementSize.y / 2f,
                        elementSize.x,
                        elementSize.y
                    );

                    if (!this.IsOverlapping(tryRect, this.placedRects) && this.IsInside(tryRect, panelSize)) {
                        bestRect = tryRect;
                        foundPlacement = true;
                        break;
                    }
                }
            }

            var clampedX = Mathf.Clamp(bestRect.x, 0, panelSize.width - bestRect.size.x);
            var clampedY = Mathf.Clamp(bestRect.y, 0, panelSize.height - bestRect.size.y);

            element.SetRect(new Rect(clampedX, clampedY, elementSize.x, elementSize.y));
            element.MarkDirtyRepaint();

            var clampedRect = new Rect(clampedX, clampedY, bestRect.width, bestRect.height);
            this.placedRects.Add((clampedRect, element));
        }
        
        private void UpdateUI() {

            this.placedRects.Clear();
            
            foreach (var req in this.waitRequests) req.Update();

            this.UpdateCity(this.currentLocationCity);
            foreach (var item in this.groups) {
                var element = item.Value[0].visual.group.city;
                this.UpdateCity(element);
            }

            this.placedRects.Clear();
            foreach (var item in this.servers) {
                var pos = item.Value.position;
                var rectPos = RuntimePanelUtils.CameraTransformWorldToPanel(this.root.panel, pos, this.cameraObj);
                var size = new Vector2(this.serverRadius, this.serverRadius);
                this.placedRects.Add((new Rect(rectPos - size * 0.5f, size), null));
            }

            foreach (var item in this.groups) {
                var element = item.Value[0].visual.group;
                var pos = element.GetTargetPosition();
                var panelSize = element.parent.localBound;
                var rect = new Rect(pos.x, pos.y, element.localBound.size.x, element.localBound.size.y);

                var elementSize = rect.size;
                var baseRadius = this.baseRadius;
                var radiusStep = this.radiusStep;
                var angleSteps = 12;
                var maxRings = 5;

                var foundPlacement = false;
                var bestRect = new Rect(pos.x, pos.y, elementSize.x, elementSize.y);

                for (var ring = 0; ring < maxRings && !foundPlacement; ring++) {
                    var radius = baseRadius + ring * radiusStep;
                    for (var step = 0; step < angleSteps; step++) {
                        var angle = 360f / angleSteps * step;
                        var rad = angle * Mathf.Deg2Rad;
                        var dx = Mathf.Cos(rad) * radius;
                        var dy = Mathf.Sin(rad) * radius;

                        var tryRect = new Rect(
                            pos.x + dx - elementSize.x / 2f,
                            pos.y + dy - elementSize.y / 2f,
                            elementSize.x,
                            elementSize.y
                        );

                        if (!this.IsOverlapping(tryRect, this.placedRects) && this.IsInside(tryRect, panelSize)) {
                            bestRect = tryRect;
                            foundPlacement = true;
                            break;
                        }
                    }
                }

                var clampedX = Mathf.Clamp(bestRect.x, 0, panelSize.width - bestRect.size.x);
                var clampedY = Mathf.Clamp(bestRect.y, 0, panelSize.height - bestRect.size.y);

                element.style.left = Mathf.Lerp(element.style.left.value.value, clampedX, Time.deltaTime * this.uiFollowSpeed);
                element.style.top = Mathf.Lerp(element.style.top.value.value, clampedY, Time.deltaTime * this.uiFollowSpeed);
                element.MarkDirty();

                var clampedRect = new Rect(clampedX, clampedY, bestRect.width, bestRect.height);
                this.placedRects.Add((clampedRect, element));
            }

        }

        private bool IsOverlapping(Rect rect, System.Collections.Generic.List<(Rect, VisualElement)> others, float margin = 2f) {
            var expanded = new Rect(rect.x - margin, rect.y - margin, rect.width + margin * 2, rect.height + margin * 2);

            foreach ((var otherRect, _) in others) {
                if (expanded.Overlaps(otherRect)) {
                    return true;
                }
            }

            return false;
        }

        private bool IsInside(Rect rect, Rect panelBounds) {
            return rect.xMin >= 0 &&
                   rect.yMin >= 0 &&
                   rect.xMax <= panelBounds.width &&
                   rect.yMax <= panelBounds.height;
        }

        public void Update() {

            this.UpdateUI();

            var bounds = new Bounds() {
                center = this.currentLocation.position,
            };
            {
                this.serversAwaitMatrix.Clear();
                this.serversFailedMatrix.Clear();
                this.serversSuccessMatrix.Clear();
                this.serversWarningMatrix.Clear();
                foreach (var kv in this.servers) {
                    var info = kv.Value;
                    var state = info.status.GetState();
                    var pos = info.position;
                    bounds.Encapsulate(pos);
                    var matrix = Matrix4x4.TRS(pos, this.serverPrefab.transform.rotation, this.serverPrefab.transform.localScale);
                    if (state == Status.State.None) {
                        this.serversAwaitMatrix.Add(matrix);
                    } else if (state == Status.State.Success) {
                        this.serversSuccessMatrix.Add(matrix);
                    } else if (state == Status.State.Warning) {
                        this.serversWarningMatrix.Add(matrix);
                    } else if (state == Status.State.Failed) {
                        this.serversFailedMatrix.Add(matrix);
                    }
                }
            }

            {
                if (this.particleSystem.particleCount < this.emittedParticles) {
                    this.particleSystem.Emit(this.emittedParticles - this.particleSystem.particleCount);
                }

                var count = this.particleSystem.GetParticles(this.particles);
                foreach (var kv in this.servers) {
                    var serverInfo = kv.Value;
                    serverInfo.offset += Time.deltaTime * this.particlesSpeed;
                    var pos = serverInfo.position;
                    var d = Vector3.Distance(this.currentLocation.position, pos);
                    for (var i = serverInfo.particlesIndex; i < serverInfo.particlesIndex + serverInfo.particlesCount; ++i) {
                        ref var p = ref this.particles[i];
                        var t = Mathf.PingPong(serverInfo.offset + i * this.particlesDense, 1f);
                        var tpos = Vector3.Lerp(this.currentLocation.position, pos, t);
                        tpos.y += this.curve.Evaluate(t) * Mathf.Min(this.curveHeight, d * 0.5f);
                        p.position = tpos;
                        var state = serverInfo.status.GetState();
                        if (state == Status.State.Failed) {
                            p.startColor = Color.red;
                        } else if (state == Status.State.Success) {
                            p.startColor = Color.green;
                        } else if (state == Status.State.Warning) {
                            p.startColor = Color.yellow;
                        }

                        p.remainingLifetime = 10000f;
                    }
                }

                this.particleSystem.SetParticles(this.particles, count);
            }

            this.cameraTr.position = Vector3.Lerp(this.cameraTr.position, bounds.center, Time.deltaTime * this.moveSpeed);
            if ((this.cameraTr.position - bounds.center).sqrMagnitude <= 1f) {
                this.cameraObj.CalculateFrustumCorners(new Rect(0, 0, 1, 1), Vector3.Distance(this.cameraTr.position, this.cameraObj.transform.position),
                                                       Camera.MonoOrStereoscopicEye.Mono, this.cameraCorners);
                var min = this.cameraTr.position.x + this.cameraCorners[0].x;
                var max = this.cameraTr.position.x + this.cameraCorners[3].x;
                if (bounds.min.x - this.cameraBoundsDelta > min && bounds.max.x + this.cameraBoundsDelta < max) {
                    this.cameraObj.fieldOfView = Mathf.Lerp(this.cameraObj.fieldOfView, 10f, Time.deltaTime * this.moveSpeed);
                }
            } else {
                this.cameraObj.fieldOfView = Mathf.Lerp(this.cameraObj.fieldOfView, 60f, Time.deltaTime * this.moveSpeed);
            }

            if (this.citiesMatrix.Count > 0) {
                Graphics.RenderMeshInstanced(new RenderParams(this.cityPrefab.GetComponent<MeshRenderer>().sharedMaterial), this.cityPrefab.GetComponent<MeshFilter>().sharedMesh,
                                             0,
                                             this.citiesMatrix);
            }

            if (this.serversAwaitMatrix.Count > 0) {
                Graphics.RenderMeshInstanced(new RenderParams(this.serverAwaitMaterial), this.serverPrefab.GetComponent<MeshFilter>().sharedMesh, 0, this.serversAwaitMatrix);
            }

            if (this.serversFailedMatrix.Count > 0) {
                Graphics.RenderMeshInstanced(new RenderParams(this.serverFailedMaterial), this.serverPrefab.GetComponent<MeshFilter>().sharedMesh, 0, this.serversFailedMatrix);
            }

            if (this.serversSuccessMatrix.Count > 0) {
                Graphics.RenderMeshInstanced(new RenderParams(this.serverSuccessMaterial), this.serverPrefab.GetComponent<MeshFilter>().sharedMesh, 0, this.serversSuccessMatrix);
            }

            if (this.serversWarningMatrix.Count > 0) {
                Graphics.RenderMeshInstanced(new RenderParams(this.serverWarningMaterial), this.serverPrefab.GetComponent<MeshFilter>().sharedMesh, 0, this.serversWarningMatrix);
            }

            for (var index = this.serversAwaitList.Count - 1; index >= 0; --index) {
                var task = this.serversAwaitList[index];
                if (task.IsCompleted == true) {
                    var item = this.servers[task.Result.name];
                    item.geo = task.Result;
                    item.position = this.GetPosition(item.geo.lat, item.geo.lon);
                    this.AddServerToGroup(item);
                    item.particlesIndex = this.emittedParticles;
                    item.particlesCount = (int)(Vector3.Distance(this.currentLocation.position, item.position) / this.particlesPerMeter);
                    this.emittedParticles += item.particlesCount;
                    this.serversAwaitList.RemoveAt(index);
                }
            }

            {
                foreach (var group in this.groups) {
                    var g = group.Value[0].visual.group;
                    g.UpdateLine();
                }
            }

            if (Input.location.status == LocationServiceStatus.Running) {
                this.location = new LocationGeoData() {
                    status = "success",
                    lat = Input.location.lastData.latitude,
                    lon = Input.location.lastData.longitude,
                };
            }

            if (this.location.status == "success") {
                var lastData = this.location;
                this.currentLocationCity.SetInfo(this.location, this.currentLocation);
                this.currentLocation.position = this.GetPosition(lastData.lat, lastData.lon);
                this.currentLocationCity.UpdateParent();
                this.currentLocation.gameObject.SetActive(true);
            } else {
                this.currentLocation.gameObject.SetActive(false);
            }

        }

        private static (double x, double y) LatLonToNormalized(double latitude, double longitude) {
            latitude = Math.Clamp(latitude, -85.05112878f, 85.05112878f);
            longitude = Math.Clamp(longitude, -180f, 180f);

            var u = (longitude + 180f) / 360f;

            var latRad = latitude * Mathf.Deg2Rad;
            var mercN = Math.Log(Math.Tan(Math.PI / 4f + latRad / 2f));
            var v = 0.5f - mercN / (2f * Math.PI);
            v = 1.0 - v;

            return (u, v);
        }

        public LineRenderer tracePrefab;
        public void BuildRoute(Status.Tracert tracert) {
            if (tracert.lineRenderer == null) {
                tracert.lineRenderer = LineRenderer.Instantiate(this.tracePrefab, this.tracePrefab.transform.position, this.tracePrefab.transform.rotation);
            }
            var line = tracert.lineRenderer;
            line.positionCount = tracert.tracert.Count + 1;
            var offset = new Vector3(0f, this.tracePrefab.transform.position.y, 0f);
            line.SetPosition(0, this.currentLocation.position + offset);
            var k = 0;
            var prev = this.currentLocation.position;
            for (int i = 0; i < tracert.tracert.Count; ++i) {
                var trace = tracert.tracert[i];
                var geo = trace.geoData;
                var p = this.GetPosition(geo.lat, geo.lon);
                var left = p + new Vector3(-this.width, 0f, 0f);
                var right = p + new Vector3(this.width, 0f, 0f);
                var d0 = (prev - p).sqrMagnitude;
                var d1 = (prev - left).sqrMagnitude;
                var d2 = (prev - right).sqrMagnitude;
                if (d0 < d1 && d0 < d2) {
                    // d0
                    p = p;
                } else if (d1 < d2) {
                    // d1
                    p = left;
                } else {
                    // d2
                    p = right;
                }
                line.SetPosition(++k, p + offset);
                prev = p;
            }
        }

    }

}