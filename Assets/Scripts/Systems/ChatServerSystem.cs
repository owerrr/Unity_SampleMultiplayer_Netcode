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
            
            var slotToNetworkId = new NativeHashMap<int, int>(4, Allocator.Temp);
            var networkIdToSlot = new NativeHashMap<int, int>(4, Allocator.Temp);

            foreach (var (slot, ghostOwner) in
                     SystemAPI.Query<RefRO<PlayerSlot>, RefRO<GhostOwner>>())
            {
                slotToNetworkId.TryAdd(slot.ValueRO.Value, ghostOwner.ValueRO.NetworkId);
                networkIdToSlot.TryAdd(ghostOwner.ValueRO.NetworkId, slot.ValueRO.Value);
            }

            foreach (var (chat, req, entity) in
                     SystemAPI.Query<RefRO<ChatMessageRpc>, RefRO<ReceiveRpcCommandRequest>>()
                         .WithEntityAccess())
            {
                var senderConn = req.ValueRO.SourceConnection;
                var senderId = networkIdFromEntity[senderConn].Value;
                var targetSlot = chat.ValueRO.TargetId;
                
                networkIdToSlot.TryGetValue(senderId, out int senderSlot);
                
                if (targetSlot != -1 && targetSlot is <= 0 or > 4)
                {
                    SendErrorToSender(ref ecb, ChatErrorMessage.InvalidTarget, senderConn);
                    ecb.DestroyEntity(entity);
                    continue;
                }
                
                if (targetSlot != -1 && targetSlot == senderSlot)
                {
                    SendErrorToSender(ref ecb, ChatErrorMessage.SelfMessage, senderConn);
                    ecb.DestroyEntity(entity);
                    continue;
                }
                
                if (targetSlot != -1 && !slotToNetworkId.ContainsKey(targetSlot))
                {
                    SendErrorToSender(ref ecb, ChatErrorMessage.PlayerNotFound(targetSlot), senderConn);
                    ecb.DestroyEntity(entity);
                    continue;
                }
                
                int targetNetworkId = -1;
                if (targetSlot != -1)
                    slotToNetworkId.TryGetValue(targetSlot, out targetNetworkId);
                
                foreach (var (id, connEntity) in
                         SystemAPI.Query<RefRO<NetworkId>>()
                             .WithAll<NetworkStreamInGame>()
                             .WithEntityAccess())
                {
                    bool isPublic = targetSlot == -1;
                    if (!isPublic && id.ValueRO.Value != targetNetworkId && id.ValueRO.Value != senderId)
                        continue;

                    var rpc = ecb.CreateEntity();
                    ecb.AddComponent(rpc, new ChatMessageRpc
                    {
                        Message    = chat.ValueRO.Message,
                        SenderId   = senderId,
                        SenderSlot = senderSlot,
                        TargetId   = targetSlot,
                        Color      = chat.ValueRO.Color
                    });
                    ecb.AddComponent(rpc, new SendRpcCommandRequest
                    {
                        TargetConnection = connEntity
                    });
                }

                ecb.DestroyEntity(entity);
            }

            slotToNetworkId.Dispose();
            networkIdToSlot.Dispose();
            ecb.Playback(state.EntityManager);
        }

        private static void SendErrorToSender(ref EntityCommandBuffer ecb, string errorMsg, Entity senderConn)
        {
            var errorRpc = ecb.CreateEntity();
            ecb.AddComponent(errorRpc, new ChatMessageRpc
            {
                Message = new FixedString128Bytes(errorMsg),
                SenderId = -1,
                SenderSlot = -1,
                TargetId = -2,
                Color = ErrorColor
            });
            ecb.AddComponent(errorRpc, new SendRpcCommandRequest
            {
                TargetConnection = senderConn
            });
        }
    }
}