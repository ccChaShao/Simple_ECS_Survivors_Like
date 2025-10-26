using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using UnityEngine;

namespace Charasiew.ECS
{
    public struct GemTag : IComponentData { }
    
    public class GemAuthoring : MonoBehaviour
    {
        private class Baker : Baker<GemAuthoring>
        {
            public override void Bake(GemAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<GemTag>(entity);
                AddComponent<DestroyEntityFlag>(entity);
                SetComponentEnabled<DestroyEntityFlag>(entity, false);
            }
        }
    }
    
    public partial struct CollectGemSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimulationSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var newCollectJob = new CollectGemJob
            {
                gemTagLookup = SystemAPI.GetComponentLookup<GemTag>(true),
                gemsCollectedLookup = SystemAPI.GetComponentLookup<GemsCollectedCount>(),
                destroyEntityLookup = SystemAPI.GetComponentLookup<DestroyEntityFlag>(),
                updateGemUILookup = SystemAPI.GetComponentLookup<UpdateGemUIFlag>(),
            };

            var simulationSingleton = SystemAPI.GetSingleton<SimulationSingleton>();
            state.Dependency = newCollectJob.Schedule(simulationSingleton, state.Dependency);
        }
    }

    [BurstCompile]
    public struct CollectGemJob : ITriggerEventsJob
    {
        [ReadOnly] public ComponentLookup<GemTag> gemTagLookup;
        public ComponentLookup<GemsCollectedCount> gemsCollectedLookup;
        public ComponentLookup<DestroyEntityFlag> destroyEntityLookup;
        public ComponentLookup<UpdateGemUIFlag> updateGemUILookup;
        
        public void Execute(TriggerEvent triggerEvent)
        {
            Entity gemEntity;
            Entity playerEntity;
            if (gemTagLookup.HasComponent(triggerEvent.EntityA) && gemsCollectedLookup.HasComponent(triggerEvent.EntityB))
            {
                gemEntity = triggerEvent.EntityA;
                playerEntity = triggerEvent.EntityB;
            }
            else if (gemTagLookup.HasComponent(triggerEvent.EntityB) && gemsCollectedLookup.HasComponent(triggerEvent.EntityA))
            {
                gemEntity = triggerEvent.EntityB;
                playerEntity = triggerEvent.EntityA;
            }
            else
            {
                return;
            }
            
            // 这里实际上没有改变组件的数据（结构体）；
            var gemsCollected = gemsCollectedLookup[playerEntity];
            gemsCollected.value += 1;
            // 这里才会改变，因为重新赋值了；
            gemsCollectedLookup[playerEntity] = gemsCollected;
            updateGemUILookup.SetComponentEnabled(playerEntity, true);
            // 打开，让system查到；
            destroyEntityLookup.SetComponentEnabled(gemEntity, true);
        }
    }
}