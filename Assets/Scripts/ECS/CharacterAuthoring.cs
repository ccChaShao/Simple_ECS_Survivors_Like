using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Rendering;
using Unity.Transforms;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Serialization;

// 在 Unity ECS 中，
// IComponentData和 IBufferElementData是两种核心的组件类型，它们最根本的区别在于：
// IComponentData每个实体只能持有该类型组件的一个实例（单值），而 IBufferElementData则允许实体持有该类型的多个元素，形成一个动态数组；

namespace Charasiew.ECS
{
    public struct InitializeCharacterFlag: IComponentData, IEnableableComponent { }
    
    public struct CharacterMoveDirection : IComponentData
    {
       public float2 value;
    }

    public struct CharacterMoveSpeed : IComponentData
    {
        public float value;
    }

    [MaterialProperty("_FacingDirection")]
    public struct FacingDirectionOverride : IComponentData
    {
        public float value;
    }

    public struct CharacterMaxHitPoints : IComponentData
    {
        public float value;
    }

    public struct CharacterCurrentHitPoint : IComponentData
    {
        public float value;
    }

    public struct DamgeThisFrame : IBufferElementData
    {
        public float value;
    }
    
    public class CharacterAuthoring : MonoBehaviour
    {
        public float moveSpeed = 5.0f;
        public float maxhitPoint = 10.0f;
        
        private class Baker : Baker<CharacterAuthoring>
        {
            public override void Bake(CharacterAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new CharacterMoveDirection());
                AddComponent(entity, new InitializeCharacterFlag());
                AddComponent(entity, new CharacterMoveSpeed { value = authoring.moveSpeed });
                AddComponent(entity, new FacingDirectionOverride { value = 1 });
                AddComponent(entity, new CharacterMaxHitPoints { value = authoring.maxhitPoint });
                AddComponent(entity, new CharacterCurrentHitPoint { value = authoring.maxhitPoint });
                AddBuffer<DamgeThisFrame>(entity);
            }
        }
    }

    /// <summary>
    /// 角色初始化系统
    /// （放在初始化系统组，保证在比较前的位置执行）
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct CharacterInitializeSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (mass, initFlag) in SystemAPI.Query<RefRW<PhysicsMass>, EnabledRefRW<InitializeCharacterFlag>>())
            {
                mass.ValueRW.InverseInertia = float3.zero;
                initFlag.ValueRW = false;           // 状态更新
            }
        }
    }
    
    /// <summary>
    /// 角色移动系统
    /// </summary>
    public partial struct CharacterMoveSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (physicsVelocity,facingDirectionOverride, moveDirection, moveSpeed, entity) in 
                     SystemAPI.Query<
                         RefRW<PhysicsVelocity>, 
                         RefRW<FacingDirectionOverride>, 
                         RefRO<CharacterMoveDirection>, 
                         RefRO<CharacterMoveSpeed>
                     >().WithEntityAccess()
                     )
            {
                // 更新物理系统的线性速度
                var moveStep2d = moveDirection.ValueRO.value * moveSpeed.ValueRO.value;
                physicsVelocity.ValueRW.Linear = new float3(moveStep2d, 0);
                
                // 更新朝向
                if (math.abs(moveStep2d.x) > 0.15f)
                {
                    facingDirectionOverride.ValueRW.value = math.sign(moveStep2d.x);
                }
                
                // 玩家动画更新
                if (SystemAPI.HasComponent<PlayerTag>(entity))
                {
                    var animationOverride = SystemAPI.GetComponentRW<AnimationIndexOverride>(entity);
                    var animationType = math.lengthsq(moveStep2d) > float.Epsilon           // 大于一个很小的值
                        ? PlayerAnimationIndex.Movement
                        : PlayerAnimationIndex.Idle;          
                    animationOverride.ValueRW.value = (float)animationType;
                }
            }
        }
    }
    
    /// <summary>
    /// 全局时间系统
    /// </summary>
    public partial struct GlobalTimeUpdateSystem: ISystem
    {
        private static int globalTimeShaderPropertyID;          // 存储全局时间的shaderID

        public void OnCreate(ref SystemState state)
        {
            globalTimeShaderPropertyID = Shader.PropertyToID("_GlobalTime");            
        }

        public void OnUpdate(ref SystemState state)
        {
            Shader.SetGlobalFloat(globalTimeShaderPropertyID, (float)SystemAPI.Time.ElapsedTime);
        }
    }
    
    public partial struct ProcessDamgeThisFrameSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (hitPoint, damgeThisFrame) in SystemAPI.Query<RefRW<CharacterCurrentHitPoint>, DynamicBuffer<DamgeThisFrame>>())
            {
                if (damgeThisFrame.IsEmpty)
                    continue;
                foreach (var damge in damgeThisFrame)
                {
                    hitPoint.ValueRW.value -= damge.value;
                }
                damgeThisFrame.Clear();
            }
        }
    }
}
