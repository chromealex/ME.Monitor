using UnityEngine;
using UnityEngine.UIElements;

public enum Protocol {

    IP,
    Tcp,
    
    GET,
    POST,
    PUT,
    DELETE,

}

public class BaseGroup {
    
    public float refreshRate;
    public float refreshRatePing;
    public float refreshRateTcp;
    public float refreshRateRest;
    public int warningPing;

}

public struct BaseGroupConfig {

    public float refreshRate;
    public float refreshRatePing;
    public float refreshRateTcp;
    public float refreshRateRest;
    public int warningPing;

    public BaseGroupConfig(BaseGroup data) {
        this.refreshRate = data.refreshRate;
        this.refreshRatePing = data.refreshRatePing;
        this.refreshRateTcp = data.refreshRateTcp;
        this.refreshRateRest = data.refreshRateRest;
        this.warningPing = data.warningPing;
    }

    public BaseGroupConfig(BaseGroup data, BaseGroupConfig config) {
        this.refreshRate = GetRefreshRate(config.refreshRate, data.refreshRate);
        this.refreshRatePing = GetRefreshRate(config.refreshRatePing, data.refreshRatePing);
        this.refreshRateTcp = GetRefreshRate(config.refreshRateTcp, data.refreshRateTcp);
        this.refreshRateRest = GetRefreshRate(config.refreshRateRest, data.refreshRateRest);
        this.warningPing = GetWarningPing(config.warningPing, data.warningPing);
    }

    private static float GetRefreshRate(float refreshRate, float value) {
        if (value > 0) return value;
        return refreshRate;
    }

    private static int GetWarningPing(int warningPing, int value) {
        if (value > 0) return value;
        return warningPing;
    }

    public float GetRate(Protocol protocol) {
        if (protocol == Protocol.IP) {
            if (this.refreshRatePing > 0f) return this.refreshRatePing;
        } else if (protocol == Protocol.Tcp) {
            if (this.refreshRateTcp > 0f) return this.refreshRateTcp;
        } else if (protocol >= Protocol.GET) {
            if (this.refreshRateRest > 0f) return this.refreshRateRest;
        }
        return this.refreshRate;
    }

}

[System.Serializable]
public class ServerConfig : BaseGroup {

    public string protocolPrefix;
    public string host;
    public string method;
    public int port;
    public string description;
    public Protocol[] protocols;

}

[System.Serializable]
public class Config : BaseGroup {

    [System.Serializable]
    public class Group : BaseGroup {

        public string caption;
        public Group[] groups;
        public ServerConfig[] servers;
        
    }
    
    public Group[] groups;
    public ServerConfig[] servers;
    public float uiScale;

}

public class Status : VisualElement {

    private ServerConfig config;
    private VisualElement root;
    private Label[] labels;
    private Label[] msLabels;
    private object[] pings;
    private BaseGroupConfig dataConfig;
    private float[] timer;
    private VisualElement parentGroup;
    
    public Status(ServerConfig config, VisualElement parentGroup, VisualElement root, BaseGroupConfig dataConfig) {
        this.parentGroup = parentGroup;
        this.root = root;
        this.timer = new float[config.protocols.Length];
        this.dataConfig = dataConfig;
        this.config = config;
        this.labels = new Label[config.protocols.Length];
        this.msLabels = new Label[config.protocols.Length];
        this.pings = new object[config.protocols.Length];
        for (var i = 0; i < this.labels.Length; ++i) {
            var label = this.labels[i] = new Label();
            var ms = this.msLabels[i] = new Label();
            label.text = config.protocols[i].ToString();
            label.AddToClassList("status");
            ms.AddToClassList("ms");
            label.AddToClassList("checking");
            this.Add(label);
            this.Add(ms);
        }
    }

    public void Start() {
        for (var i = 0; i < this.labels.Length; ++i) {
            this.Start(i);
        }
    }

