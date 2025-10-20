using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
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
            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (localTransform, moveDirection, moveSpeed) in SystemAPI.Query<RefRW<LocalTransform>, CharacterMoveDirection, CharacterMoveSpeed>())
            {
                // 移动差值
                var moveDiff = moveDirection.value * moveSpeed.value * deltaTime;
                // 移动执行
                localTransform.ValueRW.Position += new float3(moveDiff, 0);
            }
        }
    }
}
