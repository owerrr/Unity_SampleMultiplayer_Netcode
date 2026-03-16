using Unity.Entities;
using Unity.NetCode;
using Unity.Networking.Transport;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
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
        public GameObject GameUI;

        private UIDocument _uiDocument;
        private Button _startHost, _startClient;

        private void Awake()
        {
            if (!FindAnyObjectByType<EventSystem>())
            {
                var inputType = typeof(InputSystemUIInputModule);
                var eventSystem = new GameObject("EventSystem", typeof(EventSystem), inputType);
                eventSystem.transform.SetParent(transform);
            }
        }

        private void OnEnable()
        {
            _uiDocument  = GetComponent<UIDocument>();
            _startHost   = _uiDocument.rootVisualElement.Q<Button>("StartHostButton");
            _startClient = _uiDocument.rootVisualElement.Q<Button>("StartClientButton");
            _startHost.clicked   += StartClientServer;
            _startClient.clicked += StartClient;
        }

        private void OnDisable()
        {
            if (_startHost   != null) _startHost.clicked   -= StartClientServer;
            if (_startClient != null) _startClient.clicked -= StartClient;
        }

        private void StartClientServer()
        {
            if (ClientServerBootstrap.RequestedPlayType != ClientServerBootstrap.PlayType.ClientAndServer)
            {
                Debug.LogError($"PlayType is {ClientServerBootstrap.RequestedPlayType}, expected ClientAndServer.");
                return;
            }

            Application.runInBackground = true;
            DisableButtons();
            
            var server = ClientServerBootstrap.CreateServerWorld("ServerWorld");
            var client = ClientServerBootstrap.CreateClientWorld("ClientWorld");

            World.DefaultGameObjectInjectionWorld = server;

            LoadGameplayScene();

            var serverEp = NetworkEndpoint.AnyIpv4.WithPort(NetworkPort);
            using (var q = server.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<NetworkStreamDriver>()))
                q.GetSingletonRW<NetworkStreamDriver>().ValueRW.Listen(serverEp);

            var clientEp = NetworkEndpoint.LoopbackIpv4.WithPort(NetworkPort);
            using (var q = client.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<NetworkStreamDriver>()))
                q.GetSingletonRW<NetworkStreamDriver>().ValueRW.Connect(client.EntityManager, clientEp);

            AddConnectionUISystemToUpdateList();

            gameObject.SetActive(false);
            ChatUI.SetActive(true);
            GameUI.SetActive(true);
        }

        private void StartClient()
        {
            Application.runInBackground = true;
            DisableButtons();

            var client = ClientServerBootstrap.CreateClientWorld("ClientWorld");

            World.DefaultGameObjectInjectionWorld = client;

            LoadGameplayScene();

            var ep = NetworkEndpoint.Parse(Address, NetworkPort);
            using (var q = client.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<NetworkStreamDriver>()))
                q.GetSingletonRW<NetworkStreamDriver>().ValueRW.Connect(client.EntityManager, ep);

            AddConnectionUISystemToUpdateList();

            gameObject.SetActive(false);
            ChatUI.SetActive(true);
            GameUI.SetActive(true);
        }

        private void LoadGameplayScene()
        {
            if (!SceneManager.GetSceneByName(SceneToLoad).IsValid())
                SceneManager.LoadSceneAsync(SceneToLoad, LoadSceneMode.Additive);
        }

        private void AddConnectionUISystemToUpdateList()
        {
            foreach (var world in World.All)
            {
                if (world.IsClient() && !world.IsThinClient())
                {
                    var sys = world.GetOrCreateSystemManaged<ConnectionUISystem>();
                    sys.UIBehaviour = this;
                    world.GetExistingSystemManaged<SimulationSystemGroup>().AddSystemToUpdateList(sys);
                }
            }
        }

        private void DisableButtons()
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
        protected override void OnUpdate() { }
    }
}