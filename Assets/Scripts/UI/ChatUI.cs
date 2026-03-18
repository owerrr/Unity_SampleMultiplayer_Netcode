using UnityEngine;
using UnityEngine.UIElements;
using Unity.Entities;
using Unity.NetCode;
using Unity.Mathematics;
using Unity.Collections;

namespace SampleMultiplayer
{
    public class ChatUI : MonoBehaviour
    {
        public static ChatUI Instance;

        public static bool IsTyping { get; set; }

        private ScrollView _chatScroll;
        private VisualElement _chatMessages;
        private TextField _chatInput;
        private Button _sendButton;

        private static readonly float4 _ErrorColor = new float4(1f, 0.2f, 0.2f, 1f);
        private static readonly Color _ScrollBgInactive = new Color(0f, 0f, 0f, 0f);
        private static readonly Color _ScrollBgActive   = new Color(0f, 0f, 0f, 0.5f);
        
        private bool _sendingMessage;
        
        void Awake()
        {
            Instance = this;

            var root = GetComponent<UIDocument>().rootVisualElement;

            _chatScroll = root.Q<ScrollView>("ChatScroll");
            _chatMessages = root.Q<VisualElement>("ChatMessages");
            _chatInput = root.Q<TextField>("ChatInput");
            _sendButton = root.Q<Button>("SendChatButton");

            _sendButton.clicked += () => SendMessage();

            _chatInput.RegisterCallback<KeyUpEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return)
                    SendMessage();
            });

            _chatInput.RegisterCallback<PointerDownEvent>(_ =>
            {
                IsTyping = true;
                _chatScroll.style.backgroundColor = _ScrollBgActive;
            }, TrickleDown.TrickleDown);
            root.RegisterCallback<NavigationMoveEvent>(evt => evt.PreventDefault(), TrickleDown.TrickleDown);
            
            _chatInput.RegisterCallback<FocusOutEvent>(_ =>
            {
                IsTyping = false;
                _chatScroll.style.backgroundColor = _ScrollBgInactive;
            });
        }

        public void SendMessage()
        {
            EntityManager em = default;
            EntityQuery connectionQuery = default;
            World clientWorld = null;

            foreach (var world in World.All)
            {
                if (!world.IsClient()) continue;
                clientWorld = world;
                em = world.EntityManager;
                connectionQuery = em.CreateEntityQuery(typeof(NetworkStreamConnection));
                break;
            }

            if (clientWorld == null || connectionQuery.IsEmpty)
            {
                AddMessage(ChatErrorMessage.NotConnected, _ErrorColor);
                return;
            }

            var conn = connectionQuery.GetSingletonEntity();

            var text = _chatInput.value;
            if (string.IsNullOrWhiteSpace(text))
                return;

            var networkIdQuery = em.CreateEntityQuery(typeof(NetworkId));
            if (networkIdQuery.IsEmpty)
            {
                AddMessage(ChatErrorMessage.CannotGetPlayerId, _ErrorColor);
                return;
            }
            var localNetworkId = networkIdQuery.GetSingleton<NetworkId>().Value;

            int targetId = -1;
            float4 color = new float4(1, 1, 1, 1);
            
            if (text.StartsWith("/w "))
            {
                var parts = text.Split(' ', 3);

                if (parts.Length < 3 || !int.TryParse(parts[1], out int parsedId))
                {
                    AddMessage(ChatErrorMessage.InvalidSyntax, _ErrorColor);
                    _chatInput.value = "";
                    return;
                }

                if (parsedId < 0)
                {
                    AddMessage(ChatErrorMessage.InvalidPlayerId_LessThanZero, _ErrorColor);
                    _chatInput.value = "";
                    return;
                }

                targetId = parsedId;
                color = new float4(0.8f, 0f, 0.8f, 1f);
                text = parts[2];
            }
            
            var rpcEntity = em.CreateEntity();
            
            em.AddComponentData(rpcEntity, new ChatMessageRpc
            {
                Message    = new FixedString128Bytes(text),
                SenderId   = localNetworkId,
                SenderSlot = 0,
                TargetId   = targetId,
                Color      = color
            });
            em.AddComponentData(rpcEntity, new SendRpcCommandRequest
            {
                TargetConnection = conn
            });

            _chatInput.value = "";
            _chatInput.Blur();
        }

        public void AddMessage(string message, float4? color = null)
        {
            if (string.IsNullOrEmpty(message))
                return;

            var label = new Label(message);
            label.AddToClassList("chat-message");

            if (color.HasValue)
                label.style.color = new Color(color.Value.x, color.Value.y, color.Value.z, color.Value.w);

            _chatMessages.Add(label);
            _chatScroll.ScrollTo(label);

            if (_chatMessages.childCount > 50)
                _chatMessages.RemoveAt(0);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}