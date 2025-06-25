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

[System.Serializable]
public struct ServerConfig {

    public string protocolPrefix;
    public string host;
    public string method;
    public int port;
    public string description;
    public Protocol[] protocols;
    
}

[System.Serializable]
public struct Config {

    public ServerConfig[] servers;
    public float refreshRate;
    public int warningPing;
    public float uiScale;

}

public class Status : VisualElement {

    private ServerConfig config;
    private VisualElement root;
    private Label[] labels;
    private Label[] msLabels;
    private object[] pings;
    private float rate;
    private float timer;
    private int warningPing;
    
    public Status(ServerConfig config, VisualElement root, float rate = 3f, int warningPing = 100) {
        this.warningPing = warningPing;
        this.root = root;
        this.timer = 0f;
        this.rate = rate;
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
    }

    public void Dispose() {
        for (var i = 0; i < this.labels.Length; ++i) {
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
    }

    private bool IsDone(int i) {
        var protocol = this.config.protocols[i];
        if (protocol == Protocol.IP) {
            if (this.pings[i] is Ping req) {
                return req.isDone == true;
            }
        } else if (protocol == Protocol.Tcp) {
            if (this.pings[i] is System.Net.Sockets.TcpClient req) {
                return req.Connected;
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
                return req.isDone == true && req.time >= 0 && req.time <= this.warningPing;
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
                return req.isDone == true && req.time > this.warningPing;
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
                return req.Client.Ttl.ToString();
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

    public bool Update(float dt, out bool warnings) {
        this.timer += dt;
        if (this.timer >= this.rate) {
            this.timer -= this.rate;
            this.Dispose();
            this.Start();
        }

        var await = false;
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
                await = true;
            }
        }

        if (await == false) {
            if (warnings == true && allSuccess == true) {
                this.root.RemoveFromClassList("success");
                this.root.RemoveFromClassList("failed");
                this.root.AddToClassList("warning");
            } else if (allSuccess == true) {
                this.root.RemoveFromClassList("warning");
                this.root.RemoveFromClassList("failed");
                this.root.AddToClassList("success");
            } else {
                this.root.RemoveFromClassList("success");
                this.root.RemoveFromClassList("warning");
                this.root.AddToClassList("failed");
            }
        }

        return allSuccess;
    }

}

public class MainScreen : MonoBehaviour {
    
    public UIDocument document;
    private StyleSheet styles;
    private System.Collections.Generic.List<Status> statusList = new System.Collections.Generic.List<Status>();

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

        if (this.config.servers == null) return;
        
        this.statusList.Clear();
        
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
            foreach (var serverConfig in this.config.servers) {
                var item = new VisualElement();
                item.AddToClassList("item");
                list.Add(item);
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
                        var status = new Status(serverConfig, item, this.config.refreshRate, this.config.warningPing);
                        this.statusList.Add(status);
                        status.Start();
                        container.Add(status);
                    }
                }
            }
        }

    }

    public void Update() {
        var warnings = false;
        var allSuccess = true;
        foreach (var status in this.statusList) {
            allSuccess &= status.Update(Time.deltaTime, out var w);
            warnings |= w;
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
