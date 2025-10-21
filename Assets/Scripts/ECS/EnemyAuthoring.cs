using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Charasiew.ECS
{
    public struct EnemyTag : IComponentData { }
    
    [RequireComponent(typeof(CharacterAuthoring))]
    public class EnemyAuthoring : MonoBehaviour
    {
        private class Baker : Baker<EnemyAuthoring>
        {
            public override void Bake(EnemyAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<EnemyTag>(entity);
            }
        }
    }
    
    public partial struct EnemyMoveToPlayerSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            // 先确保有玩家标签
            state.RequireForUpdate<PlayerTag>();    
        }

        public void OnUpdate(ref SystemState state)
        {
            // 找到具有玩家标签的单例实体（假如有多个玩家tag的entity，则不会返回这个单例entity）
            var playerEntity = SystemAPI.GetSingletonEntity<PlayerTag>();           
            var playerPosition = SystemAPI.GetComponent<LocalTransform>(playerEntity).Position.xy;
            
            // JOB创建
            var moveToPlayerJob = new EnemyMoveToPlayerJob
            {
                playerPosition = playerPosition
            };
            
            // 多线程处理
            state.Dependency = moveToPlayerJob.ScheduleParallel(state.Dependency);          
        }
    }
    
    [BurstCompile]
    [WithAll(typeof(EnemyTag))]         // 针对所有敌人标签
    public partial struct EnemyMoveToPlayerJob : IJobEntity
    {
        public float2 playerPosition;
        
        private void Execute(ref CharacterMoveDirection direction, ref LocalTransform transform)
        {
            var vectorToPlayer = playerPosition - transform.Position.xy;
            direction.value = math.normalize(vectorToPlayer);
        }
    }
}