using Unity.Collections;
using Unity.Mathematics;
using Unity.NetCode;

namespace SampleMultiplayer
{
    public struct ChatMessageRpc : IRpcCommand
    {
        public FixedString128Bytes Message;
        public int SenderId;
        public int TargetId;
        public float4 Color;
    }
}