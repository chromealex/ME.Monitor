namespace ME {

    public class PingMonoBehaviour : UnityEngine.MonoBehaviour {

        #if UNITY_WEBGL
        [System.Runtime.InteropServices.DllImportAttribute("__Internal")]
        private static extern void ping(string address);
        #endif

        public bool isDone;
        public int time;

        public void Call(ME.Monitoring.ServerConfig config) {
            this.time = -1;
            this.isDone = false;
            ping($"{config.protocolPrefix}://{config.host}?{System.DateTime.Now.Ticks}");
        }

        public void Receive(int result) {
            this.time = result;
            this.isDone = true;
        }

    }
    
    public class Ping {

        public bool isDone => this.ping.isDone;
        public int time => this.ping.time;

        #if UNITY_WEBGL && !UNITY_EDITOR
        private PingMonoBehaviour ping;
        #else
        private UnityEngine.Ping ping;
        #endif
        
        public Ping(string address, ME.Monitoring.ServerConfig config) {
            #if UNITY_WEBGL && !UNITY_EDITOR
            var addr = $"{config.protocolPrefix}://{config.host}?{System.DateTime.Now.Ticks}";
            this.ping = new UnityEngine.GameObject(addr, typeof(PingMonoBehaviour)).GetComponent<PingMonoBehaviour>();
            this.ping.Call(config);
            #else
            this.ping = new UnityEngine.Ping(address);
            #endif
        }

        public void DestroyPing() {
            #if UNITY_WEBGL && !UNITY_EDITOR
            UnityEngine.GameObject.DestroyImmediate(this.ping.gameObject);
            this.ping = null;
            #else
            this.ping.DestroyPing();
            #endif
        }

    }

}