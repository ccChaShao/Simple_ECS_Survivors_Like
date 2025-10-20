using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

namespace Charasiew.ECS
{
    public class CharacterMoveDirection : IComponentData
    {
       public float2 value;
    }

    public class CharacterMoveSpeed : IComponentData
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
                AddComponent<CharacterMoveDirection>(entity);
                AddComponent<CharacterMoveSpeed>(entity);
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
