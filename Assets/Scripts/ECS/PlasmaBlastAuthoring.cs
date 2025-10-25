using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;
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
                AddComponent<DestroyEntityFlag>(entity);
                SetComponentEnabled<DestroyEntityFlag>(entity, false);
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
    
    [UpdateInGroup(typeof(PhysicsSystemGroup))]
    [UpdateAfter(typeof(PhysicsSimulationGroup))]
    [UpdateBefore(typeof(AfterPhysicsSystemGroup))]
    public partial struct PlasmaBlastAttackSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimulationSingleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var attackJob = new PlasmaBlastAttackJob()
            {
                plasmaBlastLookup = SystemAPI.GetComponentLookup<PlasmaBlastData>(true),
                enemyLookup = SystemAPI.GetComponentLookup<EnemyTag>(true),
                damgeThisFrameLookup = SystemAPI.GetBufferLookup<DamgeThisFrame>(),
                destroyEntityLookup = SystemAPI.GetComponentLookup<DestroyEntityFlag>(),
            };

            // 因为量少，所以用.Schedule串行处理
            var simulationSingleton = SystemAPI.GetSingleton<SimulationSingleton>();
            state.Dependency = attackJob.Schedule(simulationSingleton, state.Dependency);
        }
    }

    public struct PlasmaBlastAttackJob : ITriggerEventsJob
    {
        [ReadOnly] public ComponentLookup<PlasmaBlastData> plasmaBlastLookup;
        [ReadOnly] public ComponentLookup<EnemyTag> enemyLookup;
        public BufferLookup<DamgeThisFrame> damgeThisFrameLookup;
        public ComponentLookup<DestroyEntityFlag> destroyEntityLookup;
        
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
            {
                return;
            }

            var attackDamge = plasmaBlastLookup[plasmaBlastEntity].attackDamge;
            var enemyDamgeBuffer = damgeThisFrameLookup[enemyEntity];
            enemyDamgeBuffer.Add(new DamgeThisFrame { value = attackDamge });
            
            destroyEntityLookup.SetComponentEnabled(plasmaBlastEntity, true);
        }
    }
}