using Unity.Entities;
using Unity.NetCode;
using Unity.Collections;
using Unity.Mathematics;

namespace SampleMultiplayer
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct ChatServerSystem : ISystem
    {
        private static readonly float4 ErrorColor = new float4(1f, 0.2f, 0.2f, 1f);

        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var networkIdFromEntity = state.GetComponentLookup<NetworkId>(true);
            networkIdFromEntity.Update(ref state);

            foreach (var (chat, req, entity) in
                     SystemAPI.Query<RefRO<ChatMessageRpc>, RefRO<ReceiveRpcCommandRequest>>()
                         .WithEntityAccess())
            {
                var senderConn = req.ValueRO.SourceConnection;
                var senderId = networkIdFromEntity[senderConn].Value;
                var targetId = chat.ValueRO.TargetId;
                
                if (targetId != -1 && targetId is <= 0 or > 4)
                {
                    SendErrorToSender(ref ecb, "[Błąd] Nieprawidłowy ID odbiorcy.", senderConn);
                    ecb.DestroyEntity(entity);
                    continue;
                }
                
                if (targetId == senderId)
                {
                    SendErrorToSender(ref ecb, "[Błąd] Nie możesz wysłać prywatnej wiadomości do samego siebie.", senderConn);
                    ecb.DestroyEntity(entity);
                    continue;
                }
                
                if (targetId != -1)
                {
                    bool targetExists = false;
                    foreach (var (id, _) in
                             SystemAPI.Query<RefRO<NetworkId>>()
                                 .WithAll<NetworkStreamInGame>()
                                 .WithEntityAccess())
                    {
                        if (id.ValueRO.Value == targetId)
                        {
                            targetExists = true;
                            break;
                        }
                    }

                    if (!targetExists)
                    {
                        SendErrorToSender(ref ecb, $"[Błąd] Gracz o ID {targetId} nie istnieje lub nie jest połączony.", senderConn);
                        ecb.DestroyEntity(entity);
                        continue;
                    }
                }
                
                foreach (var (id, connEntity) in
                         SystemAPI.Query<RefRO<NetworkId>>()
                             .WithAll<NetworkStreamInGame>()
                             .WithEntityAccess())
                {
                    bool isPublic = targetId == -1;
                    if (!isPublic && id.ValueRO.Value != targetId && id.ValueRO.Value != senderId)
                        continue;

                    var rpc = ecb.CreateEntity();
                    ecb.AddComponent(rpc, new ChatMessageRpc
                    {
                        Message = chat.ValueRO.Message,
                        SenderId = senderId,
                        TargetId = targetId,
                        Color = chat.ValueRO.Color
                    });
                    ecb.AddComponent(rpc, new SendRpcCommandRequest
                    {
                        TargetConnection = connEntity
                    });
                }

                ecb.DestroyEntity(entity);
            }

            ecb.Playback(state.EntityManager);
        }
        
        private static void SendErrorToSender(ref EntityCommandBuffer ecb, string errorMsg, Entity senderConn)
        {
            var errorRpc = ecb.CreateEntity();
            ecb.AddComponent(errorRpc, new ChatMessageRpc
            {
                Message = new FixedString128Bytes(errorMsg),
                SenderId = -1,
                TargetId = -2,
                Color = new float4(1f, 0.2f, 0.2f, 1f)
            });
            ecb.AddComponent(errorRpc, new SendRpcCommandRequest
            {
                TargetConnection = senderConn
            });
        }
    }
}