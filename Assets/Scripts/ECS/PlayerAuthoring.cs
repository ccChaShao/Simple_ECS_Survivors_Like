using System.Collections;
using System.Collections.Generic;
using TMG.Survivors;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

namespace Charasiew.ECS
{
    public enum PlayerAnimationIndex : byte
    {
        Movement = 0,
        Idle = 1,
        None = byte.MaxValue,
    }
    
    public struct PlayerTag : IComponentData { }
    
    public struct InititalizeCameraTargetTag : IComponentData { }

    public struct CameraTarget : IComponentData
    {
        public UnityObjectRef<Transform> cameraTransform;           // ecs引用unity对象的类型
    }

    [MaterialProperty("_AnimationIndex")]
    public struct AnimationIndexOverride : IComponentData
    {
        public float value;
    }

    public struct PlayerAttackData : IComponentData
    {
        public Entity AttackPrefab;
        public float coolDownTime;
        public float3 detectionSize;            // 检测范围
        public CollisionFilter collisionFilter;
    }

    public struct PlayerCooldownExpirationTimestamp : IComponentData
    {
        public double value;
    }

    public struct GemsCollectedCount : IComponentData
    {
        public int value;
    }
    
    public struct UpdateGemUIFlag : IComponentData, IEnableableComponent { }
    
    [RequireComponent(typeof(CharacterAuthoring))]
    public class PlayerAuthoring : MonoBehaviour
    {
        [Header("攻击数据")]
        public GameObject attackPrefab;
        public float cooldownTime;
        public float detectionSize;
        
        private class Baker: Baker<PlayerAuthoring>
        {
            public override void Bake(PlayerAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<PlayerTag>(entity);
                AddComponent<InititalizeCameraTargetTag>(entity);
                AddComponent<CameraTarget>(entity);
                AddComponent<AnimationIndexOverride>(entity);

                var enemyLayer = LayerMask.NameToLayer("Enemy");    // 返回的是index序号；
                var enemyLayerMask = (uint)math.pow(2, enemyLayer);     // 返回的是十进制后的掩码（需要执行2的序号次方）；
                var attackCollisionFilter = new CollisionFilter
                {
                    BelongsTo = uint.MaxValue,
                    CollidesWith = enemyLayerMask
                };
                AddComponent(entity, new PlayerAttackData
                {
                    AttackPrefab = GetEntity(authoring.attackPrefab, TransformUsageFlags.Dynamic),
                    coolDownTime = authoring.cooldownTime,
                    detectionSize = new float3(authoring.detectionSize, authoring.detectionSize, authoring.detectionSize),
                    collisionFilter = attackCollisionFilter,
                });
                AddComponent<PlayerCooldownExpirationTimestamp>(entity);
                AddComponent<GemsCollectedCount>(entity);
                AddComponent<UpdateGemUIFlag>(entity);
            }
        }
    }