    public void Start(int i) {
        var protocol = this.config.protocols[i];
        if (protocol == Protocol.IP) {
            try {
                var host = System.Net.Dns.GetHostEntry(this.config.host);
                this.pings[i] = new UnityEngine.Ping(host.AddressList[0].ToString());
            } catch (System.Exception ex) {
                Debug.LogException(ex);
                this.Fail(i);
            }
        } else if (protocol == Protocol.Tcp) {
            var host = System.Net.Dns.GetHostEntry(this.config.host);
            var tcpClient = new System.Net.Sockets.TcpClient(host.AddressList[0].AddressFamily);
            tcpClient.SendTimeout = (int)(this.dataConfig.GetRate(Protocol.Tcp) * 1000);
            tcpClient.ReceiveTimeout = (int)(this.dataConfig.GetRate(Protocol.Tcp) * 1000);
            tcpClient.BeginConnect(host.AddressList, this.config.port, null, null);
            this.pings[i] = tcpClient;
        } else if (protocol >= Protocol.GET) {
            var prefix = $"{this.config.protocolPrefix}://";
            try {
                var obj = new UnityEngine.Networking.UnityWebRequest($"{prefix}{this.config.host}{(this.config.port > 0 ? $":{this.config.port}" : string.Empty)}{this.config.method}", protocol.ToString());
                obj.SendWebRequest();
                this.pings[i] = obj;
            } catch (System.Exception ex) {
                Debug.LogException(ex);
                this.Fail(i);
            }
        }
    }

    public void Dispose() {
        for (var i = 0; i < this.labels.Length; ++i) {
            this.Dispose(i);
        }
    }

    public void Dispose(int i) {
        var protocol = this.config.protocols[i];
        if (protocol == Protocol.IP) {
            (this.pings[i] as Ping)?.DestroyPing();
        } else if (protocol == Protocol.Tcp) {
            if (this.pings[i] is System.Net.Sockets.TcpClient req) {
                req.Close();
                req.Dispose();
            }
        } else if (protocol >= Protocol.GET) {
            if (this.pings[i] is UnityEngine.Networking.UnityWebRequest req) {
                req.Abort();
                req.Dispose();
            }
        }
    }

    private bool IsDone(int i) {
        var protocol = this.config.protocols[i];
        if (protocol == Protocol.IP) {
            if (this.pings[i] is Ping req) {
                return req.isDone == true;
            }
        } else if (protocol == Protocol.Tcp) {
            if (this.pings[i] is System.Net.Sockets.TcpClient req) {
                return true;
            }
        } else if (protocol >= Protocol.GET) {
            if (this.pings[i] is UnityEngine.Networking.UnityWebRequest req) {
                return req.isDone;
            }
        }
        return false;
    }

    private bool IsSuccess(int i) {
        var protocol = this.config.protocols[i];
        if (protocol == Protocol.IP) {
            if (this.pings[i] is Ping req) {
                return req.isDone == true && req.time >= 0 && req.time <= this.dataConfig.warningPing;
            }
        } else if (protocol == Protocol.Tcp) {
            if (this.pings[i] is System.Net.Sockets.TcpClient req) {
                return req.Connected == true;
            }
        } else if (protocol >= Protocol.GET) {
            if (this.pings[i] is UnityEngine.Networking.UnityWebRequest req) {
                return req.isDone == true && req.result == UnityEngine.Networking.UnityWebRequest.Result.Success;
            }
        }
        return false;
    }

    private bool IsWarning(int i) {
        var protocol = this.config.protocols[i];
        if (protocol == Protocol.IP) {
            if (this.pings[i] is Ping req) {
                return req.isDone == true && req.time > this.dataConfig.warningPing;
            }
        } else if (protocol == Protocol.Tcp) {
            if (this.pings[i] is System.Net.Sockets.TcpClient req) {
                return req.Connected == true;
            }
        } else if (protocol >= Protocol.GET) {
            if (this.pings[i] is UnityEngine.Networking.UnityWebRequest req) {
                return req.isDone == true && req.result == UnityEngine.Networking.UnityWebRequest.Result.Success;
            }
        }
        return false;
    }

    private string GetMs(int i) {
        var protocol = this.config.protocols[i];
        if (protocol == Protocol.IP) {
            if (this.pings[i] is Ping req) {
                if (req.time == -1) return "Request timeout";
                return $"{req.time}ms";
            }
        } else if (protocol == Protocol.Tcp) {
            if (this.pings[i] is System.Net.Sockets.TcpClient req) {
                if (req.Client == null) return "Aborted";
                if (req.Connected == false) return "Disconnected";
                return req.GetStream().CanRead == true ? "Connected" : "Disconnected";
            }
        } else if (protocol >= Protocol.GET) {
            if (this.pings[i] is UnityEngine.Networking.UnityWebRequest req) {
                return req.responseCode.ToString();
            }
        }
        return string.Empty;
    }

