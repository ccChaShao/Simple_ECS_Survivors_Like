using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Charasiew.ECS
{
    public struct PlayerTag : IComponentData { }
    
    public class PlayerAuthoring : MonoBehaviour
    {
        private class Baker: Baker<PlayerAuthoring>
        {
            public override void Bake(PlayerAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<PlayerTag>(entity);
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
            Debug.Log("charsiew : [OnUpdate] : --------------------" + curInput);
        }
    }
}
