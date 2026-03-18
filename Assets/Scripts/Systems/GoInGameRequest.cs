using UnityEngine;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Burst;

namespace SampleMultiplayer
{
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [CreateAfter(typeof(RpcSystem))]
    public partial struct SetRpcSystemDynamicAssemblyListSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            SystemAPI.GetSingletonRW<RpcCollection>().ValueRW.DynamicAssemblyList = true;
            state.Enabled = false;
        }
    }
    
    public struct GoInGameRequest : IRpcCommand { }
    
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    public partial struct GoInGameClientSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CubeSpawner>();
            var builder = new EntityQueryBuilder(Allocator.Temp).WithAll<NetworkId>().WithNone<NetworkStreamInGame>();
            state.RequireForUpdate(state.GetEntityQuery(builder));
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (id, entity) in SystemAPI.Query<RefRO<NetworkId>>().WithEntityAccess()
                         .WithNone<NetworkStreamInGame>())
            {
                commandBuffer.AddComponent<NetworkStreamInGame>(entity);
                var req = commandBuffer.CreateEntity();
                commandBuffer.AddComponent<GoInGameRequest>(req);
                commandBuffer.AddComponent(req, new SendRpcCommandRequest {TargetConnection = entity});
                Debug.Log($"[NEW RPC REQUEST] {entity.Index} - {id.ValueRO.Value}");
            }
            commandBuffer.Playback(state.EntityManager);
        }
    }
    
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial struct GoInGameServerSystem : ISystem 
    {
        private ComponentLookup<NetworkId> networkIdFromEntity;
        
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CubeSpawner>();
            var builder = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<GoInGameRequest>()
                .WithAll<ReceiveRpcCommandRequest>();
            state.RequireForUpdate(state.GetEntityQuery(builder));
            networkIdFromEntity = state.GetComponentLookup<NetworkId>(true);
        }
        
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var prefab = SystemAPI.GetSingleton<CubeSpawner>().Cube;
            state.EntityManager.GetName(prefab, out var prefabName);
            var worldName = state.WorldUnmanaged.Name;

            var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
            networkIdFromEntity.Update(ref state);
            
            var playerCount = 0;
            foreach (var _ in SystemAPI.Query<RefRO<NetworkId>>().WithAll<NetworkStreamInGame>())
                playerCount++;
            
            var usedSlots = new NativeList<int>(4, Allocator.Temp);
            foreach (var slot in SystemAPI.Query<RefRO<PlayerSlot>>())
                usedSlots.Add(slot.ValueRO.Value);

            foreach (var (reqSrc, reqEntity) in 
                     SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>>().WithAll<GoInGameRequest>().WithEntityAccess())
            {
                if (playerCount >= 4)
                {
                    Debug.Log($"[{worldName}] Server is full!");
                    commandBuffer.AddComponent<NetworkStreamRequestDisconnect>(reqSrc.ValueRO.SourceConnection);
                    commandBuffer.DestroyEntity(reqEntity);
                    continue;
                }
                
                int freeSlot = 1;
                for (int i = 1; i <= 4; i++)
                {
                    bool taken = false;
                    foreach (var s in usedSlots)
                        if (s == i) { taken = true; break; }
                    if (!taken) { freeSlot = i; break; }
                }
                usedSlots.Add(freeSlot);

                commandBuffer.AddComponent<NetworkStreamInGame>(reqSrc.ValueRO.SourceConnection);
                var networkId = networkIdFromEntity[reqSrc.ValueRO.SourceConnection];

                Debug.Log($"[{worldName}] {networkId.Value} joined as Player {freeSlot}, spawning {prefabName}");

                var player = commandBuffer.Instantiate(prefab);
                
                commandBuffer.SetComponent(player, new GhostOwner {NetworkId = networkId.Value});
                commandBuffer.AddComponent(player, new PlayerSlot {Value = freeSlot });
                commandBuffer.AppendToBuffer(reqSrc.ValueRO.SourceConnection, new LinkedEntityGroup {Value = player});
                commandBuffer.DestroyEntity(reqEntity);

                playerCount++;
            }

            usedSlots.Dispose();
            commandBuffer.Playback(state.EntityManager);
        }
    }
}