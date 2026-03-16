using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SampleMultiplayer
{
    public struct CubeInput : IInputComponentData
    {
        public int Horizontal;
        public int Vertical;
    }
    
    [DisallowMultipleComponent]
    public class CubeInputAuthoring : MonoBehaviour
    {
        class Baking : Baker<CubeInputAuthoring>
        {
            public override void Bake(CubeInputAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<CubeInput>(entity);
            }
        }
    }
    
    [UpdateInGroup(typeof(GhostInputSystemGroup))]
    public partial struct SampleCubeInput : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {

            var left  = Keyboard.current.aKey.isPressed;
            var right = Keyboard.current.dKey.isPressed;
            var down  = Keyboard.current.sKey.isPressed;
            var up    = Keyboard.current.wKey.isPressed;

            foreach (var playerInput in SystemAPI.Query<RefRW<CubeInput>>().WithAll<GhostOwnerIsLocal>())
            {
                playerInput.ValueRW = default;
                if (left)  playerInput.ValueRW.Horizontal -= 1;
                if (right) playerInput.ValueRW.Horizontal += 1;
                if (down)  playerInput.ValueRW.Vertical   -= 1;
                if (up)    playerInput.ValueRW.Vertical   += 1;
            }
        }
    }
    
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [BurstCompile]
    public partial struct CubeMovementSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var speed = SystemAPI.Time.DeltaTime * 4;
            foreach (var (input, trans) in
                     SystemAPI.Query<RefRO<CubeInput>, RefRW<LocalTransform>>()
                         .WithAll<Simulate>())
            {
                var moveInput = new float2(input.ValueRO.Horizontal, input.ValueRO.Vertical);
                moveInput = math.normalizesafe(moveInput) * speed;
                trans.ValueRW.Position += new float3(moveInput.x, 0, moveInput.y);
            }
        }
    }
}