    private void Fail(int i) {
        var label = this.labels[i];
        label.RemoveFromClassList("checking");
        label.RemoveFromClassList("success");
        label.RemoveFromClassList("warning");
        label.AddToClassList("failed");
    }

    private void Warning(int i) {
        var label = this.labels[i];
        label.RemoveFromClassList("checking");
        label.RemoveFromClassList("success");
        label.RemoveFromClassList("failed");
        label.AddToClassList("warning");
    }

    public bool Update(float dt, out bool warnings, out bool await) {
        for (int i = 0; i < this.timer.Length; ++i) {
            this.timer[i] += dt;
            var rate = this.dataConfig.GetRate(this.config.protocols[i]);
            if (this.timer[i] >= rate) {
                this.timer[i] -= rate;
                this.Dispose(i);
                this.Start(i);
            }
        }

        await = false;
        var allSuccess = true;
        warnings = false;
        for (var i = 0; i < this.labels.Length; ++i) {
            var ping = this.pings[i];
            if (ping == null) continue;
            var label = this.labels[i];
            if (this.IsDone(i) == true) {
                label.RemoveFromClassList("checking");
                if (this.IsSuccess(i) == true) {
                    label.RemoveFromClassList("failed");
                    label.RemoveFromClassList("warning");
                    label.AddToClassList("success");
                } else if (this.IsWarning(i) == true) {
                    warnings = true;
                    this.Warning(i);
                } else {
                    allSuccess = false;
                    this.Fail(i);
                }
                this.msLabels[i].text = this.GetMs(i);
            } else {
                label.AddToClassList("checking");
                await = true;
            }
        }

        if (await == false) {
            if (warnings == true && allSuccess == true) {
                this.root.RemoveFromClassList("success");
                this.root.RemoveFromClassList("failed");
                this.root.AddToClassList("warning");
                /*if (this.parentGroup != null) {
                    this.parentGroup.AddToClassList("flag-warning");
                }*/
            } else if (allSuccess == true) {
                this.root.RemoveFromClassList("warning");
                this.root.RemoveFromClassList("failed");
                this.root.AddToClassList("success");
                /*if (this.parentGroup != null) {
                    this.parentGroup.AddToClassList("flag-success");
                }*/
            } else {
                this.root.RemoveFromClassList("success");
                this.root.RemoveFromClassList("warning");
                this.root.AddToClassList("failed");
                /*if (this.parentGroup != null) {
                    this.parentGroup.AddToClassList("flag-failed");
                }*/
            }
        }

        return allSuccess;
    }

}

public class MainScreen : MonoBehaviour {
    
    public UIDocument document;
    private StyleSheet styles;
    private System.Collections.Generic.List<Status> statusList = new System.Collections.Generic.List<Status>();
    private System.Collections.Generic.List<VisualElement> groups = new System.Collections.Generic.List<VisualElement>();

    private Label globalStatusLabel;
    private VisualElement globalStatus;
    private Config config;

    public void LoadStyle() {
        if (this.styles == null) this.styles = Resources.Load<StyleSheet>("Styles");
    }

    public void Awake() {
        
        if (System.IO.File.Exists($"{System.IO.Directory.GetCurrentDirectory()}/Config.json") == false) {
            Debug.LogError($"{System.IO.Directory.GetCurrentDirectory()}/Config.json file not found");
            return;
        }
        
        var json = System.IO.File.ReadAllText($"{System.IO.Directory.GetCurrentDirectory()}/Config.json");
        var config = JsonUtility.FromJson<Config>(json);
        Debug.Log(JsonUtility.ToJson(config));

        this.config = config;

        this.document.panelSettings.scale = config.uiScale;

    }
    
    public void OnEnable() {

        if (this.config.servers == null && this.config.groups == null) return;
        
        this.statusList.Clear();
        this.groups.Clear();
        
        this.LoadStyle();

        var doc = this.document;
        var root = doc.rootVisualElement.Q("Root");
        root.styleSheets.Add(this.styles);
        {
            var servers = new VisualElement();
            servers.AddToClassList("servers");
            root.Add(servers);
            {
                var globalStatus = new VisualElement();
                globalStatus.AddToClassList("global-status");
                this.globalStatus = globalStatus;
                var label = new Label();
                this.globalStatusLabel = label;
                label.AddToClassList("status");
                globalStatus.Add(label);
                servers.Add(globalStatus);
            }
            var scrollView = new ScrollView();
            servers.Add(scrollView);
            var list = new VisualElement();
            list.AddToClassList("list");
            scrollView.Add(list);
            var baseParams = new BaseGroupConfig(this.config);
            this.DrawServers(list, list, this.config.servers, baseParams);
            this.DrawGroups(list, this.config.groups, baseParams);
        }

    }

