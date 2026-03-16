using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;
using Unity.Collections;
using UnityEngine;
using TMPro;

namespace SampleMultiplayer
{
    [WorldSystemFilter(WorldSystemFilterFlags.Presentation)]
    public partial class NameplateSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var camera = Camera.main;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (ghostOwner, transform, entity) in
                     SystemAPI.Query<RefRO<GhostOwner>, RefRO<LocalTransform>>()
                         .WithNone<NameplateInitialized>()
                         .WithEntityAccess())
            {
                var go = new GameObject($"Nameplate_{ghostOwner.ValueRO.NetworkId}");
                go.transform.position = new Vector3(
                    transform.ValueRO.Position.x,
                    transform.ValueRO.Position.y + 1.5f,
                    transform.ValueRO.Position.z);
                go.transform.localScale = Vector3.one * 0.01f;

                var textGo = new GameObject("Text");
                textGo.transform.SetParent(go.transform, false);

                var tmp = textGo.AddComponent<TextMeshPro>();
                tmp.text = $"Player {ghostOwner.ValueRO.NetworkId}";
                tmp.fontSize = 200;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = Color.white;

                var rect = textGo.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(2000, 400);
                rect.anchoredPosition = Vector2.zero;

                var billboard = go.AddComponent<BillboardFollow>();
                billboard.Init(EntityManager, entity, camera);

                ecb.AddComponent<NameplateInitialized>(entity);
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }

    public class BillboardFollow : MonoBehaviour
    {
        private EntityManager _em;
        private Entity _entity;
        private Camera _camera;

        public void Init(EntityManager em, Entity entity, Camera camera)
        {
            _em = em;
            _entity = entity;
            _camera = camera;
        }

        private void LateUpdate()
        {
            
            if (!_em.Exists(_entity))
            {
                Destroy(gameObject);
                return;
            }

            var pos = _em.GetComponentData<LocalTransform>(_entity).Position;
            transform.position = new Vector3(pos.x, pos.y + 1.5f, pos.z);

            if (_camera != null)
                transform.forward = _camera.transform.forward;
        }
    }

    public struct NameplateInitialized : IComponentData { }
}