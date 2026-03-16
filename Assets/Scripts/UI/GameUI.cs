using Unity.Entities;
using Unity.NetCode;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;

namespace SampleMultiplayer
{
    public class GameUI : MonoBehaviour
    {
        private Button _disconnectButton;
        private bool _wasConnected = false;
        private float _connectionTimer = 0f;
        private const float ConnectionTimeout = 3f;

        private void Awake()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            _disconnectButton = root.Q<Button>("DisconnectButton");
            _disconnectButton.clicked += Disconnect;
        }

        private void OnEnable()
        {
            _wasConnected = false;
            _connectionTimer = 0f;
        }

        private void Update()
        {
            foreach (var world in World.All)
            {
                if (world.Name != "ClientWorld") continue;

                var connQ = world.EntityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<NetworkStreamConnection>(),
                    ComponentType.ReadOnly<NetworkId>());

                if (!_wasConnected && !connQ.IsEmpty)
                    _wasConnected = true;

                if (_wasConnected && connQ.IsEmpty)
                {
                    connQ.Dispose();
                    Disconnect();
                    return;
                }

                connQ.Dispose();
            }
            
            if (!_wasConnected)
            {
                _connectionTimer += Time.deltaTime;
                if (_connectionTimer >= ConnectionTimeout)
                {
                    Debug.Log("[TIMEOUT] Couldn't find server. Returning to lobby");
                    Disconnect();
                }
            }
        }

        private void Disconnect()
        {
            foreach (var nameplate in FindObjectsByType<BillboardFollow>(FindObjectsSortMode.None))
                Destroy(nameplate.gameObject);

            var toDispose = new System.Collections.Generic.List<World>();
            foreach (var world in World.All)
                if (world.Name == "ClientWorld" || world.Name == "ServerWorld")
                    toDispose.Add(world);

            foreach (var world in toDispose)
                world.Dispose();

            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}