    /// <summary>
    /// 相机初始化系统
    /// （放在初始化系统组，保证在比较前的位置执行）
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct CameraInititalizationSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<InititalizeCameraTargetTag>();           // 当世界中存在至少一个具备指定组件的实体时，系统的OnUpdate才会执行；
        }

        public void OnUpdate(ref SystemState state)
        {
            if (CameraTargetSingleton.Instance == null)
            {
                return;
            }

            Transform cameraTargetTransform = CameraTargetSingleton.Instance.transform;

            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);
            foreach (var (cameraTarget, entity) in SystemAPI.Query<RefRW<CameraTarget>>().WithAll<InititalizeCameraTargetTag, PlayerTag>().WithEntityAccess())
            {
                cameraTarget.ValueRW.cameraTransform = cameraTargetTransform;
                ecb.RemoveComponent<InititalizeCameraTargetTag>(entity);            // 这里移除掉组件（结构性改变，所以使用ecb进行延迟性批量改动）
            }

            ecb.Playback(state.EntityManager);
        }
    }

    /// <summary>
    /// 相机移动系统
    /// （放在transform系统组中更新）
    /// </summary>
    [UpdateAfter(typeof(TransformSystemGroup))]
    public partial struct MoveCameraSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (localToWorld, cameraTarget) in SystemAPI.Query<RefRO<LocalToWorld>, RefRW<CameraTarget>>().WithAll<PlayerTag>().WithNone<InititalizeCameraTargetTag>())         // 需要玩家标签，并且已经初始化完毕了
            {
                cameraTarget.ValueRW.cameraTransform.Value.position = localToWorld.ValueRO.Position;
            }
        }
    }
    
    public partial class PlayerInputSystem : SystemBase
    {
        public SurvivorsInput input;

        protected override void OnCreate()
        {
            input = new();
            input.Enable();
        }

        protected override void OnUpdate()
        {
            var curInput = (float2)input.Player.Move.ReadValue<Vector2>();
            foreach (var direction in SystemAPI.Query<RefRW<CharacterMoveDirection>>().WithAll<PlayerTag>())
            {
                direction.ValueRW.value = curInput;
            }
        }
    }
    
    public partial struct PlayerAttackSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BeginInitializationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<PhysicsWorldSingleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var elapsedTime = SystemAPI.Time.ElapsedTime;
            var ecbSystem = SystemAPI.GetSingleton<BeginInitializationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSystem.CreateCommandBuffer(state.WorldUnmanaged);
            // 提供对整个物理世界（刚体、碰撞体等）状态的安全访问
            var physicsWorldSingleton = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
            foreach (var (cooldownExpirationTimestamp, attackData, transform) in 
                     SystemAPI.Query<RefRW<PlayerCooldownExpirationTimestamp>, RefRO<PlayerAttackData>, RefRO<LocalTransform>>())
            {
                if (cooldownExpirationTimestamp.ValueRO.value > elapsedTime)
                    continue;
                var spawnPosition = transform.ValueRO.Position;

                // 定义一个轴对齐包围盒（AABB），用于查询该区域内的所有碰撞体
                var aabbInput = new OverlapAabbInput
                {
                    Aabb = new Aabb
                    {
                        Min = spawnPosition - attackData.ValueRO.detectionSize,
                        Max = spawnPosition + attackData.ValueRO.detectionSize
                    },
                    Filter = attackData.ValueRO.collisionFilter,
                };
                // 查询所有重叠的Entity；
                var overlapHits = new NativeList<int>(state.WorldUpdateAllocator);
                bool hasEnemy = physicsWorldSingleton.OverlapAabb(aabbInput, ref overlapHits);
                if (!hasEnemy)
                    continue;
                // 找到警戒范围内最近的敌人；
                var maxDistanceSq = float.MaxValue;
                var closestEnemyPosition = float3.zero;
                foreach (var overlapHit in overlapHits)
                {
                    var curEnemyPosition = physicsWorldSingleton.Bodies[overlapHit].WorldFromBody.pos;
                    var distanceToPlayerSq = math.distance(spawnPosition.xy, curEnemyPosition.xy);
                    if (distanceToPlayerSq < maxDistanceSq)
                    {
                        // 更新数据
                        maxDistanceSq = distanceToPlayerSq;
                        closestEnemyPosition = curEnemyPosition;
                    }
                }
                var vectorToClosestEnemy = closestEnemyPosition - spawnPosition;
                // 计算上一步得到的方向向量与正X轴之间夹角的弧度值；
                var angleToClosestEnemy = math.atan2(vectorToClosestEnemy.y, vectorToClosestEnemy.x);
                var spawnOrientation = quaternion.Euler(0.0f, 0.0f, angleToClosestEnemy);
                
                // 要知道，命令缓冲区仅仅是记录我们的操作，此时并不会马上实例化出来；
                var newAttack = ecb.Instantiate(attackData.ValueRO.AttackPrefab);
                // 更新位置
                ecb.SetComponent(newAttack, LocalTransform.FromPositionRotation(spawnPosition, spawnOrientation));
                // 更新冷却
                cooldownExpirationTimestamp.ValueRW.value = elapsedTime + attackData.ValueRO.coolDownTime;
            }
        }
    }

    public partial struct UpdateGemUISystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (gemCount, shouldUpdateUI) in SystemAPI.Query<RefRO<GemsCollectedCount>, EnabledRefRW<UpdateGemUIFlag>>())
            {
                GameUIController.Instance.UpdateGemsCollectedText(gemCount.ValueRO.value);
                shouldUpdateUI.ValueRW = false;
            }
        }
    }
}
