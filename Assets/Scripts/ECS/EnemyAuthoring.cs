using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;

namespace Charasiew.ECS
{
    public struct EnemyTag : IComponentData { }

    public struct EnemyAttackData : IComponentData
    {
        public float hitPoints;
        public float cooldownTime;
    }

    public struct EnemyCoolDownExpirationTimestamp : IComponentData, IEnableableComponent
    {
        public double value;
    }
    
    [RequireComponent(typeof(CharacterAuthoring))]
    public class EnemyAuthoring : MonoBehaviour
    {
        public float attackDamge;
        public float cooldownTime;
        
        private class Baker : Baker<EnemyAuthoring>
        {
            public override void Bake(EnemyAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new EnemyTag());
                AddComponent(entity, new EnemyAttackData { hitPoints = authoring.attackDamge, cooldownTime = authoring.cooldownTime });
                AddComponent(entity, new EnemyCoolDownExpirationTimestamp());
                SetComponentEnabled<EnemyCoolDownExpirationTimestamp>(entity, false);
            }
        }
    }
    
    [BurstCompile]
    [WithAll(typeof(EnemyTag))]         // 针对所有敌人标签
    public partial struct EnemyMoveToPlayerJob : IJobEntity
    {
        public float2 playerPosition;
        
        private void Execute(ref CharacterMoveDirection direction, ref LocalTransform transform)
        {
            // 修改移动速度
            var vectorToPlayer = playerPosition - transform.Position.xy;
            direction.value = math.normalize(vectorToPlayer);
        }
    }
    
    [BurstCompile]
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
            
            // 处理依赖：
            // 输入依赖（参数部分）：你的新Job会等待这些工作完成后才开始执行，从而避免访问尚未准备就绪的数据；
            // 输出依赖（赋值部分）：后续的任何操作如果需要依赖本系统的数据，必须等待我这个新Job完成；
            state.Dependency = moveToPlayerJob.ScheduleParallel(state.Dependency);          
        }
    }
    
    [UpdateInGroup(typeof(PhysicsSystemGroup))]
    [UpdateAfter(typeof(PhysicsSystemGroup))]           // 在物理组更新完后执行
    [UpdateBefore(typeof(AfterPhysicsSystemGroup))]
    public partial struct EnemyAttackSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimulationSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // 自游戏开始后所经过的时间
            double elapsedTime = SystemAPI.Time.ElapsedTime;           
            // 攻击CD处理
            foreach (var (expirationTimestamp, cooldownEnable) in SystemAPI.Query<EnemyCoolDownExpirationTimestamp, EnabledRefRW<EnemyCoolDownExpirationTimestamp>>())
            {
                if (expirationTimestamp.value > elapsedTime) 
                    continue;
                // CD到达后，关闭组件；
                cooldownEnable.ValueRW = false;
            }
            // 攻击job执行
            var attackJob = new EnemyAttackJob
            {
                // SystemAPI.GetComponentLookup
                // 提供一个高效、线程安全的“字典”或“查找表”，让你能够通过实体的 ID 随机读取或写入特定类型的组件数据；
                playerTagLookup = SystemAPI.GetComponentLookup<PlayerTag>(true),
                attackDataLoopup = SystemAPI.GetComponentLookup<EnemyAttackData>(true),
                cooldownLookup = SystemAPI.GetComponentLookup<EnemyCoolDownExpirationTimestamp>(),
                damageLookup = SystemAPI.GetBufferLookup<DamgeThisFrame>(),
                elapsedTime = elapsedTime,
            };
            var simulationSingleton = SystemAPI.GetSingleton<SimulationSingleton>();
            state.Dependency = attackJob.Schedule(simulationSingleton, state.Dependency);
        }
    }

    [BurstCompile]
    public struct EnemyAttackJob : ICollisionEventsJob
    {
        // 系统外只能用componentLookup来进行高效查询；
        [ReadOnly] public ComponentLookup<PlayerTag> playerTagLookup;
        [ReadOnly] public ComponentLookup<EnemyAttackData> attackDataLoopup;
        public ComponentLookup<EnemyCoolDownExpirationTimestamp> cooldownLookup;
        public BufferLookup<DamgeThisFrame> damageLookup;
        public double elapsedTime;
        
        public void Execute(CollisionEvent collisionEvent)
        {
            Entity playerEntity;
            Entity enemyEntity;

            // 快速区分玩家/敌人
            if (playerTagLookup.HasComponent(collisionEvent.EntityA) && attackDataLoopup.HasComponent(collisionEvent.EntityB))
            {
                playerEntity = collisionEvent.EntityA;
                enemyEntity = collisionEvent.EntityB;
            }
            else if (playerTagLookup.HasComponent(collisionEvent.EntityB) && attackDataLoopup.HasComponent(collisionEvent.EntityA))
            {
                playerEntity = collisionEvent.EntityB;
                enemyEntity = collisionEvent.EntityA;
            }
            else
            {
                return;
            }
            // 组件打开，代表处于CD中
            if (cooldownLookup.IsComponentEnabled(enemyEntity))
            {
                return;
            }
            EnemyAttackData attackData = attackDataLoopup[enemyEntity];
            // CD更新
            cooldownLookup[enemyEntity] = new EnemyCoolDownExpirationTimestamp
            {
                value = elapsedTime + attackData.cooldownTime           // 当前时间 + 冷却时间
            };
            cooldownLookup.SetComponentEnabled(enemyEntity, true);
            // 添加伤害
            var playerDamgeBuffer = damageLookup[playerEntity];
            playerDamgeBuffer.Add(new DamgeThisFrame
            {
                value = attackData.hitPoints
            });
        }
    }
}