    private void DrawGroups(VisualElement root, Config.Group[] groups, BaseGroupConfig baseGroup) {
        if (groups == null) return;
        foreach (var group in groups) {
            var groupVisual = new Foldout();
            this.groups.Add(groupVisual);
            groupVisual.userData = root;
            groupVisual.text = group.caption;
            root.Add(groupVisual);
            groupVisual.AddToClassList("group");
            var inhConfig = new BaseGroupConfig(group, baseGroup);
            var list = new VisualElement();
            groupVisual.Add(list);
            list.AddToClassList("list");
            this.DrawServers(list, groupVisual, group.servers, inhConfig);
            this.DrawGroups(groupVisual, group.groups, inhConfig);
        }
    }

    private void DrawServers(VisualElement root, VisualElement parentGroup, ServerConfig[] servers, BaseGroupConfig baseGroup) {
        if (servers == null) return;
        foreach (var serverConfig in servers) {
            var item = new VisualElement();
            item.AddToClassList("item");
            root.Add(item);
            var inhConfig = new BaseGroupConfig(serverConfig, baseGroup);
            {
                var caption = new Label(serverConfig.host);
                caption.AddToClassList("caption");
                item.Add(caption);
                var container = new VisualElement();
                container.AddToClassList("container");
                item.Add(container);
                {
                    var description = new Label(serverConfig.description);
                    caption.AddToClassList("description");
                    container.Add(description);
                    var status = new Status(serverConfig, parentGroup, item, inhConfig);
                    this.statusList.Add(status);
                    status.Start();
                    container.Add(status);
                }
            }
        }
    }

    public struct Buffer {
        public VisualElement group;
        public bool warning;
        public bool success;
        public bool failed;
    }

    private System.Collections.Generic.List<Buffer> buffers = new System.Collections.Generic.List<Buffer>();
    public void Update() {
        
        if (this.config.servers == null && this.config.groups == null) return;

        var warnings = false;
        var allSuccess = true;
        var await = false;
        this.buffers.Clear();
        /*foreach (var group in this.groups) {
            var buffer = new Buffer() {
                group = group,
            };
            if (group.ClassListContains("flag-warning") == true) buffer.warning = true;
            if (group.ClassListContains("flag-success") == true) buffer.success = true;
            if (group.ClassListContains("flag-failed") == true) buffer.failed = true;
            this.buffers.Add(buffer);
            group.RemoveFromClassList("flag-warning");
            group.RemoveFromClassList("flag-success");
            group.RemoveFromClassList("flag-failed");
        }*/

        foreach (var status in this.statusList) {
            allSuccess &= status.Update(Time.deltaTime, out var w, out var a);
            warnings |= w;
            await |= a;
        }

        /*if (await == true) {
            foreach (var buffer in this.buffers) {
                if (buffer.success == true) buffer.group.AddToClassList("flag-success");
                if (buffer.warning == true) buffer.group.AddToClassList("flag-warning");
                if (buffer.failed == true) buffer.group.AddToClassList("flag-failed");
            }
        }*/

        foreach (var group in this.groups) {
            if (group.ClassListContains("flag-failed") == true) {
                group.RemoveFromClassList("flag-warning");
                group.RemoveFromClassList("flag-success");
            } else if (group.ClassListContains("flag-warning") == true) {
                group.RemoveFromClassList("flag-success");
            }
        }

        if (allSuccess == true && warnings == true) {
            this.globalStatus.RemoveFromClassList("success");
            this.globalStatus.RemoveFromClassList("failed");
            this.globalStatus.AddToClassList("warning");
            this.globalStatusLabel.text = "All is OK, but some attention required";
        } else if (allSuccess == true) {
            this.globalStatus.RemoveFromClassList("warning");
            this.globalStatus.RemoveFromClassList("failed");
            this.globalStatus.AddToClassList("success");
            this.globalStatusLabel.text = "All is OK";
        } else {
            this.globalStatus.RemoveFromClassList("warning");
            this.globalStatus.RemoveFromClassList("success");
            this.globalStatus.AddToClassList("failed");
            this.globalStatusLabel.text = "Some services not responding";
        }
    }

    public void OnDisable() {
        foreach (var status in this.statusList) {
            status.Dispose();
        }
    }

}
