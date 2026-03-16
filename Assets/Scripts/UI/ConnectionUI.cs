using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Button = UnityEngine.UIElements.Button;
using UnityEngine.InputSystem.UI;

namespace SampleMultiplayer
{
    public class ConnectionUI : MonoBehaviour
    {
        public ushort NetworkPort = 7979;
        public string Address = "127.0.0.1";
        public string SceneToLoad;
        
        public GameObject ChatUI;

        internal static string OldFrontendWorldName = string.Empty;

        public void OnBeforeConnect()
        {
            Application.runInBackground = true;
        }
        
        public void OnConnected()
        {
            DestroyLocalSimulationWorld();
            
            var scene = SceneManager.GetSceneByName(SceneToLoad);
            if (scene.IsValid())
                return;
            
            SceneManager.LoadSceneAsync(SceneToLoad, LoadSceneMode.Additive);
        }
        
        private UIDocument _uiDocument;
        private Button _startHost, _startClient;
        
        void Awake()
        {
            _uiDocument = GetComponent<UIDocument>();
            
            _startHost = _uiDocument.rootVisualElement.Q<Button>("StartHostButton");
            _startClient = _uiDocument.rootVisualElement.Q<Button>("StartClientButton");
            _startHost.clicked += StartClientServer;
            _startClient.clicked += StartClient;
            
            if (!FindAnyObjectByType<EventSystem>())
            {

                var inputType = typeof(InputSystemUIInputModule);
                var eventSystem = new GameObject("EventSystem", typeof(EventSystem), inputType);
                eventSystem.transform.SetParent(transform);
            }
        }

        void AddConnectionUISystemToUpdateList()
        {
            foreach (var world in World.All)
            {
                if (world.IsClient() && !world.IsThinClient())
                {
                    var sys = world.GetOrCreateSystemManaged<ConnectionUISystem>();
                    sys.UIBehaviour = this;
                    var simGroup = world.GetExistingSystemManaged<SimulationSystemGroup>();
                    simGroup.AddSystemToUpdateList(sys);
                }
            }
        }
        
        void StartClientServer()
        {
            if (ClientServerBootstrap.RequestedPlayType != ClientServerBootstrap.PlayType.ClientAndServer)
            {
                Debug.LogError($"Creating client/server worlds is not allowed if playmode is set to {ClientServerBootstrap.RequestedPlayType}");
                return;
            }

            OnBeforeConnect();
            DisableButtons();
           
            var server = ClientServerBootstrap.CreateServerWorld("ServerWorld");
            var client = ClientServerBootstrap.CreateClientWorld("ClientWorld");
            
            if (World.DefaultGameObjectInjectionWorld == null)
                World.DefaultGameObjectInjectionWorld = server;

            OnConnected();
            
            NetworkEndpoint ep = NetworkEndpoint.AnyIpv4.WithPort(NetworkPort);
            {
                using var drvQuery = server.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<NetworkStreamDriver>());
                drvQuery.GetSingletonRW<NetworkStreamDriver>().ValueRW.Listen(ep);
            }

            ep = NetworkEndpoint.LoopbackIpv4.WithPort(NetworkPort);
            {
                using var drvQuery = client.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<NetworkStreamDriver>());
                drvQuery.GetSingletonRW<NetworkStreamDriver>().ValueRW.Connect(client.EntityManager, ep);
            }
            AddConnectionUISystemToUpdateList();

            gameObject.SetActive(false);
            ChatUI.SetActive(true);
        }
        
        void StartClient()
        {
            OnBeforeConnect();
            DisableButtons(); 
            var client = ClientServerBootstrap.CreateClientWorld("ClientWorld");
            
            if (World.DefaultGameObjectInjectionWorld == null)
                World.DefaultGameObjectInjectionWorld = client;
            
            OnConnected();

            var ep = NetworkEndpoint.Parse(Address, NetworkPort);
            {
                using var drvQuery = client.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<NetworkStreamDriver>());
                drvQuery.GetSingletonRW<NetworkStreamDriver>().ValueRW.Connect(client.EntityManager, ep);
            }
            AddConnectionUISystemToUpdateList();
            
            gameObject.SetActive(false);
            ChatUI.SetActive(true);
        }

        static void DestroyLocalSimulationWorld()
        {
            foreach (var world in World.All)
            {
                if (world.Flags == WorldFlags.Game)
                {
                    OldFrontendWorldName = world.Name;
                    world.Dispose();
                    break;
                }
            }
        }

        void DisableButtons()
        {
            _startHost.SetEnabled(false);
            _startClient.SetEnabled(false);
        }
    }
    
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    [DisableAutoCreation]
    public partial class ConnectionUISystem : SystemBase
    {
        public ConnectionUI UIBehaviour;
        string m_PingText;
        
        protected override void OnUpdate()
        {
            CompleteDependency();
            if (!SystemAPI.TryGetSingletonEntity<NetworkStreamConnection>(out var connectionEntity))
            {
                m_PingText = default;
                return;
            }

            var connection = EntityManager.GetComponentData<NetworkStreamConnection>(connectionEntity);
            var address = SystemAPI.GetSingletonRW<NetworkStreamDriver>().ValueRO.GetRemoteEndPoint(connection).Address;
            if (EntityManager.HasComponent<NetworkId>(connectionEntity))
            {
                if (string.IsNullOrEmpty(m_PingText) || UnityEngine.Time.frameCount % 30 == 0)
                {
                    var networkSnapshotAck = EntityManager.GetComponentData<NetworkSnapshotAck>(connectionEntity);
                    m_PingText = networkSnapshotAck.EstimatedRTT > 0 ? $"{(int) networkSnapshotAck.EstimatedRTT}ms" : "Connected";
                }
            }
        }
    }
}