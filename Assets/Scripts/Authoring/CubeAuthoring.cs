using Unity.Entities;
using UnityEngine;
    
namespace SampleMultiplayer
{
    public struct Cube : IComponentData
    {
    }
    
    [DisallowMultipleComponent]
    public class CubeAuthoring : MonoBehaviour
    {
        class CubeBaker : Baker<CubeAuthoring>
        {
            public override void Bake(CubeAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<Cube>(entity);
                AddComponent(entity, new PlayerSlot { Value = 0 });
            }
        }
    }
}