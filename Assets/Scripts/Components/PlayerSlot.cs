using Unity.Entities;
using Unity.NetCode;

namespace SampleMultiplayer
{
    [GhostComponent(PrefabType = GhostPrefabType.All)]
    public struct PlayerSlot : IComponentData
    {
        [GhostField]
        public int Value;
    }
}