using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

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
    
    public class CharacterAuthoring : MonoBehaviour
    {
        public float moveSpeed = 5.0f;
        
        private class Baker : Baker<CharacterAuthoring>
        {
            public override void Bake(CharacterAuthoring authoring)
            {
                Entity entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new CharacterMoveDirection());
                AddComponent(entity, new CharacterMoveSpeed { value = authoring.moveSpeed });
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
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (mass, initFlag) in SystemAPI.Query<RefRW<PhysicsMass>, EnabledRefRW<InitializeCharacterFlag>>())
            {
                
            }
        }
    }
    
    public partial struct CharacterMoveSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (physicsVelocity, moveDirection, moveSpeed) in SystemAPI.Query<RefRW<PhysicsVelocity>, CharacterMoveDirection, CharacterMoveSpeed>())
            {
                var move2 = moveDirection.value * moveSpeed.value;
                physicsVelocity.ValueRW.Linear = new float3(move2, 0);
            }
        }
    }
}
