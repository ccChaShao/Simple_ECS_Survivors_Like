using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

namespace Charasiew.ECS
{
    public struct PlasmaBlastData : IComponentData
    {
        public float moveSpeed;
        public int attackDamge;
    }
    public class PlasmaBlastAuthoring : MonoBehaviour
    {
        public float moveSpeed;
        public int attackDamge;
        
        private class Baker : Baker<PlasmaBlastAuthoring>
        {
            public override void Bake(PlasmaBlastAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new PlasmaBlastData
                {
                    moveSpeed = authoring.moveSpeed,
                    attackDamge = authoring.attackDamge,
                });
            }
        }
    }

    public partial struct PlasmaBlastMoveSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            foreach (var (transform, plasmaBlastData) in SystemAPI.Query<RefRW<LocalTransform>, RefRO<PlasmaBlastData>>())
            {
                // 右侧为正方形，应该创建的时候算了以右侧为正方向的欧拉角；
                transform.ValueRW.Position += transform.ValueRO.Right() * plasmaBlastData.ValueRO.moveSpeed * deltaTime;
            }
        }
    }
    
    public partial struct PlasmaBlastAttackSystem : ISystem
    {
        
    }

    public struct PlasmaBlastAttackJob : ITriggerEventsJob
    {
        [ReadOnly] public ComponentLookup<PlasmaBlastData> plasmaBlastLookup;
        [ReadOnly] public ComponentLookup<EnemyTag> enemyLookup;
        public BufferLookup<DamgeThisFrame> damgeThisFrameLookup;
        
        public void Execute(TriggerEvent triggerEvent)
        {
            Entity plasmaBlastEntity;
            Entity enemyEntity;

            if (plasmaBlastLookup.HasComponent(triggerEvent.EntityA) && enemyLookup.HasComponent(triggerEvent.EntityB))
            {
                plasmaBlastEntity = triggerEvent.EntityA;
                enemyEntity = triggerEvent.EntityB;
            }
            else if (plasmaBlastLookup.HasComponent(triggerEvent.EntityB) && enemyLookup.HasComponent(triggerEvent.EntityA))
            {
                plasmaBlastEntity = triggerEvent.EntityB;
                enemyEntity = triggerEvent.EntityA;
            }
            else
                return;
        }
    }
}