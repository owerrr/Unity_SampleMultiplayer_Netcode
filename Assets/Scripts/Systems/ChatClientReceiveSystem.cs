using Unity.Entities;
using Unity.NetCode;
using Unity.Collections;
using UnityEngine;

namespace SampleMultiplayer
{
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial struct ChatClientReceiveSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ChatMessageRpc>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (chat, req, entity) in
                     SystemAPI.Query<RefRO<ChatMessageRpc>, RefRO<ReceiveRpcCommandRequest>>()
                         .WithEntityAccess())
            {
                var senderId = chat.ValueRO.SenderId;
                var targetId = chat.ValueRO.TargetId;
                var col      = chat.ValueRO.Color;

                string msg;
                
                if (senderId == -1 && targetId == -2)
                {
                    msg = chat.ValueRO.Message.ToString();
                }
                else if (targetId == -1)
                {
                    msg = $"Player {chat.ValueRO.SenderSlot}: {chat.ValueRO.Message}";
                }
                else
                {
                    msg = $"[PM] Player {chat.ValueRO.SenderSlot} > Player {targetId}: {chat.ValueRO.Message}";
                }

                Debug.Log(msg);
                ChatUI.Instance?.AddMessage(msg, col);

                ecb.DestroyEntity(entity);
            }
            ecb.Playback(state.EntityManager);
        }
    }
}